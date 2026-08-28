using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;

namespace MandelbrotAvalonia
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args)
            => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
    }

    public class App : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }

    public class MainWindow : Window
    {
        private Image _imageControl;
        private WriteableBitmap? _bitmap;

        private double _zoom = 1.0;
        private double _targetX = -0.5;
        private double _targetY = 0.0;
        private readonly double _widthRange = 3.5;
        private readonly double _heightRange = 2.5;
        private const int MaxIterations = 300;

        private bool _isDragging = false;
        private Point _lastMousePosition;

        public MainWindow()
        {
            Title = "Mandelbrot Explorer (Drag to Move / Scroll Wheel to Zoom)";
            Width = 800;
            Height = 600;

            _imageControl = new Image { Stretch = Stretch.Fill };
            Content = _imageControl;

            Resized += (s, e) => { InitBitmap(); RenderFractal(); };

            // 마우스 이벤트 등록 (드래그 & 휠)
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerWheelChanged += OnPointerWheelChanged;

            InitBitmap();
            RenderFractal();
        }

        private void InitBitmap()
        {
            int w = Math.Max(1, (int)ClientSize.Width);
            int h = Math.Max(1, (int)ClientSize.Height);

            _bitmap = new WriteableBitmap(
                new PixelSize(w, h),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            _imageControl.Source = _bitmap;
        }

        #region Mouse Events (Drag & Scroll)

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            
            // 마우스 좌클릭 시 드래그 시작
            if (point.Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                _lastMousePosition = point.Position;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDragging) return;

            Point currentMousePos = e.GetPosition(this);
            Vector delta = _lastMousePosition - currentMousePos;

            // 마우스 이동량을 복소수 평면 스케일로 변환하여 중심 위치 이동
            double currentWidth = _widthRange / _zoom;
            double currentHeight = _heightRange / _zoom;

            _targetX += (delta.X / ClientSize.Width) * currentWidth;
            _targetY += (delta.Y / ClientSize.Height) * currentHeight;

            _lastMousePosition = currentMousePos;

            RenderFractal();
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                _isDragging = false;
            }
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            Point mousePos = e.GetPosition(this);

            // 현재 마우스 포인터 위치의 복소수 좌표 계산
            double currentWidth = _widthRange / _zoom;
            double currentHeight = _heightRange / _zoom;
            double minX = _targetX - (currentWidth / 2.0);
            double minY = _targetY - (currentHeight / 2.0);

            double mouseRealX = minX + (mousePos.X / ClientSize.Width) * currentWidth;
            double mouseRealY = minY + (mousePos.Y / ClientSize.Height) * currentHeight;

            // 휠 업/다운 배율 설정 (위로 굴리면 확대, 아래로 굴리면 축소)
            double zoomFactor = e.Delta.Y > 0 ? 1.25 : 0.8;
            double newZoom = _zoom * zoomFactor;

            if (newZoom < 0.5) newZoom = 0.5; // 최소 줌 제한

            // 마우스 커서 위치를 기준점(Pivot)으로 확대/축소되도록 _targetX, _targetY 재조정
            double newWidth = _widthRange / newZoom;
            double newHeight = _heightRange / newZoom;

            _targetX = mouseRealX - (mousePos.X / ClientSize.Width - 0.5) * newWidth;
            _targetY = mouseRealY - (mousePos.Y / ClientSize.Height - 0.5) * newHeight;
            _zoom = newZoom;

            RenderFractal();
        }

        #endregion

        private unsafe void RenderFractal()
        {
            if (_bitmap == null) return;

            int width = _bitmap.PixelSize.Width;
            int height = _bitmap.PixelSize.Height;

            if (width <= 0 || height <= 0) return;

            using (ILockedFramebuffer fb = _bitmap.Lock())
            {
                byte* ptrScan0 = (byte*)fb.Address;
                int stride = fb.RowBytes;

                double currentWidth = _widthRange / _zoom;
                double currentHeight = _heightRange / _zoom;
                double minX = _targetX - (currentWidth / 2.0);
                double minY = _targetY - (currentHeight / 2.0);

                Parallel.For(0, height, y =>
                {
                    byte* row = ptrScan0 + (y * stride);
                    double cy = minY + (y / (double)height) * currentHeight;

                    for (int x = 0; x < width; x++)
                    {
                        double cx = minX + (x / (double)width) * currentWidth;
                        int iter = GetMandelbrotIterations(cx, cy);

                        // 각 픽셀에 색상을 칠함
                        byte r = 0, g = 0, b = 0;
                        if (iter < MaxIterations)
                        {
                            double t = (double)iter / MaxIterations;
                            r = (byte)(9 * (1 - t) * t * t * t * 255);
                            g = (byte)(15 * (1 - t) * (1 - t) * t * t * 255);
                            b = (byte)(8.5 * (1 - t) * (1 - t) * (1 - t) * t * 255);
                        }

                        int pixelIndex = x * 4;
                        row[pixelIndex] = b;
                        row[pixelIndex + 1] = g;
                        row[pixelIndex + 2] = r;
                        row[pixelIndex + 3] = 255;
                    }
                });
            }

            _imageControl.InvalidateVisual();
        }

        // 핵심 수학 연산
        private int GetMandelbrotIterations(double cx, double cy)
        { // cx, cy는 복소평면의 한 점
            // 현재 위치의 x(실수부), y(허수부)
            double zx = 0.0, zy = 0.0; 
            // x, y의 제곱
            double zx2 = 0.0, zy2 = 0.0;
            int iter = 0;

            // 반지름이 2인 원. 이 계산이 MaxIteration까지 반복되도 점이 갇혀있다면 검은색으로 칠해짐.
            while (zx2 + zy2 <= 10 && iter < MaxIterations)
            {
                // 새로운 허수부
                zy = 2.0 * zx * zy + cy;
                // 새로운 실수부
                zx = zx2 - zy2 + cx;

                // 제곱
                zx2 = zx * zx;
                zy2 = zy * zy;
                iter++;
            }

            return iter;
        }
    }
}