using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Aarohi.Wpf.Controls.Lib.Controls.OutlinedText
{
    /// <summary>
    /// Advanced WPF control rendering bold outlined text with customizable liquid wave dynamics,
    /// buoyant rising bubble particles, corner progress percentage badge, and smooth
    /// backspace -> property swap -> typewriter text transition animations.
    /// </summary>
    public class OutlinedTextBlock : FrameworkElement
    {
        #region Dependency Properties - Text & Font

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryInvalidated));

        public static readonly DependencyProperty FontFamilyProperty =
            DependencyProperty.Register(
                nameof(FontFamily),
                typeof(FontFamily),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryInvalidated));

        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register(
                nameof(FontSize),
                typeof(double),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(72.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryInvalidated));

        public static readonly DependencyProperty FontWeightProperty =
            DependencyProperty.Register(
                nameof(FontWeight),
                typeof(FontWeight),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(FontWeights.Bold, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryInvalidated));

        public static readonly DependencyProperty FontStyleProperty =
            DependencyProperty.Register(
                nameof(FontStyle),
                typeof(FontStyle),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(FontStyles.Normal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryInvalidated));

        public static readonly DependencyProperty TextAlignmentProperty =
            DependencyProperty.Register(
                nameof(TextAlignment),
                typeof(TextAlignment),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(TextAlignment.Left, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryInvalidated));

        #endregion

        #region Dependency Properties - Stroke / Border & Fill

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register(
                nameof(Stroke),
                typeof(Brush),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register(
                nameof(StrokeThickness),
                typeof(double),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(3.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryInvalidated));

        public static readonly DependencyProperty StrokeDashArrayProperty =
            DependencyProperty.Register(
                nameof(StrokeDashArray),
                typeof(DoubleCollection),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FillProperty =
            DependencyProperty.Register(
                nameof(Fill),
                typeof(Brush),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        #endregion

        #region Dependency Properties - Liquid Wave & Physics

        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register(
                nameof(Progress),
                typeof(double),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LiquidBrushProperty =
            DependencyProperty.Register(
                nameof(LiquidBrush),
                typeof(Brush),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BackLiquidBrushProperty =
            DependencyProperty.Register(
                nameof(BackLiquidBrush),
                typeof(Brush),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WaveAmplitudeProperty =
            DependencyProperty.Register(
                nameof(WaveAmplitude),
                typeof(double),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(5.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WaveFrequencyProperty =
            DependencyProperty.Register(
                nameof(WaveFrequency),
                typeof(double),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(0.035, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WaveSpeedProperty =
            DependencyProperty.Register(
                nameof(WaveSpeed),
                typeof(double),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsWaveAnimatedProperty =
            DependencyProperty.Register(
                nameof(IsWaveAnimated),
                typeof(bool),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(true, OnIsWaveAnimatedChanged));

        public static readonly DependencyProperty ShowBubblesProperty =
            DependencyProperty.Register(
                nameof(ShowBubbles),
                typeof(bool),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        #endregion

        #region Dependency Properties - Corner Percentage Badge

        public static readonly DependencyProperty ShowPercentageProperty =
            DependencyProperty.Register(
                nameof(ShowPercentage),
                typeof(bool),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PercentageCornerProperty =
            DependencyProperty.Register(
                nameof(PercentageCorner),
                typeof(BadgeCorner),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(BadgeCorner.TopRight, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PercentageBrushProperty =
            DependencyProperty.Register(
                nameof(PercentageBrush),
                typeof(Brush),
                typeof(OutlinedTextBlock),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        #endregion

        #region CLR Properties

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public FontFamily FontFamily
        {
            get => (FontFamily)GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public FontWeight FontWeight
        {
            get => (FontWeight)GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }

        public FontStyle FontStyle
        {
            get => (FontStyle)GetValue(FontStyleProperty);
            set => SetValue(FontStyleProperty, value);
        }

        public TextAlignment TextAlignment
        {
            get => (TextAlignment)GetValue(TextAlignmentProperty);
            set => SetValue(TextAlignmentProperty, value);
        }

        public Brush? Stroke
        {
            get => (Brush?)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public DoubleCollection? StrokeDashArray
        {
            get => (DoubleCollection?)GetValue(StrokeDashArrayProperty);
            set => SetValue(StrokeDashArrayProperty, value);
        }

        public Brush? Fill
        {
            get => (Brush?)GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, Math.Clamp(value, 0.0, 100.0));
        }

        public Brush? LiquidBrush
        {
            get => (Brush?)GetValue(LiquidBrushProperty);
            set => SetValue(LiquidBrushProperty, value);
        }

        public Brush? BackLiquidBrush
        {
            get => (Brush?)GetValue(BackLiquidBrushProperty);
            set => SetValue(BackLiquidBrushProperty, value);
        }

        public double WaveAmplitude
        {
            get => (double)GetValue(WaveAmplitudeProperty);
            set => SetValue(WaveAmplitudeProperty, value);
        }

        public double WaveFrequency
        {
            get => (double)GetValue(WaveFrequencyProperty);
            set => SetValue(WaveFrequencyProperty, value);
        }

        public double WaveSpeed
        {
            get => (double)GetValue(WaveSpeedProperty);
            set => SetValue(WaveSpeedProperty, value);
        }

        public bool IsWaveAnimated
        {
            get => (bool)GetValue(IsWaveAnimatedProperty);
            set => SetValue(IsWaveAnimatedProperty, value);
        }

        public bool ShowBubbles
        {
            get => (bool)GetValue(ShowBubblesProperty);
            set => SetValue(ShowBubblesProperty, value);
        }

        public bool ShowPercentage
        {
            get => (bool)GetValue(ShowPercentageProperty);
            set => SetValue(ShowPercentageProperty, value);
        }

        public BadgeCorner PercentageCorner
        {
            get => (BadgeCorner)GetValue(PercentageCornerProperty);
            set => SetValue(PercentageCornerProperty, value);
        }

        public Brush? PercentageBrush
        {
            get => (Brush?)GetValue(PercentageBrushProperty);
            set => SetValue(PercentageBrushProperty, value);
        }

        #endregion

        #region Particle System (Bubbles)

        private class LiquidBubble
        {
            public double RelX;
            public double Y;
            public double Radius;
            public double Speed;
            public double WobbleSeed;
        }

        private readonly List<LiquidBubble> _bubbles = new();
        private readonly Random _random = new();
        private static readonly Brush BubbleBrush = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));
        private static readonly Brush BubbleSpecular = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
        private static readonly Brush BadgeBackgroundBrush = new SolidColorBrush(Color.FromArgb(225, 15, 20, 32));

        static OutlinedTextBlock()
        {
            BubbleBrush.Freeze();
            BubbleSpecular.Freeze();
            BadgeBackgroundBrush.Freeze();
        }

        #endregion

        private Geometry? _textGeometry;
        private double _wavePhase = 0.0;
        private bool _isRenderingHooked = false;
        private CancellationTokenSource? _transitionCts;

        public OutlinedTextBlock()
        {
            var defaultLiquid = new LinearGradientBrush(
                (Color)ColorConverter.ConvertFromString("#00E5FF"),
                (Color)ColorConverter.ConvertFromString("#0284C7"),
                new Point(0, 0),
                new Point(0, 1));
            defaultLiquid.Freeze();
            LiquidBrush = defaultLiquid;

            var defaultBackLiquid = new SolidColorBrush(Color.FromArgb(115, 0, 229, 255));
            defaultBackLiquid.Freeze();
            BackLiquidBrush = defaultBackLiquid;

            PercentageBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF"));

            InitBubbles(18);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void InitBubbles(int count)
        {
            _bubbles.Clear();
            for (int i = 0; i < count; i++)
            {
                _bubbles.Add(new LiquidBubble
                {
                    RelX = _random.NextDouble(),
                    Y = 9999,
                    Radius = 1.5 + _random.NextDouble() * 3.5,
                    Speed = 0.8 + _random.NextDouble() * 1.6,
                    WobbleSeed = _random.NextDouble() * Math.PI * 2
                });
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => UpdateAnimationHook();
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _transitionCts?.Cancel();
            _transitionCts?.Dispose();
            DetachAnimation();
        }

        #region Backspace & Typewriter Text Transition Method

        /// <summary>
        /// Smoothly transitions the text with a backspace erasing animation, updates properties (liquid color,
        /// border stroke, progress, etc.) while empty, and then types the new text with a typewriter animation.
        /// </summary>
        public async Task TransitionTextAsync(
            string newText,
            Brush? newLiquidBrush = null,
            Brush? newBackLiquidBrush = null,
            Brush? newStroke = null,
            Brush? newPercentageBrush = null,
            double? newProgress = null,
            TimeSpan? eraseSpeed = null,
            TimeSpan? typeSpeed = null,
            TimeSpan? pauseDelay = null)
        {
            // Cancel any transition currently running
            _transitionCts?.Cancel();
            _transitionCts?.Dispose();
            _transitionCts = new CancellationTokenSource();
            var token = _transitionCts.Token;

            var eraseDelay = eraseSpeed ?? TimeSpan.FromMilliseconds(45);
            var typeDelay = typeSpeed ?? TimeSpan.FromMilliseconds(70);
            var pauseTime = pauseDelay ?? TimeSpan.FromMilliseconds(160);

            try
            {
                // ==========================================
                // Phase 1: Backspace Animation (Right to Left)
                // ==========================================
                while (!string.IsNullOrEmpty(Text))
                {
                    token.ThrowIfCancellationRequested();
                    Text = Text.Substring(0, Text.Length - 1);
                    await Task.Delay(eraseDelay, token);
                }

                token.ThrowIfCancellationRequested();

                // ==========================================
                // Phase 2: Property Hand-off (While Empty)
                // ==========================================
                if (newLiquidBrush != null) LiquidBrush = newLiquidBrush;
                if (newBackLiquidBrush != null) BackLiquidBrush = newBackLiquidBrush;
                if (newStroke != null) Stroke = newStroke;
                if (newPercentageBrush != null) PercentageBrush = newPercentageBrush;
                if (newProgress.HasValue) Progress = newProgress.Value;

                // Brief natural pause before typing the new text
                await Task.Delay(pauseTime, token);

                // ==========================================
                // Phase 3: Typewriter Animation (Left to Right)
                // ==========================================
                if (!string.IsNullOrEmpty(newText))
                {
                    for (int i = 0; i < newText.Length; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        Text = newText.Substring(0, i + 1);
                        await Task.Delay(typeDelay, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Transition was gracefully cancelled by a newer request
            }
        }

        #endregion

        private static void OnIsWaveAnimatedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is OutlinedTextBlock control)
            {
                control.UpdateAnimationHook();
            }
        }

        private void UpdateAnimationHook()
        {
            if (IsWaveAnimated && IsLoaded)
            {
                AttachAnimation();
            }
            else
            {
                DetachAnimation();
            }
        }

        private void AttachAnimation()
        {
            if (!_isRenderingHooked)
            {
                CompositionTarget.Rendering += OnCompositionRendering;
                _isRenderingHooked = true;
            }
        }

        private void DetachAnimation()
        {
            if (_isRenderingHooked)
            {
                CompositionTarget.Rendering -= OnCompositionRendering;
                _isRenderingHooked = false;
            }
        }

        private void OnCompositionRendering(object? sender, EventArgs e)
        {
            _wavePhase += 0.07 * WaveSpeed;
            if (_wavePhase > Math.PI * 200)
            {
                _wavePhase = 0.0;
            }

            InvalidateVisual();
        }

        private static void OnGeometryInvalidated(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is OutlinedTextBlock control)
            {
                control._textGeometry = null;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            EnsureGeometry();

            if (_textGeometry == null)
            {
                return new Size(0, 0);
            }

            Pen? pen = CreatePen();
            Rect bounds = _textGeometry.GetRenderBounds(pen);

            if (bounds.IsEmpty)
            {
                return new Size(0, 0);
            }

            double extraMargin = ShowPercentage ? 45.0 : 0.0;

            return new Size(
                Math.Max(0, bounds.Right + extraMargin + 20),
                Math.Max(0, bounds.Bottom + extraMargin + 10));
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            EnsureGeometry();

            if (_textGeometry == null)
            {
                return;
            }

            Pen? pen = CreatePen();
            Rect bounds = _textGeometry.GetRenderBounds(pen);

            // 1. Optional background fill inside text
            if (Fill != null)
            {
                drawingContext.DrawGeometry(Fill, null, _textGeometry);
            }

            // 2. Render liquid waves and bubbles clipped strictly to text glyphs
            double progressRatio = Math.Clamp(Progress / 100.0, 0.0, 1.0);
            if (progressRatio > 0.001 && LiquidBrush != null && !bounds.IsEmpty)
            {
                drawingContext.PushClip(_textGeometry);

                double waterLevelY = bounds.Bottom - (bounds.Height * progressRatio);
                double amplitude = progressRatio >= 0.99 ? 0.0 : WaveAmplitude;

                // Back Wave (Secondary depth layer)
                if (BackLiquidBrush != null && amplitude > 0)
                {
                    Geometry backWave = CreateWaveGeometry(bounds, waterLevelY, amplitude * 0.85, _wavePhase + Math.PI * 0.7);
                    drawingContext.DrawGeometry(BackLiquidBrush, null, backWave);
                }

                // Front Wave (Primary liquid layer)
                Geometry frontWave = CreateWaveGeometry(bounds, waterLevelY, amplitude, _wavePhase);
                drawingContext.DrawGeometry(LiquidBrush, null, frontWave);

                // Buoyant Rising Bubbles inside liquid
                if (ShowBubbles && progressRatio > 0.05)
                {
                    RenderBubbles(drawingContext, bounds, waterLevelY);
                }

                drawingContext.Pop();
            }

            // 3. Render crisp outer border outline on top
            if (pen != null)
            {
                drawingContext.DrawGeometry(null, pen, _textGeometry);
            }

            // 4. Render Corner Progress Percentage Badge (x%) if enabled
            if (ShowPercentage && !bounds.IsEmpty)
            {
                RenderCornerPercentageBadge(drawingContext, bounds);
            }
        }

        private void RenderCornerPercentageBadge(DrawingContext dc, Rect bounds)
        {
            string percentText = $"{Math.Round(Progress)}%";
            double badgeFontSize = Math.Max(13.0, FontSize * 0.22);

            Brush textBrush = PercentageBrush ?? Brushes.Cyan;

            var ft = new FormattedText(
                percentText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                badgeFontSize,
                textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double padH = 10.0;
            double padV = 5.0;
            double badgeW = ft.Width + (padH * 2);
            double badgeH = ft.Height + (padV * 2);

            double badgeX;
            double badgeY;

            switch (PercentageCorner)
            {
                case BadgeCorner.TopLeft:
                    badgeX = bounds.Left - (badgeW * 0.35);
                    badgeY = bounds.Top - (badgeH * 0.45);
                    break;
                case BadgeCorner.BottomLeft:
                    badgeX = bounds.Left - (badgeW * 0.35);
                    badgeY = bounds.Bottom - (badgeH * 0.55);
                    break;
                case BadgeCorner.BottomRight:
                    badgeX = bounds.Right - (badgeW * 0.65);
                    badgeY = bounds.Bottom - (badgeH * 0.55);
                    break;
                case BadgeCorner.TopRight:
                default:
                    badgeX = bounds.Right - (badgeW * 0.65);
                    badgeY = bounds.Top - (badgeH * 0.45);
                    break;
            }

            var badgeRect = new Rect(badgeX, badgeY, badgeW, badgeH);

            var borderPen = new Pen(textBrush, 1.5);
            if (borderPen.CanFreeze) borderPen.Freeze();

            dc.DrawRoundedRectangle(BadgeBackgroundBrush, borderPen, badgeRect, 8, 8);
            dc.DrawText(ft, new Point(badgeX + padH, badgeY + padV));
        }

        private void RenderBubbles(DrawingContext dc, Rect bounds, double waterLevelY)
        {
            foreach (var b in _bubbles)
            {
                b.Y -= b.Speed * Math.Max(0.2, WaveSpeed);

                if (b.Y <= waterLevelY || b.Y > bounds.Bottom + 10)
                {
                    b.Y = bounds.Bottom + _random.NextDouble() * 15;
                    b.RelX = _random.NextDouble();
                }

                double bubbleX = bounds.Left + (bounds.Width * b.RelX) + Math.Sin(_wavePhase * 1.5 + b.WobbleSeed) * 4.0;
                var center = new Point(bubbleX, b.Y);

                dc.DrawEllipse(BubbleBrush, null, center, b.Radius, b.Radius);

                var specCenter = new Point(bubbleX - b.Radius * 0.35, b.Y - b.Radius * 0.35);
                dc.DrawEllipse(BubbleSpecular, null, specCenter, b.Radius * 0.3, b.Radius * 0.3);
            }
        }

        private Geometry CreateWaveGeometry(Rect bounds, double waterLevelY, double amplitude, double phase)
        {
            var wave = new StreamGeometry();
            using (StreamGeometryContext ctx = wave.Open())
            {
                double startX = bounds.Left - 30;
                double endX = bounds.Right + 30;
                double step = 7.0;

                double startY = waterLevelY + amplitude * Math.Sin((startX * WaveFrequency) + phase);
                ctx.BeginFigure(new Point(startX, startY), isFilled: true, isClosed: true);

                for (double x = startX + step; x <= endX; x += step)
                {
                    double y = waterLevelY + amplitude * Math.Sin((x * WaveFrequency) + phase);
                    ctx.LineTo(new Point(x, y), isStroked: false, isSmoothJoin: true);
                }

                ctx.LineTo(new Point(endX, bounds.Bottom + 35), isStroked: false, isSmoothJoin: false);
                ctx.LineTo(new Point(startX, bounds.Bottom + 35), isStroked: false, isSmoothJoin: false);
            }

            wave.Freeze();
            return wave;
        }

        private Pen? CreatePen()
        {
            if (Stroke == null || StrokeThickness <= 0)
            {
                return null;
            }

            var pen = new Pen(Stroke, StrokeThickness)
            {
                LineJoin = PenLineJoin.Round,
                EndLineCap = PenLineCap.Round,
                StartLineCap = PenLineCap.Round
            };

            if (StrokeDashArray != null && StrokeDashArray.Count > 0)
            {
                pen.DashStyle = new DashStyle(StrokeDashArray, 0);
            }

            if (pen.CanFreeze)
            {
                pen.Freeze();
            }

            return pen;
        }

        private void EnsureGeometry()
        {
            if (_textGeometry != null)
            {
                return;
            }

            if (string.IsNullOrEmpty(Text))
            {
                _textGeometry = null;
                return;
            }

            double pixelsPerDip = 1.0;
            try
            {
                pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            }
            catch
            {
                pixelsPerDip = 1.0;
            }

            var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretches.Normal);

            var formattedText = new FormattedText(
                Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                Math.Max(1.0, FontSize),
                Brushes.Black,
                pixelsPerDip)
            {
                TextAlignment = TextAlignment
            };

            double offset = Math.Max(25.0, StrokeThickness + 15.0);
            Geometry geometry = formattedText.BuildGeometry(new Point(offset, offset));

            if (geometry.CanFreeze)
            {
                geometry.Freeze();
            }

            _textGeometry = geometry;
        }
    }
}
