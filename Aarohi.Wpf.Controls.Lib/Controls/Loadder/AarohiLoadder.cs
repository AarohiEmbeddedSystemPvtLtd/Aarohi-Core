using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AarohiWpfControls.Controls.Loadder
{
    public class AarohiLoadder : Control
    {
        public enum EasingType { Linear, EaseIn, EaseOut, EaseInOut, SmoothStep }
        public enum RevealMode { Radial, Vertical, Horizontal }
        public enum FillTimingMode { WithStrokes, AfterStrokes, Custom }
        public enum StrokeGroup { Logo, Text }

        private const double MarginPixels = 10.0;
        private const double DefaultWidth = 710.0;
        private const double DefaultHeight = 694.0;

        private readonly List<Line> _lines = new();
        private readonly List<FillRegion> _fillRegions = new();
        private readonly Stopwatch _stopwatch = new();

        private EasingType _easingType = EasingType.EaseInOut;
        private Func<double, double>? _customEasing;
        private FillTimingMode _fillTiming = FillTimingMode.WithStrokes;
        private double _fillAfterOffsetSeconds = 0.05;
        private double _globalDurationSeconds = 1.0;
        private Rect _worldBounds = new(0, 0, 1, 1);

        private long _lastElapsedMs;
        private double _lastFrameSeconds;
        private bool _isRenderingAttached;
        private bool _isRunning;

        private bool _strokeFadeStarted;
        private double _strokeFadeStartTime;
        private double _strokeAlphaFactor = 1.0;

        private bool _guideFadeStarted;
        private double _guideFadeStartTime;
        private double _guideAlphaFactor = 1.0;

        private Brush? _cachedStrokeBrush;
        private Brush? _cachedTextStrokeBrush;
        private Brush? _cachedGuideBrush;
        private Brush? _cachedBackgroundBrush;

        public static readonly DependencyProperty AutoStartProperty =
            DependencyProperty.Register(
                nameof(AutoStart),
                typeof(bool),
                typeof(AarohiLoadder),
                new PropertyMetadata(true));

        // LogoOnlyMode is used by compact header placements where the filled Aarohi mark
        // and word strokes should render instantly without startup construction animation.
        public static readonly DependencyProperty LogoOnlyModeProperty =
            DependencyProperty.Register(
                nameof(LogoOnlyMode),
                typeof(bool),
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnLogoOnlyModeChanged));

        public static readonly DependencyProperty TargetFramesPerSecondProperty =
            DependencyProperty.Register(
                nameof(TargetFramesPerSecond),
                typeof(int),
                typeof(AarohiLoadder),
                new PropertyMetadata(60, OnVisualPropertyChanged, CoerceTargetFramesPerSecond));

        public static readonly DependencyProperty FillRevealModeProperty =
            DependencyProperty.Register(
                nameof(FillRevealMode),
                typeof(RevealMode),
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(RevealMode.Vertical, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DrawFillAboveStrokesProperty =
            DependencyProperty.Register(
                nameof(DrawFillAboveStrokes),
                typeof(bool),
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowGuideLinesProperty =
            DependencyProperty.Register(
                nameof(ShowGuideLines),
                typeof(bool),
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GuideFadeDelayProperty =
            DependencyProperty.Register(
                nameof(GuideFadeDelay),
                typeof(double),
                typeof(AarohiLoadder),
                new PropertyMetadata(0.10));

        public static readonly DependencyProperty GuideFadeDurationProperty =
            DependencyProperty.Register(
                nameof(GuideFadeDuration),
                typeof(double),
                typeof(AarohiLoadder),
                new PropertyMetadata(0.60));

        public static readonly DependencyProperty FadeStrokesAfterFillProperty =
            DependencyProperty.Register(
                nameof(FadeStrokesAfterFill),
                typeof(bool),
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeFadeDurationProperty =
            DependencyProperty.Register(
                nameof(StrokeFadeDuration),
                typeof(double),
                typeof(AarohiLoadder),
                new PropertyMetadata(0.9));

        public static readonly DependencyProperty StrokeFadeDelayProperty =
            DependencyProperty.Register(
                nameof(StrokeFadeDelay),
                typeof(double),
                typeof(AarohiLoadder),
                new PropertyMetadata(0.05));

        public static readonly DependencyProperty EnableReflectionProperty =
            DependencyProperty.Register(
                nameof(EnableReflection),
                typeof(bool),
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnReflectionChanged));

        public static readonly DependencyProperty ReflectionSpeedProperty =
            DependencyProperty.Register(
                nameof(ReflectionSpeed),
                typeof(double),
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(0.2, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ReflectionIntensityProperty =
            DependencyProperty.Register(
                nameof(ReflectionIntensity),
                typeof(double),
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(0.6, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ReflectionAngleProperty =
            DependencyProperty.Register(
                nameof(ReflectionAngle),
                typeof(double),
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(-30.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ReflectionThicknessProperty =
            DependencyProperty.Register(
                nameof(ReflectionThickness),
                typeof(double),
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(0.22, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeBrushProperty =
            DependencyProperty.Register(
                nameof(StrokeBrush),
                typeof(Brush),
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(CreateFrozenBrush(Color.FromRgb(40, 24, 119)), FrameworkPropertyMetadataOptions.AffectsRender, OnBrushChanged));

        public static readonly DependencyProperty TextStrokeBrushProperty =
            DependencyProperty.Register(
                nameof(TextStrokeBrush),
                typeof(Brush),
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(CreateFrozenBrush(Color.FromRgb(40, 24, 119)), FrameworkPropertyMetadataOptions.AffectsRender, OnBrushChanged));

        // Separate thickness values let small header logos tune the wordmark without
        // changing the construction stroke weight used by the full startup animation.
        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register(
                nameof(StrokeThickness),
                typeof(double),
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

        public static readonly DependencyProperty TextStrokeThicknessProperty =
            DependencyProperty.Register(
                nameof(TextStrokeThickness),
                typeof(double),
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(5.0, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

        public static readonly DependencyProperty GuideBrushProperty =
            DependencyProperty.Register(
                nameof(GuideBrush),
                typeof(Brush),
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(CreateFrozenBrush(Color.FromRgb(200, 200, 200)), FrameworkPropertyMetadataOptions.AffectsRender, OnBrushChanged));

        static AarohiLoadder()
        {
            BackgroundProperty.OverrideMetadata(
                typeof(AarohiLoadder),
                new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender, OnBrushChanged));
        }

        public AarohiLoadder()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            Focusable = false;

            BuildDefaultShape();
            ComputeWorldBounds();
            ResetAnimation();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool AutoStart
        {
            get => (bool)GetValue(AutoStartProperty);
            set => SetValue(AutoStartProperty, value);
        }

        /// <summary>
        /// Renders the filled logo and Aarohi text immediately, skipping animation,
        /// guide lines, reflection, and symbol construction strokes.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool LogoOnlyMode
        {
            get => (bool)GetValue(LogoOnlyModeProperty);
            set => SetValue(LogoOnlyModeProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int TargetFramesPerSecond
        {
            get => (int)GetValue(TargetFramesPerSecondProperty);
            set => SetValue(TargetFramesPerSecondProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public RevealMode FillRevealMode
        {
            get => (RevealMode)GetValue(FillRevealModeProperty);
            set => SetValue(FillRevealModeProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool DrawFillAboveStrokes
        {
            get => (bool)GetValue(DrawFillAboveStrokesProperty);
            set => SetValue(DrawFillAboveStrokesProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowGuideLines
        {
            get => (bool)GetValue(ShowGuideLinesProperty);
            set => SetValue(ShowGuideLinesProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double GuideFadeDelay
        {
            get => (double)GetValue(GuideFadeDelayProperty);
            set => SetValue(GuideFadeDelayProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double GuideFadeDuration
        {
            get => (double)GetValue(GuideFadeDurationProperty);
            set => SetValue(GuideFadeDurationProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool FadeStrokesAfterFill
        {
            get => (bool)GetValue(FadeStrokesAfterFillProperty);
            set => SetValue(FadeStrokesAfterFillProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double StrokeFadeDuration
        {
            get => (double)GetValue(StrokeFadeDurationProperty);
            set => SetValue(StrokeFadeDurationProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double StrokeFadeDelay
        {
            get => (double)GetValue(StrokeFadeDelayProperty);
            set => SetValue(StrokeFadeDelayProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool EnableReflection
        {
            get => (bool)GetValue(EnableReflectionProperty);
            set => SetValue(EnableReflectionProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double ReflectionSpeed
        {
            get => (double)GetValue(ReflectionSpeedProperty);
            set => SetValue(ReflectionSpeedProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double ReflectionIntensity
        {
            get => (double)GetValue(ReflectionIntensityProperty);
            set => SetValue(ReflectionIntensityProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double ReflectionAngle
        {
            get => (double)GetValue(ReflectionAngleProperty);
            set => SetValue(ReflectionAngleProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double ReflectionThickness
        {
            get => (double)GetValue(ReflectionThicknessProperty);
            set => SetValue(ReflectionThicknessProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Brush StrokeBrush
        {
            get => (Brush)GetValue(StrokeBrushProperty);
            set => SetValue(StrokeBrushProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Brush TextStrokeBrush
        {
            get => (Brush)GetValue(TextStrokeBrushProperty);
            set => SetValue(TextStrokeBrushProperty, value);
        }

        /// <summary>
        /// Stroke weight for the logo construction lines.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        /// <summary>
        /// Stroke weight for the Aarohi text/wordmark lines.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double TextStrokeThickness
        {
            get => (double)GetValue(TextStrokeThicknessProperty);
            set => SetValue(TextStrokeThicknessProperty, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Brush GuideBrush
        {
            get => (Brush)GetValue(GuideBrushProperty);
            set => SetValue(GuideBrushProperty, value);
        }

        public bool IsRunning => _isRunning;

        public IReadOnlyList<Line> Lines => _lines;
        public IReadOnlyList<FillRegion> FillRegions => _fillRegions;

        public RevealMode fillRevealMode
        {
            get => FillRevealMode;
            set => FillRevealMode = value;
        }

        public bool drawFillAboveStrokes
        {
            get => DrawFillAboveStrokes;
            set => DrawFillAboveStrokes = value;
        }

        public void SetFillOnTop(bool fillOnTop)
        {
            DrawFillAboveStrokes = fillOnTop;
            InvalidateVisual();
        }

        public void SetFillTiming(FillTimingMode mode, double afterOffsetSeconds = 0.05)
        {
            _fillTiming = mode;
            _fillAfterOffsetSeconds = afterOffsetSeconds;
        }

        public void StartReflection()
        {
            if (LogoOnlyMode)
                return;

            EnableReflection = true;
            if (!_stopwatch.IsRunning)
            {
                _stopwatch.Start();
                _lastElapsedMs = _stopwatch.ElapsedMilliseconds;
                _lastFrameSeconds = _stopwatch.Elapsed.TotalSeconds;
            }
            EnsureRenderingAttached();
        }

        public void StopReflection()
        {
            EnableReflection = false;
        }

        public void SetFillEasing(EasingType easing)
        {
            _easingType = easing;
        }

        public void SetRegionEasing(FillRegion region, EasingType easing)
        {
            if (region != null)
                region.EasingTypeOverride = easing;
        }

        public void SetRegionCustomEasing(FillRegion region, Func<double, double> customEasingFunc)
        {
            if (region != null)
                region.CustomEasing = customEasingFunc;
        }

        public void SetFillRegions(IEnumerable<FillRegion>? regions)
        {
            _fillRegions.Clear();
            if (regions != null)
            {
                foreach (var region in regions)
                {
                    if (region.DurationSeconds <= 0)
                        region.DurationSeconds = _globalDurationSeconds;
                    _fillRegions.Add(region);
                }
            }

            ComputeWorldBounds();
            ResetAnimation();
        }

        public void AddFillRegion(FillRegion region)
        {
            if (region == null)
                return;

            if (region.DurationSeconds <= 0)
                region.DurationSeconds = _globalDurationSeconds;

            _fillRegions.Add(region);
            ComputeWorldBounds();
            InvalidateVisual();
        }

        public void ClearFillRegions()
        {
            _fillRegions.Clear();
            ComputeWorldBounds();
            InvalidateVisual();
        }

        public void StartAnimation()
        {
            if (LogoOnlyMode)
            {
                _isRunning = false;
                _stopwatch.Reset();
                DetachRenderingIfIdle();
                InvalidateVisual();
                return;
            }

            ApplyFillTimingToRegions();
            _guideFadeStarted = false;
            _guideAlphaFactor = 1.0;

            _strokeFadeStarted = false;
            _strokeAlphaFactor = 1.0;

            _isRunning = true;
            _stopwatch.Restart();
            _lastElapsedMs = _stopwatch.ElapsedMilliseconds;
            _lastFrameSeconds = _stopwatch.Elapsed.TotalSeconds;

            EnsureRenderingAttached();
            InvalidateVisual();
        }

        public void StopAnimation()
        {
            _isRunning = false;
            _stopwatch.Reset();
            DetachRenderingIfIdle();
        }

        public void PauseAnimation()
        {
            _stopwatch.Stop();
            _isRunning = false;
            DetachRenderingIfIdle();
        }

        public void ResumeAnimation()
        {
            _isRunning = true;
            if (!_stopwatch.IsRunning)
                _stopwatch.Start();

            _lastElapsedMs = _stopwatch.ElapsedMilliseconds;
            _lastFrameSeconds = _stopwatch.Elapsed.TotalSeconds;
            EnsureRenderingAttached();
        }

        public void ResetAnimation()
        {
            foreach (var line in _lines)
                line.ElapsedSeconds = 0.0;

            foreach (var region in _fillRegions)
                region.ElapsedSeconds = 0.0;

            _guideFadeStarted = false;
            _guideAlphaFactor = 1.0;
            _strokeFadeStarted = false;
            _strokeAlphaFactor = 1.0;

            _isRunning = false;
            _stopwatch.Reset();
            DetachRenderingIfIdle();
            InvalidateVisual();
        }

        public void SetEasing(EasingType type)
        {
            _easingType = type;
            _customEasing = null;
        }

        public void SetCustomEasing(Func<double, double> func)
        {
            _customEasing = func;
        }

        public void SetGlobalDuration(double seconds, bool applyToExisting = false)
        {
            if (seconds <= 0)
                throw new ArgumentException("duration > 0", nameof(seconds));

            _globalDurationSeconds = seconds;

            if (!applyToExisting)
                return;

            foreach (var line in _lines)
                line.DurationSeconds = seconds;

            foreach (var region in _fillRegions)
                region.DurationSeconds = seconds;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var width = double.IsInfinity(constraint.Width) ? DefaultWidth : constraint.Width;
            var height = double.IsInfinity(constraint.Height) ? DefaultHeight : constraint.Height;
            return new Size(Math.Max(0, width), Math.Max(0, height));
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            EnsureBrushCache();

            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            if (_cachedBackgroundBrush != null)
                drawingContext.DrawRectangle(_cachedBackgroundBrush, null, bounds);

            if (LogoOnlyMode)
            {
                // Header/logo-only rendering should be fully settled on first paint:
                // filled logo regions plus completed text strokes, no animated guides.
                DrawFillRegions(drawingContext);
                DrawStrokes(drawingContext, 1.0);
                return;
            }

            var guideAlpha = Clamp(0.0, 0.70, 0.70 * _guideAlphaFactor);
            if (ShowGuideLines && guideAlpha > 0.001 && _cachedGuideBrush != null)
            {
                drawingContext.PushOpacity(guideAlpha);
                var guidePen = CreatePen(_cachedGuideBrush, 1.0);
                foreach (var line in _lines)
                {
                    var (start, end) = line.GetWorldEndpoints();
                    drawingContext.DrawLine(guidePen, MapToScreen(start), MapToScreen(end));
                }
                drawingContext.Pop();
            }

            if (DrawFillAboveStrokes)
            {
                DrawStrokes(drawingContext, _strokeAlphaFactor);
                DrawFillRegions(drawingContext);
                if (EnableReflection)
                    DrawReflection(drawingContext);
            }
            else
            {
                DrawFillRegions(drawingContext);
                if (EnableReflection)
                    DrawReflection(drawingContext);
                DrawStrokes(drawingContext, _strokeAlphaFactor);
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            InvalidateVisual();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (AutoStart && !LogoOnlyMode && !_isRunning)
                StartAnimation();
            else if (_isRunning || EnableReflection)
                EnsureRenderingAttached();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachRendering();
        }

        private void BuildDefaultShape()
        {
            _lines.Clear();
            _fillRegions.Clear();

            const double offset = 0.0;

            _lines.AddRange(new[]
            {
                new Line(0.5, -1.8, 3.1, 8.5),
                new Line(0.5, -0.6, 3.1, 7.7),
                new Line(0.5, -0.1, -0.5, 2.5),
                new Line(0.5, 1.1, -0.5, 2.5),
                new Line(0.5, 1.5, -0.5, 2.5),
                new Line(0.5, 2.7, -0.5, 2.5),
                new Line(0.5, 3.1, -0.5, 2.5),
                new Line(0.5, 4.3, -0.5, 2.5),
                new Line(-0.5, 1.8, 1.3, 4.1),
                new Line(-0.5, 3.0, 1.3, 4.1),
                new Line(-0.5, 3.4, 1.3, 4.1),
                new Line(-0.5, 4.6, 1.3, 4.1),
                new Line(-0.5, 5.0, 1.3, 4.1),
                new Line(-0.5, 6.2, 1.3, 4.1),
                new Line(-0.93, 9.8, -0.5, 8.5),
                new Line(-0.93, 8.2, -0.5, 7.5),
                new Line(3.6, -0.5, 5.0),
                new Line(0, -0.5, 4.9),
                new Line(0, 7.7, 10.3),
                new Line(0.5, 0.3, -2, -0.6) { Group = StrokeGroup.Text },
                new Line(-0.5, -0.3, -2, -0.6) { Group = StrokeGroup.Text },
                new Line(-1.6, -0.5, 0.5) { Group = StrokeGroup.Text },
                new Line(0.5, 2.05, -2, -0.6) { Group = StrokeGroup.Text },
                new Line(-0.5, 1.45, -2, -0.6) { Group = StrokeGroup.Text },
                new Line(-1.6, 1.25, 2.25) { Group = StrokeGroup.Text },
                new Line(0, 3.2, -2, -0.6) { Group = StrokeGroup.Text },
                new Line(0, 4.3, -1.3, -0.6) { Group = StrokeGroup.Text },
                new Line(-0.6, 3.2, 4.3) { Group = StrokeGroup.Text },
                new Line(-1.3, 3.2, 4.3) { Group = StrokeGroup.Text },
                new Line(-1, 2.35, -2, -1.3) { Group = StrokeGroup.Text },
                new Line(0, 5.2, -1.9, -0.6) { Group = StrokeGroup.Text },
                new Line(0, 6.5, -1.9, -0.6) { Group = StrokeGroup.Text },
                new Line(-0.6, 5.2, 6.5) { Group = StrokeGroup.Text },
                new Line(-1.9, 5.2, 6.5) { Group = StrokeGroup.Text },
                new Line(-1.3, 7.4, 8.6) { Group = StrokeGroup.Text },
                new Line(0, 7.4, -1.9, -0.6) { Group = StrokeGroup.Text },
                new Line(0, 8.6, -1.9, -0.6) { Group = StrokeGroup.Text },
                new Line(0, 9.6, -1.9, -0.6) { Group = StrokeGroup.Text }
            });

            foreach (var line in _lines)
            {
                if (!line.IsHorizontal)
                {
                    line.YMin += offset;
                    line.YMax -= offset;
                }

                line.DurationSeconds = 0.9;
                line.DelaySeconds = 0.0;
            }

            _fillRegions.Add(new FillRegion(
                new[]
                {
                    new Point(-0.1, 0.0),
                    new Point(0.85, 1.9),
                    new Point(0, 3.6),
                    new Point(1.67, 7),
                    new Point(2.26, 8.11),
                    new Point(3.03, 7.27),
                    new Point(9.8, 0),
                    new Point(8.2, 0),
                    new Point(2.5, 6.15),
                    new Point(1.2, 3.6),
                    new Point(2.05, 1.9),
                    new Point(1.1, 0)
                },
                Color.FromRgb(40, 24, 119))
            {
                DelaySeconds = 0.0,
                DurationSeconds = 1.2
            });

            _fillRegions.Add(new FillRegion(
                new[]
                {
                    new Point(3.2, 3.6),
                    new Point(4.4, 3.6),
                    new Point(5.25, 1.9),
                    new Point(4.3, 0),
                    new Point(3.1, 0),
                    new Point(4.05, 1.9)
                },
                Color.FromRgb(40, 24, 119))
            {
                DelaySeconds = 0.08,
                DurationSeconds = 1.0
            });

            _fillRegions.Add(new FillRegion(
                new[]
                {
                    new Point(1.6, 3.6),
                    new Point(2.8, 3.6),
                    new Point(3.65, 1.9),
                    new Point(2.7, 0),
                    new Point(1.5, 0),
                    new Point(2.45, 1.9)
                },
                Color.FromRgb(237, 127, 14))
            {
                DelaySeconds = 0.25,
                DurationSeconds = 0.9
            });
        }

        private void ApplyFillTimingToRegions()
        {
            if (_fillTiming == FillTimingMode.Custom)
                return;

            double earliestLineStart = double.PositiveInfinity;
            double latestLineEnd = 0.0;

            if (_lines.Count > 0)
            {
                foreach (var line in _lines)
                {
                    earliestLineStart = Math.Min(earliestLineStart, line.DelaySeconds);
                    latestLineEnd = Math.Max(latestLineEnd, line.DelaySeconds + line.DurationSeconds);
                }

                if (double.IsInfinity(earliestLineStart))
                    earliestLineStart = 0.0;
            }
            else
            {
                earliestLineStart = 0.0;
            }

            foreach (var region in _fillRegions)
            {
                region.DelaySeconds = _fillTiming == FillTimingMode.WithStrokes
                    ? Math.Max(0.0, earliestLineStart)
                    : latestLineEnd + _fillAfterOffsetSeconds;
            }
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (!_isRunning && !EnableReflection)
            {
                DetachRenderingIfIdle();
                return;
            }

            if (!_isRunning && EnableReflection && !_stopwatch.IsRunning)
            {
                _stopwatch.Start();
                _lastElapsedMs = _stopwatch.ElapsedMilliseconds;
                _lastFrameSeconds = _stopwatch.Elapsed.TotalSeconds - (1.0 / TargetFramesPerSecond);
            }

            if (_isRunning && !_stopwatch.IsRunning)
            {
                _stopwatch.Start();
                _lastElapsedMs = _stopwatch.ElapsedMilliseconds;
                _lastFrameSeconds = _stopwatch.Elapsed.TotalSeconds;
                return;
            }

            var nowSeconds = _stopwatch.Elapsed.TotalSeconds;
            var minFrameSeconds = 1.0 / TargetFramesPerSecond;
            if (nowSeconds - _lastFrameSeconds < minFrameSeconds)
                return;

            _lastFrameSeconds = nowSeconds;

            var nowMs = _stopwatch.ElapsedMilliseconds;
            var dt = (nowMs - _lastElapsedMs) / 1000.0;
            _lastElapsedMs = nowMs;

            if (dt <= 0 && !EnableReflection)
                return;

            if (dt > 0.25)
                dt = 0.25;

            var needInvalidate = EnableReflection;
            var globalTime = _stopwatch.Elapsed.TotalSeconds;

            if (_isRunning)
            {
                foreach (var region in _fillRegions)
                {
                    if (region.ElapsedSeconds < region.DurationSeconds && globalTime >= region.DelaySeconds)
                    {
                        region.ElapsedSeconds = Math.Min(region.DurationSeconds, region.ElapsedSeconds + dt);
                        needInvalidate = true;
                    }
                }

                foreach (var line in _lines)
                {
                    if (line.ElapsedSeconds < line.DurationSeconds && globalTime >= line.DelaySeconds)
                    {
                        line.ElapsedSeconds = Math.Min(line.DurationSeconds, line.ElapsedSeconds + dt);
                        needInvalidate = true;
                    }
                }

                var allFillsDone = _fillRegions.All(region => region.ElapsedSeconds >= region.DurationSeconds - 1e-6);
                if (allFillsDone && !_guideFadeStarted)
                {
                    _guideFadeStarted = true;
                    _guideFadeStartTime = globalTime + GuideFadeDelay;
                }

                if (_guideFadeStarted)
                {
                    var fadeT = Clamp01((globalTime - _guideFadeStartTime) / Math.Max(1e-9, GuideFadeDuration));
                    _guideAlphaFactor = Math.Pow(1.0 - fadeT, 2);
                    needInvalidate = true;
                }

                if (FadeStrokesAfterFill && allFillsDone && !_strokeFadeStarted)
                {
                    _strokeFadeStarted = true;
                    _strokeFadeStartTime = globalTime + StrokeFadeDelay;
                }

                if (_strokeFadeStarted)
                {
                    var fadeT = Clamp01((globalTime - _strokeFadeStartTime) / Math.Max(1e-9, StrokeFadeDuration));
                    _strokeAlphaFactor = Math.Pow(1.0 - fadeT, 2);
                    needInvalidate = true;
                }

                var anyActive =
                    _lines.Any(line => line.ElapsedSeconds < line.DurationSeconds) ||
                    _fillRegions.Any(region => region.ElapsedSeconds < region.DurationSeconds) ||
                    (FadeStrokesAfterFill && _strokeAlphaFactor > 0.001);

                if (!anyActive)
                {
                    _isRunning = false;
                    _stopwatch.Stop();
                }
            }

            if (needInvalidate)
                InvalidateVisual();

            DetachRenderingIfIdle();
        }

        private void DrawStrokes(DrawingContext dc, double alphaFactor)
        {
            if (_cachedStrokeBrush == null || _cachedTextStrokeBrush == null)
                return;

            var logoAlpha = Clamp01(alphaFactor);
            var textAlpha = 1.0;

            foreach (var line in _lines)
            {
                // LogoOnlyMode keeps the Aarohi wordmark strokes but suppresses the
                // construction strokes around the filled symbol.
                if (LogoOnlyMode && line.Group != StrokeGroup.Text)
                    continue;

                var penBrush = line.Group == StrokeGroup.Text ? _cachedTextStrokeBrush : _cachedStrokeBrush;
                var thickness = Math.Max(0.0, line.Group == StrokeGroup.Text ? TextStrokeThickness : StrokeThickness);
                if (thickness <= 0.0)
                    continue;

                var alpha = line.Group == StrokeGroup.Text ? textAlpha : logoAlpha;

                dc.PushOpacity(alpha);
                var pen = CreatePen(penBrush, thickness);
                var eased = LogoOnlyMode ? 1.0 : ApplyEasing(line.Progress);
                var (left, right) = line.GetPointsFromCenter(eased);
                var center = MapToScreen(line.GetWorldCenter());

                dc.DrawLine(pen, center, MapToScreen(left));
                dc.DrawLine(pen, center, MapToScreen(right));
                dc.Pop();
            }
        }

        private void DrawFillRegions(DrawingContext dc)
        {
            foreach (var region in _fillRegions)
                DrawFillRegion(dc, region);
        }

        private void DrawFillRegion(DrawingContext dc, FillRegion region)
        {
            if (region.PolygonWorld.Length < 3)
                return;

            var rawT = LogoOnlyMode ? 1.0 : region.Progress;
            var easedT = GetEasedForRegion(region, rawT);
            var points = region.PolygonWorld.Select(MapToScreen).ToArray();
            var polygon = CreatePolygonGeometry(points);
            var centroid = MapToScreen(region.GetWorldCentroid());

            if (LogoOnlyMode)
            {
                dc.DrawGeometry(CreateFrozenBrush(region.FillColor), null, polygon);
                return;
            }

            var minX = points.Min(point => point.X);
            var maxX = points.Max(point => point.X);
            var minY = points.Min(point => point.Y);
            var maxY = points.Max(point => point.Y);
            var width = Math.Max(1.0, maxX - minX);
            var height = Math.Max(1.0, maxY - minY);

            Geometry revealGeometry;
            if (FillRevealMode == RevealMode.Radial)
            {
                var maxRadius = points.Max(point =>
                {
                    var dx = point.X - centroid.X;
                    var dy = point.Y - centroid.Y;
                    return Math.Sqrt(dx * dx + dy * dy);
                });
                var radius = Math.Max(2.0, easedT * maxRadius);
                revealGeometry = new EllipseGeometry(centroid, radius, radius);
            }
            else if (FillRevealMode == RevealMode.Vertical)
            {
                var h = Math.Max(2.0, easedT * height);
                revealGeometry = new RectangleGeometry(new Rect(minX - 1.0, maxY - h, width + 2.0, h + 2.0));
            }
            else
            {
                var w = Math.Max(2.0, easedT * width);
                revealGeometry = new RectangleGeometry(new Rect(minX, minY - 1.0, w + 2.0, height + 2.0));
            }

            var fillBrush = CreateFrozenBrush(region.FillColor, Clamp01(rawT * 1.1));

            dc.PushClip(polygon);
            dc.DrawGeometry(fillBrush, null, revealGeometry);
            dc.Pop();

            if (region.Progress < 1.0 - 1e-6)
                return;

            var fullBrush = CreateFrozenBrush(region.FillColor);
            var borderColor = Color.FromArgb(
                200,
                (byte)Math.Max(0, region.FillColor.R - 20),
                (byte)Math.Max(0, region.FillColor.G - 20),
                (byte)Math.Max(0, region.FillColor.B - 20));
            var borderPen = CreatePen(CreateFrozenBrush(borderColor), 2.0);
            dc.DrawGeometry(fullBrush, borderPen, polygon);
        }

        private void DrawReflection(DrawingContext dc)
        {
            if (_fillRegions.Count == 0)
                return;

            var geometries = _fillRegions
                .Where(region => region.PolygonWorld.Length >= 3)
                .Select(region => CreatePolygonGeometry(region.PolygonWorld.Select(MapToScreen).ToArray()))
                .ToList();

            if (geometries.Count == 0)
                return;

            var combined = new GeometryGroup { FillRule = FillRule.Nonzero };
            foreach (var geometry in geometries)
                combined.Children.Add(geometry);

            var bounds = combined.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var t = (DateTime.UtcNow.TimeOfDay.TotalSeconds * ReflectionSpeed) % 1.0;
            var stripThickness = Math.Max(2.0, ReflectionThickness * bounds.Width);
            var stripLength = Math.Max(bounds.Height * 1.4, bounds.Height + 10.0);
            var startX = bounds.Left - stripThickness * 1.5;
            var endX = bounds.Right + stripThickness * 1.5;
            var centerX = startX + (endX - startX) * t;
            var centerY = bounds.Top + bounds.Height * 0.5;
            var rect = new Rect(centerX - stripThickness / 2.0, centerY - stripLength / 2.0, stripThickness, stripLength);

            var centerAlpha = Clamp01(ReflectionIntensity);
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            };
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * centerAlpha * 0.25), 255, 255, 255), 0.35));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * centerAlpha), 255, 255, 255), 0.5));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * centerAlpha * 0.25), 255, 255, 255), 0.65));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));
            brush.Freeze();

            dc.PushClip(combined);
            dc.PushTransform(new RotateTransform(ReflectionAngle, centerX, centerY));
            dc.DrawRectangle(brush, null, rect);
            dc.Pop();
            dc.Pop();
        }

        private Point MapToScreen(Point worldPoint)
        {
            var cw = ActualWidth - 2.0 * MarginPixels;
            var ch = ActualHeight - 2.0 * MarginPixels;

            if (cw <= 1 || ch <= 1)
                return new Point(MarginPixels, MarginPixels);

            var worldWidth = Math.Max(1e-6, _worldBounds.Width);
            var worldHeight = Math.Max(1e-6, _worldBounds.Height);
            var scale = Math.Min(cw / worldWidth, ch / worldHeight);
            var drawWidth = worldWidth * scale;
            var drawHeight = worldHeight * scale;
            var ox = MarginPixels + (cw - drawWidth) / 2.0;
            var oy = MarginPixels + (ch - drawHeight) / 2.0;

            var x = ox + (worldPoint.X - _worldBounds.Left) * scale;
            var y = oy + (_worldBounds.Bottom - worldPoint.Y) * scale;
            return new Point(x, y);
        }

        private void ComputeWorldBounds()
        {
            var points = new List<Point>();

            foreach (var line in _lines)
            {
                var (start, end) = line.GetWorldEndpoints();
                points.Add(start);
                points.Add(end);
            }

            foreach (var region in _fillRegions)
                points.AddRange(region.PolygonWorld);

            if (points.Count == 0)
            {
                _worldBounds = new Rect(0, 0, 1, 1);
                return;
            }

            var minX = points.Min(point => point.X);
            var maxX = points.Max(point => point.X);
            var minY = points.Min(point => point.Y);
            var maxY = points.Max(point => point.Y);

            if (Math.Abs(maxX - minX) < 1e-6)
                maxX = minX + 1.0;

            if (Math.Abs(maxY - minY) < 1e-6)
                maxY = minY + 1.0;

            _worldBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private double GetEasedForRegion(FillRegion region, double t)
        {
            t = Clamp01(t);

            if (region.CustomEasing != null)
            {
                try
                {
                    return Clamp01(region.CustomEasing(t));
                }
                catch
                {
                    return t;
                }
            }

            return region.EasingTypeOverride.HasValue
                ? ApplyEasingWithType(region.EasingTypeOverride.Value, t)
                : ApplyEasing(t);
        }

        private double ApplyEasing(double t)
        {
            t = Clamp01(t);

            if (_customEasing != null)
            {
                try
                {
                    return Clamp01(_customEasing(t));
                }
                catch
                {
                    return t;
                }
            }

            return ApplyEasingWithType(_easingType, t);
        }

        private static double ApplyEasingWithType(EasingType type, double t)
        {
            t = Clamp01(t);
            return type switch
            {
                EasingType.Linear => t,
                EasingType.EaseIn => t * t,
                EasingType.EaseOut => 1.0 - Math.Pow(1.0 - t, 2),
                EasingType.EaseInOut => t < 0.5
                    ? 2.0 * t * t
                    : 1.0 - Math.Pow(-2.0 * t + 2.0, 2) / 2.0,
                EasingType.SmoothStep => t * t * (3.0 - 2.0 * t),
                _ => t
            };
        }

        private void EnsureRenderingAttached()
        {
            if (_isRenderingAttached || !IsLoaded)
                return;

            CompositionTarget.Rendering += OnRendering;
            _isRenderingAttached = true;
        }

        private void DetachRenderingIfIdle()
        {
            if (_isRunning || EnableReflection)
                return;

            DetachRendering();
        }

        private void DetachRendering()
        {
            if (!_isRenderingAttached)
                return;

            CompositionTarget.Rendering -= OnRendering;
            _isRenderingAttached = false;
        }

        private void EnsureBrushCache()
        {
            _cachedStrokeBrush ??= PrepareBrush(StrokeBrush);
            _cachedTextStrokeBrush ??= PrepareBrush(TextStrokeBrush);
            _cachedGuideBrush ??= PrepareBrush(GuideBrush);
            _cachedBackgroundBrush ??= PrepareBrush(Background);
        }

        private static Pen CreatePen(Brush brush, double thickness)
        {
            var pen = new Pen(brush, thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            if (pen.CanFreeze)
                pen.Freeze();
            return pen;
        }

        private static StreamGeometry CreatePolygonGeometry(IReadOnlyList<Point> points)
        {
            var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
            using (var context = geometry.Open())
            {
                context.BeginFigure(points[0], true, true);
                for (var i = 1; i < points.Count; i++)
                    context.LineTo(points[i], true, false);
            }

            geometry.Freeze();
            return geometry;
        }

        private static Brush PrepareBrush(Brush? source)
        {
            if (source == null)
                return Brushes.Transparent;

            if (source.IsFrozen)
                return source;

            var clone = source.CloneCurrentValue();
            if (clone.CanFreeze)
                clone.Freeze();
            return clone;
        }

        private static SolidColorBrush CreateFrozenBrush(Color color, double opacity = 1.0)
        {
            color.A = (byte)(255 * Clamp01(opacity));
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static double Clamp01(double value)
        {
            if (double.IsNaN(value))
                return 0.0;
            if (value < 0.0)
                return 0.0;
            if (value > 1.0)
                return 1.0;
            return value;
        }

        private static double Clamp(double min, double max, double value)
            => Math.Max(min, Math.Min(max, value));

        private static object CoerceTargetFramesPerSecond(DependencyObject d, object value)
        {
            var fps = (int)value;
            if (fps < 15)
                return 15;
            if (fps > 120)
                return 120;
            return fps;
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AarohiLoadder loader)
                loader.InvalidateVisual();
        }

        private static void OnBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not AarohiLoadder loader)
                return;

            loader._cachedStrokeBrush = null;
            loader._cachedTextStrokeBrush = null;
            loader._cachedGuideBrush = null;
            loader._cachedBackgroundBrush = null;
            loader.InvalidateVisual();
        }

        private static void OnReflectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not AarohiLoadder loader)
                return;

            if (loader.LogoOnlyMode && (bool)e.NewValue)
            {
                loader.EnableReflection = false;
                return;
            }

            if ((bool)e.NewValue)
            {
                if (!loader._stopwatch.IsRunning)
                {
                    loader._stopwatch.Start();
                    loader._lastElapsedMs = loader._stopwatch.ElapsedMilliseconds;
                    loader._lastFrameSeconds = loader._stopwatch.Elapsed.TotalSeconds;
                }
                loader.EnsureRenderingAttached();
            }
            else
            {
                if (!loader._isRunning)
                    loader._stopwatch.Stop();
                loader.DetachRenderingIfIdle();
            }

            loader.InvalidateVisual();
        }

        private static void OnLogoOnlyModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AarohiLoadder loader && (bool)e.NewValue)
            {
                loader.StopAnimation();
                loader.StopReflection();
                loader.InvalidateVisual();
            }
        }

        public class Line
        {
            public Line(double slope, double intercept, double yMin, double yMax)
            {
                Slope = slope;
                Intercept = intercept;
                YMin = yMin;
                YMax = yMax;
                IsHorizontal = false;
            }

            public Line(double intercept, double xMin, double xMax)
            {
                Intercept = intercept;
                XMin = xMin;
                XMax = xMax;
                IsHorizontal = true;
            }

            public double? Slope { get; set; }
            public double? Intercept { get; set; }
            public double? YMin { get; set; }
            public double? YMax { get; set; }
            public double? XMin { get; set; }
            public double? XMax { get; set; }
            public bool IsHorizontal { get; set; }
            public double DelaySeconds { get; set; }
            public double DurationSeconds { get; set; } = 1.0;
            public double ElapsedSeconds { get; set; }
            public double Progress => DurationSeconds <= 0
                ? 1.0
                : Math.Min(1.0, ElapsedSeconds / DurationSeconds);
            public StrokeGroup Group { get; set; } = StrokeGroup.Logo;

            public (Point start, Point end) GetWorldEndpoints()
            {
                if (IsHorizontal)
                {
                    var y = Intercept.GetValueOrDefault();
                    return (new Point(XMin.GetValueOrDefault(), y), new Point(XMax.GetValueOrDefault(), y));
                }

                var y1 = YMin.GetValueOrDefault();
                var y2 = YMax.GetValueOrDefault();
                var m = Slope.GetValueOrDefault();
                var c = Intercept.GetValueOrDefault();
                return (new Point(m * y1 + c, y1), new Point(m * y2 + c, y2));
            }

            public Point GetWorldCenter()
            {
                var (start, end) = GetWorldEndpoints();
                return new Point((start.X + end.X) / 2.0, (start.Y + end.Y) / 2.0);
            }

            public (Point left, Point right) GetPointsFromCenter(double t)
            {
                var (start, end) = GetWorldEndpoints();
                var center = GetWorldCenter();

                var left = new Point(
                    center.X + (start.X - center.X) * t,
                    center.Y + (start.Y - center.Y) * t);
                var right = new Point(
                    center.X + (end.X - center.X) * t,
                    center.Y + (end.Y - center.Y) * t);

                return (left, right);
            }
        }

        public class FillRegion
        {
            public FillRegion(Point[] polygonWorld, Color color)
            {
                PolygonWorld = polygonWorld ?? Array.Empty<Point>();
                FillColor = color;
            }

            public Point[] PolygonWorld { get; set; }
            public Color FillColor { get; set; }
            public double DelaySeconds { get; set; }
            public double DurationSeconds { get; set; } = 1.0;
            public double ElapsedSeconds { get; set; }
            public double Progress => DurationSeconds <= 0
                ? 1.0
                : Math.Min(1.0, ElapsedSeconds / DurationSeconds);
            public EasingType? EasingTypeOverride { get; set; }
            public Func<double, double>? CustomEasing { get; set; }

            public Point GetWorldCentroid()
            {
                var points = PolygonWorld;
                var count = points.Length;
                if (count == 0)
                    return new Point(0, 0);

                double area = 0;
                double cx = 0;
                double cy = 0;

                for (var i = 0; i < count; i++)
                {
                    var p0 = points[i];
                    var p1 = points[(i + 1) % count];
                    var cross = p0.X * p1.Y - p1.X * p0.Y;
                    area += cross;
                    cx += (p0.X + p1.X) * cross;
                    cy += (p0.Y + p1.Y) * cross;
                }

                area *= 0.5;
                if (Math.Abs(area) < 1e-9)
                {
                    var sx = points.Sum(point => point.X);
                    var sy = points.Sum(point => point.Y);
                    return new Point(sx / count, sy / count);
                }

                cx /= 6.0 * area;
                cy /= 6.0 * area;
                return new Point(cx, cy);
            }
        }
    }
}
