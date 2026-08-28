using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Input;

namespace AvaloniaApp;



public class InteractiveCanvas : Control
{
    private Vector _pan = new Vector(0, 0); // 드래그 이동 오프셋
    private double _zoom = 2.0;            // 확대/축소 배율
    private Point _lastMousePos;
    private bool _isDragging;

    public InteractiveCanvas()
    {
        // 마우스 이벤트 연결

    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _lastMousePos = e.GetPosition(this); // 드래그 시작 위치 저장
            e.Pointer.Capture(this); // 마우스 포인터 캡처 (드래그 중 이탈 방지)
        }
    }

    // 2. 마우스 이동
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_isDragging)
        {
            Point currentPos = e.GetPosition(this);
            Vector delta = currentPos - _lastMousePos;

            _pan += delta; // 마우스 이동량만큼 Pan 누적
            _lastMousePos = currentPos;

            InvalidateVisual(); // 화면 갱신 (Render 호출)
        }
    }

    // 3. 마우스 뗌
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isDragging = false;
        e.Pointer.Capture(null); // 포인터 캡처 해제
    }

    // 4. 마우스 휠
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        // 휠 방향에 따라 10%씩 확대/축소
        double zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
        _zoom *= zoomFactor;

        // 줌 범위 제한 (0.2배 ~ 5.0배)
        _zoom = Math.Clamp(_zoom, 0.2, 10.0);

        InvalidateVisual(); // 화면 갱신
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double width = Bounds.Width;
        double height = Bounds.Height;

        // 히트 테스트 영역 확보
        context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));

        // Pan이 반영된 현재 원점(0,0)의 화면 좌표
        double originX = (width / 2) + _pan.X;
        double originY = (height / 2) + _pan.Y;

        // 눈금 간격(Step) 동적 계산(목표 픽셀 간격: 약 80px)
        double targetPixels = 40.0;
        double rawStep = targetPixels / (50.0 * _zoom);

        // 1-2-5 단위로 깔끔한 정수/소수 단위 구하기
        double exponent = Math.Floor(Math.Log10(rawStep));
        double fraction = rawStep / Math.Pow(10, exponent);

        double niceFraction;
        if (fraction < 1.5) niceFraction = 1.0;
        else if (fraction < 3.0) niceFraction = 2.0;
        else if (fraction < 7.0) niceFraction = 5.0;
        else niceFraction = 10.0;

        // 계산된 실제 좌표 단위 (예: 2, 1, 0.5, 0.2, 0.1, 0.05 ...)
        double step = niceFraction * Math.Pow(10, exponent);
        // 화면상의 실제 픽셀 간격
        double stepInPixels = step * 50.0 * _zoom;

        // Zoom이 반영된 눈금 간격 (기본 50px * 배율)
        double gridSize = 50.0 * _zoom;

        // 배경 격자(Grid) 그리기
        var gridPen = new Pen(Brushes.LightGray, 1);
        var axisPen = new Pen(Brushes.Black, 2);

        string numberFormat = step switch
        {
            < 0.01 => "0.000",
            < 0.1 => "0.00",
            < 1.0 => "0.0",
            _ => "0"
        };

        // 2. 세로 눈금선 & X축 숫자 (화면 좌측 끝~우측 끝 인덱스 계산)
        int minXIndex = (int)Math.Floor((0 - originX) / stepInPixels);
        int maxXIndex = (int)Math.Ceiling((width - originX) / stepInPixels);

        for (int i = minXIndex; i <= maxXIndex; i++)
        {
            double x = originX + (i * stepInPixels);
            double realValue = Math.Round(i * step, 6); // 부동소수점 오차 방지

            context.DrawLine(gridPen, new Point(x, 0), new Point(x, height));

            var text = new FormattedText(
                realValue.ToString(numberFormat),
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                11,
                Brushes.DimGray
            );

            if (step <= 0.5 && realValue == (int)realValue)
            {
                text = new FormattedText(
                realValue.ToString(numberFormat),
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                14,
                Brushes.Black
            );
            }

            context.DrawText(text, new Point(x + 3, originY + 3));





        }

        // 3. 가로 눈금선 & Y축 숫자
        int minYIndex = (int)Math.Floor((0 - originY) / stepInPixels);
        int maxYIndex = (int)Math.Ceiling((height - originY) / stepInPixels);

        for (int i = minYIndex; i <= maxYIndex; i++)
        {
            double y = originY + (i * stepInPixels);
            double realValue = Math.Round(-i * step, 6); // Y축 좌표계 반전

            context.DrawLine(gridPen, new Point(0, y), new Point(width, y));

            // 0은 X축 루프에서 출력하므로 중복 방지
            if (Math.Abs(realValue) > 1e-6)
            {
                var text = new FormattedText(
                    realValue.ToString(numberFormat),
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    11,
                    Brushes.DimGray
                );

                if (step <= 0.5 && realValue == (int)realValue)
                {
                    text = new FormattedText(
                    realValue.ToString(numberFormat),
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    14,
                    Brushes.Black
                );
                }

                context.DrawText(text, new Point(originX + 3, y + 3));
            }
        }



        // 4. 메인 X축 / Y축 그리기

        context.DrawLine(axisPen, new Point(0, originY), new Point(width, originY)); // X축
        context.DrawLine(axisPen, new Point(originX, 0), new Point(originX, height)); // Y축

        var zoomText = new FormattedText(
        $"Zoom: {_zoom:F2}x | Step: {step}",
        System.Globalization.CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        Typeface.Default,
        20,
        Brushes.Black
    );
        context.DrawText(zoomText, new Point(10, 10));

    }
}

public class MainWindow : Window
{
    public MainWindow()
    {
        Title = "그림 예제";
        Width = 1280;
        Height = 800;

        Content = new InteractiveCanvas();
    }
}

public class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime destkop)
        {
            destkop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

public class Imaginary
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        Console.WriteLine("Hello11");

        const int MaxIteration = 300;

        double zx = 0.0, zy = 0.0;
        double zx2 = 0.0, zy2 = 0.0;
        double cx, cy;

        cx = 0.1;
        cy = 0.1;

        int iter = 0;

        Console.WriteLine($"C = ({cx}, {cy})\n\n");

        while (zx2 + zy2 <= 2.0 && iter < MaxIteration)
        {
            zy = 2.0 * zx * zy + cy;
            zx = zx2 - zy2 + cx;

            Console.WriteLine($"Z{iter} = ({zx}, {zy})\n\n");

            zx2 = zx * zx;
            zy2 = zy * zy;

            iter++;
        }


    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}