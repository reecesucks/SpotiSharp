namespace SpotiSharp.Controls;

/// <summary>Circular download indicator: an indeterminate spinning arc while <see cref="Progress"/> is
/// negative (the server hasn't told us the total size yet), then a determinate ring filling clockwise
/// as Progress goes from 0 to 1 - used for AudioBookshelf episode downloads, which can take minutes.</summary>
public class DownloadProgressRing : GraphicsView, IDrawable
{
    public static readonly BindableProperty ProgressProperty =
        BindableProperty.Create(nameof(Progress), typeof(double), typeof(DownloadProgressRing), -1.0, propertyChanged: OnProgressChanged);

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public static readonly BindableProperty RingColorProperty =
        BindableProperty.Create(nameof(RingColor), typeof(Color), typeof(DownloadProgressRing), Colors.Gray,
            propertyChanged: (bindable, _, _) => ((DownloadProgressRing)bindable).Invalidate());

    public Color RingColor
    {
        get => (Color)GetValue(RingColorProperty);
        set => SetValue(RingColorProperty, value);
    }

    private const float StrokeWidth = 2.5f;

    private double _indeterminateAngle;
    private IDispatcherTimer? _animationTimer;

    public DownloadProgressRing()
    {
        Drawable = this;
    }

    private static void OnProgressChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var ring = (DownloadProgressRing)bindable;
        if ((double)newValue < 0) ring.StartIndeterminateAnimation();
        else ring.StopIndeterminateAnimation();
        ring.Invalidate();
    }

    private void StartIndeterminateAnimation()
    {
        if (_animationTimer != null) return;

        _animationTimer = Dispatcher.CreateTimer();
        _animationTimer.Interval = TimeSpan.FromMilliseconds(40);
        _animationTimer.Tick += (_, _) =>
        {
            _indeterminateAngle = (_indeterminateAngle + 12) % 360;
            Invalidate();
        };
        _animationTimer.Start();
    }

    private void StopIndeterminateAnimation()
    {
        if (_animationTimer == null) return;
        _animationTimer.Stop();
        _animationTimer = null;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var ringRect = dirtyRect;
        ringRect.Inflate(-StrokeWidth, -StrokeWidth);
        if (ringRect.Width <= 0 || ringRect.Height <= 0) return;

        canvas.StrokeSize = StrokeWidth;
        canvas.StrokeColor = RingColor.WithAlpha(0.25f);
        canvas.DrawEllipse(ringRect);

        canvas.StrokeColor = RingColor;
        if (Progress < 0)
        {
            // a quarter-circle segment chasing itself around the ring
            canvas.DrawArc(ringRect.X, ringRect.Y, ringRect.Width, ringRect.Height,
                (float)_indeterminateAngle, (float)_indeterminateAngle + 90, true, false);
        }
        else if (Progress > 0)
        {
            // starts at 12 o'clock (90°) and sweeps clockwise, i.e. decreasing angle in MAUI's
            // counterclockwise-positive convention
            var sweep = (float)(Math.Clamp(Progress, 0, 1) * 360);
            canvas.DrawArc(ringRect.X, ringRect.Y, ringRect.Width, ringRect.Height, 90, 90 - sweep, true, false);
        }
    }
}
