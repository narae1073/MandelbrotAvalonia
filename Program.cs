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
            Title = "Mandelbrot Interactive Navigator (Left Click: Zoom In / Right Click: Zoom Out)";
            Width = 800;
            Height = 600;

            _imageControl = new Image { Stretch = Stretch.Fill };
            Content = _imageControl;

            Resized += (s, e) => { InitBitmap(); RenderFractal(); };

            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;

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

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            Point mousePos = point.Position;

            double currentWidth = _widthRange / _zoom;
            double currentHeight = _heightRange / _zoom;
            double minX = _targetX - (currentWidth / 2.0);
            double minY = _targetY - (currentHeight / 2.0);

            double clickedRealX = minX + (mousePos.X / ClientSize.Width) * currentWidth;
            double clickedRealY = minY + (mousePos.Y / ClientSize.Height) * currentHeight;

            if (point.Properties.IsLeftButtonPressed)
            {
                _targetX = clickedRealX;
                _targetY = clickedRealY;
                _zoom *= 2.0;

                _isDragging = true;
                _lastMousePosition = mousePos;

                RenderFractal();
            }
            else if (point.Properties.IsRightButtonPressed)
            {
                _targetX = clickedRealX;
                _targetY = clickedRealY;
                _zoom /= 2.0;
                if (_zoom < 0.5) _zoom = 0.5;

                RenderFractal();
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDragging) return;

            Point currentMousePos = e.GetPosition(this);
            Vector delta = _lastMousePosition - currentMousePos;

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

        private int GetMandelbrotIterations(double cx, double cy)
        {
            double zx = 0.0, zy = 0.0;
            double zx2 = 0.0, zy2 = 0.0;
            int iter = 0;

            while (zx2 + zy2 <= 4.0 && iter < MaxIterations)
            {
                zy = 2.0 * zx * zy + cy;
                zx = zx2 - zy2 + cx;
                zx2 = zx * zx;
                zy2 = zy * zy;
                iter++;
            }

            return iter;
        }
    }
}