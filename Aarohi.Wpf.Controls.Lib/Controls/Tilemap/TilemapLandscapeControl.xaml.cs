using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Aarohi.Wpf.Controls.Lib.Controls.Tilemap;

public partial class TilemapLandscapeControl : UserControl
{
    // Original HTML constants.
    private const double SceneSize = 1600.0;
    private const double Perspective = 900.0;
    private bool _hasRendered;
    private readonly Stopwatch _renderAnimationClock = new();
    private List<TileVisualInfo> _animatedTiles = new();
    private double _maxRevealLine = 1.0;

    public TilemapLandscapeControl()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty SceneTiltProperty = DependencyProperty.Register(
        nameof(SceneTilt),
        typeof(double),
        typeof(TilemapLandscapeControl),
        new PropertyMetadata(68.0, OnRenderPropertyChanged));

    /// <summary>
    /// Backend/XAML controlled tilt angle. Same purpose as CSS --scene-tilt.
    /// Example: tilemap.SceneTilt = 68;
    /// </summary>
    public double SceneTilt
    {
        get => (double)GetValue(SceneTiltProperty);
        set => SetValue(SceneTiltProperty, value);
    }

    public static readonly DependencyProperty FarFadePercentProperty = DependencyProperty.Register(
        nameof(FarFadePercent),
        typeof(double),
        typeof(TilemapLandscapeControl),
        new PropertyMetadata(72.0, OnRenderPropertyChanged));

    public double FarFadePercent
    {
        get => (double)GetValue(FarFadePercentProperty);
        set => SetValue(FarFadePercentProperty, value);
    }

    public static readonly DependencyProperty TileSizeProperty = DependencyProperty.Register(
        nameof(TileSize),
        typeof(double),
        typeof(TilemapLandscapeControl),
        new PropertyMetadata(74.0, OnRenderPropertyChanged));

    public double TileSize
    {
        get => (double)GetValue(TileSizeProperty);
        set => SetValue(TileSizeProperty, value);
    }

    public static readonly DependencyProperty TileGapProperty = DependencyProperty.Register(
        nameof(TileGap),
        typeof(double),
        typeof(TilemapLandscapeControl),
        new PropertyMetadata(3.0, OnRenderPropertyChanged));

    public double TileGap
    {
        get => (double)GetValue(TileGapProperty);
        set => SetValue(TileGapProperty, value);
    }

    public static readonly DependencyProperty TileCornerRadiusProperty = DependencyProperty.Register(
        nameof(TileCornerRadius),
        typeof(double),
        typeof(TilemapLandscapeControl),
        new PropertyMetadata(14.0, OnRenderPropertyChanged));

    public double TileCornerRadius
    {
        get => (double)GetValue(TileCornerRadiusProperty);
        set => SetValue(TileCornerRadiusProperty, value);
    }

    public static readonly DependencyProperty GridRowsProperty = DependencyProperty.Register(
        nameof(GridRows),
        typeof(int),
        typeof(TilemapLandscapeControl),
        new PropertyMetadata(18, OnRenderPropertyChanged, CoerceGridSize));

    public int GridRows
    {
        get => (int)GetValue(GridRowsProperty);
        set => SetValue(GridRowsProperty, value);
    }

    public static readonly DependencyProperty GridColumnsProperty = DependencyProperty.Register(
        nameof(GridColumns),
        typeof(int),
        typeof(TilemapLandscapeControl),
        new PropertyMetadata(18, OnRenderPropertyChanged, CoerceGridSize));

    public int GridColumns
    {
        get => (int)GetValue(GridColumnsProperty);
        set => SetValue(GridColumnsProperty, value);
    }

    public static readonly DependencyProperty PlaneDistanceXProperty = DependencyProperty.Register(
        nameof(PlaneDistanceX),
        typeof(double),
        typeof(TilemapLandscapeControl),
        new PropertyMetadata(0.0, OnRenderPropertyChanged));

    public double PlaneDistanceX
    {
        get => (double)GetValue(PlaneDistanceXProperty);
        set => SetValue(PlaneDistanceXProperty, value);
    }

    public static readonly DependencyProperty PlaneDistanceYProperty = DependencyProperty.Register(
        nameof(PlaneDistanceY),
        typeof(double),
        typeof(TilemapLandscapeControl),
        new PropertyMetadata(0.0, OnRenderPropertyChanged));

    public double PlaneDistanceY
    {
        get => (double)GetValue(PlaneDistanceYProperty);
        set => SetValue(PlaneDistanceYProperty, value);
    }

    public static readonly DependencyProperty PlaneDistanceZProperty = DependencyProperty.Register(
        nameof(PlaneDistanceZ),
        typeof(double),
        typeof(TilemapLandscapeControl),
        new PropertyMetadata(0.0, OnRenderPropertyChanged));

    public double PlaneDistanceZ
    {
        get => (double)GetValue(PlaneDistanceZProperty);
        set => SetValue(PlaneDistanceZProperty, value);
    }

    public static readonly DependencyProperty EnableRenderAnimationProperty = DependencyProperty.Register(
        nameof(EnableRenderAnimation),
        typeof(bool),
        typeof(TilemapLandscapeControl),
        new PropertyMetadata(true));

    public bool EnableRenderAnimation
    {
        get => (bool)GetValue(EnableRenderAnimationProperty);
        set => SetValue(EnableRenderAnimationProperty, value);
    }

    public static readonly DependencyProperty RenderAnimationDurationProperty = DependencyProperty.Register(
        nameof(RenderAnimationDuration),
        typeof(double),
        typeof(TilemapLandscapeControl),
        new PropertyMetadata(1.65));

    public double RenderAnimationDuration
    {
        get => (double)GetValue(RenderAnimationDurationProperty);
        set => SetValue(RenderAnimationDurationProperty, value);
    }

    public static readonly DependencyProperty RenderAnimationFallHeightProperty = DependencyProperty.Register(
        nameof(RenderAnimationFallHeight),
        typeof(double),
        typeof(TilemapLandscapeControl),
        new PropertyMetadata(24.0));

    public double RenderAnimationFallHeight
    {
        get => (double)GetValue(RenderAnimationFallHeightProperty);
        set => SetValue(RenderAnimationFallHeightProperty, value);
    }

    public static readonly DependencyProperty BackgroundTopColorProperty = RegisterColorProperty(
        nameof(BackgroundTopColor),
        Color.FromRgb(236, 253, 245));

    public Color BackgroundTopColor
    {
        get => (Color)GetValue(BackgroundTopColorProperty);
        set => SetValue(BackgroundTopColorProperty, value);
    }

    public static readonly DependencyProperty BackgroundBottomColorProperty = RegisterColorProperty(
        nameof(BackgroundBottomColor),
        Color.FromRgb(191, 219, 254));

    public Color BackgroundBottomColor
    {
        get => (Color)GetValue(BackgroundBottomColorProperty);
        set => SetValue(BackgroundBottomColorProperty, value);
    }

    public static readonly DependencyProperty TopGlowInnerColorProperty = RegisterColorProperty(
        nameof(TopGlowInnerColor),
        Color.FromArgb(210, 255, 255, 255));

    public Color TopGlowInnerColor
    {
        get => (Color)GetValue(TopGlowInnerColorProperty);
        set => SetValue(TopGlowInnerColorProperty, value);
    }

    public static readonly DependencyProperty TopGlowOuterColorProperty = RegisterColorProperty(
        nameof(TopGlowOuterColor),
        Color.FromArgb(0, 255, 255, 255));

    public Color TopGlowOuterColor
    {
        get => (Color)GetValue(TopGlowOuterColorProperty);
        set => SetValue(TopGlowOuterColorProperty, value);
    }

    public static readonly DependencyProperty GroundGlowInnerColorProperty = RegisterColorProperty(
        nameof(GroundGlowInnerColor),
        Color.FromArgb(105, 45, 212, 191));

    public Color GroundGlowInnerColor
    {
        get => (Color)GetValue(GroundGlowInnerColorProperty);
        set => SetValue(GroundGlowInnerColorProperty, value);
    }

    public static readonly DependencyProperty GroundGlowOuterColorProperty = RegisterColorProperty(
        nameof(GroundGlowOuterColor),
        Color.FromArgb(0, 45, 212, 191));

    public Color GroundGlowOuterColor
    {
        get => (Color)GetValue(GroundGlowOuterColorProperty);
        set => SetValue(GroundGlowOuterColorProperty, value);
    }

    public static readonly DependencyProperty HorizonFadeStrongColorProperty = RegisterColorProperty(
        nameof(HorizonFadeStrongColor),
        Color.FromArgb(235, 236, 253, 245));

    public Color HorizonFadeStrongColor
    {
        get => (Color)GetValue(HorizonFadeStrongColorProperty);
        set => SetValue(HorizonFadeStrongColorProperty, value);
    }

    public static readonly DependencyProperty HorizonFadeMidColorProperty = RegisterColorProperty(
        nameof(HorizonFadeMidColor),
        Color.FromArgb(190, 219, 234, 254));

    public Color HorizonFadeMidColor
    {
        get => (Color)GetValue(HorizonFadeMidColorProperty);
        set => SetValue(HorizonFadeMidColorProperty, value);
    }

    public static readonly DependencyProperty HorizonFadeSoftColorProperty = RegisterColorProperty(
        nameof(HorizonFadeSoftColor),
        Color.FromArgb(62, 191, 219, 254));

    public Color HorizonFadeSoftColor
    {
        get => (Color)GetValue(HorizonFadeSoftColorProperty);
        set => SetValue(HorizonFadeSoftColorProperty, value);
    }

    public static readonly DependencyProperty HorizonFadeClearColorProperty = RegisterColorProperty(
        nameof(HorizonFadeClearColor),
        Color.FromArgb(0, 191, 219, 254));

    public Color HorizonFadeClearColor
    {
        get => (Color)GetValue(HorizonFadeClearColorProperty);
        set => SetValue(HorizonFadeClearColorProperty, value);
    }

    public static readonly DependencyProperty TileTopNearColorProperty = RegisterColorProperty(
        nameof(TileTopNearColor),
        Color.FromRgb(255, 255, 255));

    public Color TileTopNearColor
    {
        get => (Color)GetValue(TileTopNearColorProperty);
        set => SetValue(TileTopNearColorProperty, value);
    }

    public static readonly DependencyProperty TileTopFarColorProperty = RegisterColorProperty(
        nameof(TileTopFarColor),
        Color.FromRgb(204, 251, 241));

    public Color TileTopFarColor
    {
        get => (Color)GetValue(TileTopFarColorProperty);
        set => SetValue(TileTopFarColorProperty, value);
    }

    public static readonly DependencyProperty TileBottomNearColorProperty = RegisterColorProperty(
        nameof(TileBottomNearColor),
        Color.FromRgb(219, 234, 254));

    public Color TileBottomNearColor
    {
        get => (Color)GetValue(TileBottomNearColorProperty);
        set => SetValue(TileBottomNearColorProperty, value);
    }

    public static readonly DependencyProperty TileBottomFarColorProperty = RegisterColorProperty(
        nameof(TileBottomFarColor),
        Color.FromRgb(125, 211, 252));

    public Color TileBottomFarColor
    {
        get => (Color)GetValue(TileBottomFarColorProperty);
        set => SetValue(TileBottomFarColorProperty, value);
    }

    public static readonly DependencyProperty TileStrokeColorProperty = RegisterColorProperty(
        nameof(TileStrokeColor),
        Color.FromRgb(255, 255, 255));

    public Color TileStrokeColor
    {
        get => (Color)GetValue(TileStrokeColorProperty);
        set => SetValue(TileStrokeColorProperty, value);
    }

    public static readonly DependencyProperty TileInnerStrokeColorProperty = RegisterColorProperty(
        nameof(TileInnerStrokeColor),
        Color.FromRgb(240, 253, 250));

    public Color TileInnerStrokeColor
    {
        get => (Color)GetValue(TileInnerStrokeColorProperty);
        set => SetValue(TileInnerStrokeColorProperty, value);
    }

    public static readonly DependencyProperty ShadowColorProperty = RegisterColorProperty(
        nameof(ShadowColor),
        Color.FromRgb(15, 118, 110));

    public Color ShadowColor
    {
        get => (Color)GetValue(ShadowColorProperty);
        set => SetValue(ShadowColorProperty, value);
    }

    private static DependencyProperty RegisterColorProperty(string propertyName, Color defaultColor)
    {
        return DependencyProperty.Register(
            propertyName,
            typeof(Color),
            typeof(TilemapLandscapeControl),
            new PropertyMetadata(defaultColor, OnVisualColorPropertyChanged));
    }

    private static void OnVisualColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TilemapLandscapeControl control)
        {
            return;
        }

        control.ApplyTheme();

        if (control.IsLoaded)
        {
            control.RenderOnce(animateReveal: false);
        }
    }

    private static void OnRenderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TilemapLandscapeControl control && control.IsLoaded)
        {
            control.RenderOnce(animateReveal: false);
        }
    }

    private static object CoerceGridSize(DependencyObject d, object baseValue)
    {
        return Math.Max(1, (int)baseValue);
    }

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme();
        RenderOnce(animateReveal: EnableRenderAnimation);
    }

    private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsLoaded)
        {
            RenderOnce(animateReveal: !_hasRendered && EnableRenderAnimation);
        }
    }

    private void Root_Unloaded(object sender, RoutedEventArgs e)
    {
        StopRenderAnimation();
    }

    /// <summary>
    /// Renders the landscape and optionally plays a camera-centered radial reveal.
    /// It redraws when size / tilt / fade / tile settings change.
    /// </summary>
    public void RenderOnce()
    {
        RenderOnce(animateReveal: EnableRenderAnimation);
    }

    public void ReplayRenderAnimation()
    {
        RenderOnce(animateReveal: EnableRenderAnimation);
    }

    private void RenderOnce(bool animateReveal)
    {
        if (ActualWidth <= 10 || ActualHeight <= 10)
            return;

        StopRenderAnimation();
        TileCanvas.Children.Clear();
        ThemePalette palette = GetThemePalette();
        int rows = Math.Max(1, GridRows);
        int columns = Math.Max(1, GridColumns);

        double step = TileSize + TileGap;
        double centerOffsetX = (SceneSize / 2.0) - (columns * step) / 2.0;
        double centerOffsetY = (SceneSize / 2.0) - (rows * step) / 2.0;
        double fadePower = Math.Clamp(FarFadePercent / 100.0, 0.0, 1.0);

        double viewportCenterX = ActualWidth / 2.0;
        double viewportCenterY = ActualHeight * 0.58;

        List<TileVisualInfo> tileInfos = new();

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                double nearDistance = rows <= 1 ? 1.0 : y / (double)(rows - 1);      // front side
                double farDistance = 1.0 - nearDistance;           // horizon side
                double scale = 0.78 + nearDistance * 0.24;         // same original scale

                double tileLeft = centerOffsetX + x * step;
                double tileTop = centerOffsetY + y * step;

                double centerSceneX = tileLeft + TileSize / 2.0 + PlaneDistanceX;
                double centerSceneY = tileTop + TileSize / 2.0 + PlaneDistanceY;

                double z = PlaneDistanceZ;

                var outer = CreateProjectedRoundedRectangle(
                    centerSceneX,
                    centerSceneY,
                    TileSize * scale,
                    TileSize * scale,
                    TileCornerRadius * scale,
                    z,
                    viewportCenterX,
                    viewportCenterY);

                var inner = CreateProjectedRoundedRectangle(
                    centerSceneX,
                    centerSceneY,
                    Math.Max(1, (TileSize - 14) * scale),
                    Math.Max(1, (TileSize - 14) * scale),
                    Math.Max(1, (TileCornerRadius - 4) * scale),
                    z + 0.1,
                    viewportCenterX,
                    viewportCenterY);

                Point projectedCenter = ProjectPoint(
                    centerSceneX,
                    centerSceneY,
                    z + 0.1,
                    viewportCenterX,
                    viewportCenterY);

                double sideDistance = columns <= 1 ? 0.0 : Math.Abs(x - columns / 2.0) / (columns / 2.0);
                double opacity = Math.Max(0.05, 1.0 - farDistance * fadePower - sideDistance * 0.06);

                byte alphaTop = ToByte(255.0 * (0.99 - farDistance * 0.34));
                byte alphaBottom = ToByte(255.0 * (0.76 - farDistance * 0.40));
                Color tileTopColor = WithAlpha(Interpolate(palette.TileTopNear, palette.TileTopFar, farDistance), alphaTop);
                Color tileBottomColor = WithAlpha(Interpolate(palette.TileBottomNear, palette.TileBottomFar, farDistance), alphaBottom);

                var fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };
                fill.GradientStops.Add(new GradientStop(tileTopColor, 0.0));
                fill.GradientStops.Add(new GradientStop(tileBottomColor, 1.0));
                fill.Freeze();

                var path = new Path
                {
                    Data = outer,
                    Fill = fill,
                    Stroke = CreateFrozenBrush(WithAlpha(palette.TileStroke, 155)),
                    StrokeThickness = 1.0,
                    Opacity = opacity,
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = false
                };

                TileTransform outerTransform = PrepareRevealTransform(path, projectedCenter);

                var innerPath = new Path
                {
                    Data = inner,
                    Fill = Brushes.Transparent,
                    Stroke = CreateFrozenBrush(WithAlpha(palette.TileInnerStroke, 72)),
                    StrokeThickness = 0.8,
                    Opacity = opacity * 0.85,
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = false
                };
                TileTransform innerTransform = PrepareRevealTransform(innerPath, projectedCenter);

                // Front rows are closest to the viewer, so they reveal first.
                int revealLine = rows - 1 - y;
                tileInfos.Add(new TileVisualInfo(
                    y,
                    revealLine,
                    opacity,
                    nearDistance > 0.35,
                    path,
                    innerPath,
                    outerTransform,
                    innerTransform));
            }
        }

        tileInfos.Sort((a, b) => a.Row.CompareTo(b.Row));

        foreach (TileVisualInfo info in tileInfos)
        {
            TileCanvas.Children.Add(info.OuterPath);
            TileCanvas.Children.Add(info.InnerPath);
        }

        if (animateReveal)
        {
            StartRenderAnimation(tileInfos, rows);
        }
        else
        {
            foreach (TileVisualInfo info in tileInfos)
            {
                SetTileAnimationState(info, 1.0, fallOffset: 0.0);
                ApplyDepthEffect(info, GetThemePalette());
            }
        }

        _hasRendered = true;
    }

    private void StartRenderAnimation(List<TileVisualInfo> tileInfos, int rows)
    {
        _animatedTiles = tileInfos;
        _maxRevealLine = Math.Max(1.0, rows - 1);

        double fallHeight = Math.Max(0.0, RenderAnimationFallHeight);
        foreach (TileVisualInfo info in _animatedTiles)
        {
            SetTileAnimationState(info, 0.0, -fallHeight);
            info.OuterPath.Effect = null;
        }

        _renderAnimationClock.Restart();
        CompositionTarget.Rendering += OnRenderAnimationFrame;
    }

    private void StopRenderAnimation()
    {
        CompositionTarget.Rendering -= OnRenderAnimationFrame;
        _renderAnimationClock.Reset();
        _animatedTiles.Clear();
    }

    private void OnRenderAnimationFrame(object? sender, EventArgs e)
    {
        double totalDuration = Math.Max(0.2, RenderAnimationDuration);
        double tileDuration = Math.Max(0.12, totalDuration * 0.34);
        double spreadDuration = Math.Max(0.01, totalDuration - tileDuration);
        double elapsed = _renderAnimationClock.Elapsed.TotalSeconds;
        double fallHeight = Math.Max(0.0, RenderAnimationFallHeight);
        bool complete = true;

        foreach (TileVisualInfo info in _animatedTiles)
        {
            double normalizedLine = Math.Clamp(info.RevealLine / _maxRevealLine, 0.0, 1.0);
            double startTime = normalizedLine * spreadDuration;
            double rawProgress = (elapsed - startTime) / tileDuration;
            double progress = Math.Clamp(rawProgress, 0.0, 1.0);
            double eased = EaseOutCubic(progress);
            double fallOffset = -fallHeight * (1.0 - EaseOutBack(progress));

            SetTileAnimationState(info, eased, fallOffset);

            if (rawProgress < 1.0)
            {
                complete = false;
            }
        }

        if (!complete)
        {
            return;
        }

        CompositionTarget.Rendering -= OnRenderAnimationFrame;
        _renderAnimationClock.Reset();

        foreach (TileVisualInfo info in _animatedTiles)
        {
            SetTileAnimationState(info, 1.0, fallOffset: 0.0);
            ApplyDepthEffect(info, GetThemePalette());
        }

        _animatedTiles.Clear();
    }

    private static void SetTileAnimationState(TileVisualInfo info, double progress, double fallOffset)
    {
        double scale = 0.965 + 0.035 * progress;
        double outerOpacity = info.TargetOpacity * progress;
        double innerOpacity = info.TargetOpacity * 0.85 * progress;

        info.OuterPath.Opacity = outerOpacity;
        info.InnerPath.Opacity = innerOpacity;

        ApplyTransformState(info.OuterTransform, scale, fallOffset);
        ApplyTransformState(info.InnerTransform, scale, fallOffset);
    }

    private static void ApplyTransformState(TileTransform transform, double scale, double yOffset)
    {
        transform.Scale.ScaleX = scale;
        transform.Scale.ScaleY = scale;
        transform.Translate.Y = yOffset;
    }

    private static void ApplyDepthEffect(TileVisualInfo info, ThemePalette palette)
    {
        if (!info.HasDepthShadow || info.OuterPath.Effect is not null)
        {
            return;
        }

        info.OuterPath.Effect = new DropShadowEffect
        {
            BlurRadius = 14,
            ShadowDepth = 4,
            Direction = 270,
            Opacity = 0.055,
            Color = palette.Shadow
        };
    }

    private void ApplyTheme()
    {
        ThemePalette palette = GetThemePalette();

        BackgroundTopStop.Color = palette.BackgroundTop;
        BackgroundBottomStop.Color = palette.BackgroundBottom;
        TopGlowInnerStop.Color = palette.TopGlowInner;
        TopGlowOuterStop.Color = palette.TopGlowOuter;
        HorizonFadeStrongStop.Color = palette.HorizonFadeStrong;
        HorizonFadeMidStop.Color = palette.HorizonFadeMid;
        HorizonFadeSoftStop.Color = palette.HorizonFadeSoft;
        HorizonFadeClearStop.Color = palette.HorizonFadeClear;
    }

    private ThemePalette GetThemePalette()
    {
        return new ThemePalette(
            BackgroundTopColor,
            BackgroundBottomColor,
            TopGlowInnerColor,
            TopGlowOuterColor,
            GroundGlowInnerColor,
            GroundGlowOuterColor,
            HorizonFadeStrongColor,
            HorizonFadeMidColor,
            HorizonFadeSoftColor,
            HorizonFadeClearColor,
            TileTopNearColor,
            TileTopFarColor,
            TileBottomNearColor,
            TileBottomFarColor,
            TileStrokeColor,
            TileInnerStrokeColor,
            ShadowColor);
    }

    private static TileTransform PrepareRevealTransform(Path path, Point projectedCenter)
    {
        path.RenderTransformOrigin = new Point(0, 0);
        var scale = new ScaleTransform(1.0, 1.0, projectedCenter.X, projectedCenter.Y);
        var translate = new TranslateTransform();
        path.RenderTransform = new TransformGroup
        {
            Children =
            {
                scale,
                translate
            }
        };

        return new TileTransform(scale, translate);
    }

    private StreamGeometry CreateProjectedRoundedRectangle(
        double centerSceneX,
        double centerSceneY,
        double width,
        double height,
        double radius,
        double z,
        double viewportCenterX,
        double viewportCenterY)
    {
        double halfW = width / 2.0;
        double halfH = height / 2.0;
        radius = Math.Clamp(radius, 0, Math.Min(halfW, halfH));

        List<Point> localPoints = BuildRoundedRectanglePoints(halfW, halfH, radius, segmentsPerCorner: 5);

        var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };

        using (StreamGeometryContext ctx = geometry.Open())
        {
            bool first = true;
            foreach (Point p in localPoints)
            {
                Point projected = ProjectPoint(
                    centerSceneX + p.X,
                    centerSceneY + p.Y,
                    z,
                    viewportCenterX,
                    viewportCenterY);

                if (first)
                {
                    ctx.BeginFigure(projected, true, true);
                    first = false;
                }
                else
                {
                    ctx.LineTo(projected, true, false);
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static List<Point> BuildRoundedRectanglePoints(double halfW, double halfH, double radius, int segmentsPerCorner)
    {
        var points = new List<Point>();

        // Start from top-right corner and move clockwise.
        AddCorner(points, halfW - radius, -halfH + radius, radius, -90, 0, segmentsPerCorner);
        AddCorner(points, halfW - radius, halfH - radius, radius, 0, 90, segmentsPerCorner);
        AddCorner(points, -halfW + radius, halfH - radius, radius, 90, 180, segmentsPerCorner);
        AddCorner(points, -halfW + radius, -halfH + radius, radius, 180, 270, segmentsPerCorner);

        return points;
    }

    private static void AddCorner(List<Point> points, double cx, double cy, double r, double startDeg, double endDeg, int segments)
    {
        for (int i = 0; i <= segments; i++)
        {
            double t = i / (double)segments;
            double deg = startDeg + (endDeg - startDeg) * t;
            double rad = deg * Math.PI / 180.0;
            points.Add(new Point(cx + Math.Cos(rad) * r, cy + Math.Sin(rad) * r));
        }
    }

    private Point ProjectPoint(double sceneX, double sceneY, double z, double viewportCenterX, double viewportCenterY)
    {
        // CSS transform-origin is center of scene-wrap.
        double localX = sceneX - SceneSize / 2.0;
        double localY = sceneY - SceneSize / 2.0;

        double angle = SceneTilt * Math.PI / 180.0;
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);

        // Equivalent visual model for rotateX(sceneTilt) with perspective on parent.
        double rotatedY = localY * cos - z * sin;
        double rotatedZ = localY * sin + z * cos;

        double perspectiveScale = Perspective / Math.Max(1.0, Perspective - rotatedZ);

        double screenX = viewportCenterX + localX * perspectiveScale;
        double screenY = viewportCenterY + rotatedY * perspectiveScale;

        return new Point(screenX, screenY);
    }

    private static byte ToByte(double value)
    {
        return (byte)Math.Clamp(Math.Round(value), 0, 255);
    }

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static Color Interpolate(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0.0, 1.0);
        return Color.FromRgb(
            ToByte(from.R + (to.R - from.R) * amount),
            ToByte(from.G + (to.G - from.G) * amount),
            ToByte(from.B + (to.B - from.B) * amount));
    }

    private static double EaseOutCubic(double value)
    {
        double inverse = 1.0 - value;
        return 1.0 - inverse * inverse * inverse;
    }

    private static double EaseOutBack(double value)
    {
        const double c1 = 1.70158;
        const double c3 = c1 + 1.0;
        double adjusted = value - 1.0;
        return 1.0 + c3 * adjusted * adjusted * adjusted + c1 * adjusted * adjusted;
    }

    private sealed record TileTransform(ScaleTransform Scale, TranslateTransform Translate);

    private sealed record ThemePalette(
        Color BackgroundTop,
        Color BackgroundBottom,
        Color TopGlowInner,
        Color TopGlowOuter,
        Color GroundGlowInner,
        Color GroundGlowOuter,
        Color HorizonFadeStrong,
        Color HorizonFadeMid,
        Color HorizonFadeSoft,
        Color HorizonFadeClear,
        Color TileTopNear,
        Color TileTopFar,
        Color TileBottomNear,
        Color TileBottomFar,
        Color TileStroke,
        Color TileInnerStroke,
        Color Shadow);

    private sealed record TileVisualInfo(
        int Row,
        int RevealLine,
        double TargetOpacity,
        bool HasDepthShadow,
        Path OuterPath,
        Path InnerPath,
        TileTransform OuterTransform,
        TileTransform InnerTransform);
}
