using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Aarohi.Loadder
{
    public partial class AarohiLoadder : UserControl
    {
        #region Enums

        public enum EasingType { Linear, EaseIn, EaseOut, EaseInOut, SmoothStep }
        public enum RevealMode { Radial, Vertical, Horizontal }
        public enum FillTimingMode { WithStrokes, AfterStrokes, Custom }

        public enum StrokeGroup { Logo, Text }


        #endregion

        #region Fields

        private EasingType easingType = EasingType.EaseInOut;
        private Func<double, double> customEasing;
        private double globalDurationSeconds = 1.0;
        private double globalStartDelaySeconds = 0.0;

        private readonly List<Line> lines = new List<Line>();
        private readonly List<FillRegion> fillRegions = new List<FillRegion>();

        private readonly Timer animTimer = new Timer();
        private readonly Stopwatch sw = new Stopwatch();

        private RectangleF worldBounds;
        private const int marginPixels = 10;

        public RevealMode fillRevealMode = RevealMode.Vertical;
        public bool drawFillAboveStrokes = true;

        private FillTimingMode fillTiming = FillTimingMode.WithStrokes;
        private double fillAfterOffsetSeconds = 0.05;

        private long lastElapsedMs;

        private bool strokeFadeStarted;
        private double strokeFadeStartTime;
        private double strokeAlphaFactor = 1.0;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowGuideLines { get; set; } = false;

        private bool guideFadeStarted;
        private double guideFadeStartTime;
        private double guideAlphaFactor = 1.0;

        // tweakable
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double GuideFadeDelay { get; set; } = 0.10;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double GuideFadeDuration { get; set; } = 0.60;

        #endregion






        #region Properties

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool FadeStrokesAfterFill { get; set; } = true;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double StrokeFadeDuration { get; set; } = 0.9;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double StrokeFadeDelay { get; set; } = 0.05;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool EnableReflection { get; set; } = false;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double ReflectionSpeed { get; set; } = 0.2;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float ReflectionIntensity { get; set; } = 0.6f;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float ReflectionAngle { get; set; } = -30f;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double ReflectionThickness { get; set; } = 0.22;

        #endregion

        #region Ctor

        public AarohiLoadder()
        {
            InitializeComponent();

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            double offset = 0.0;

            lines.AddRange(new[]
            {
                // Logo
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

                // Text
                new Line(0.5, 0.3, -2, -0.6)   { Group = StrokeGroup.Text },
                new Line(-0.5, -0.3, -2, -0.6) { Group = StrokeGroup.Text },
                new Line(-1.6, -0.5, 0.5)      { Group = StrokeGroup.Text },

                new Line(0.5, 2.05, -2, -0.6)  { Group = StrokeGroup.Text },
                new Line(-0.5, 1.45, -2, -0.6) { Group = StrokeGroup.Text },
                new Line(-1.6, 1.25, 2.25)     { Group = StrokeGroup.Text },

                new Line(0, 3.2, -2, -0.6)     { Group = StrokeGroup.Text },
                new Line(0, 4.3, -1.3, -0.6)   { Group = StrokeGroup.Text },
                new Line(-0.6, 3.2, 4.3)       { Group = StrokeGroup.Text },
                new Line(-1.3, 3.2, 4.3)       { Group = StrokeGroup.Text },
                new Line(-1, 2.35, -2, -1.3)   { Group = StrokeGroup.Text },

                new Line(0, 5.2, -1.9, -0.6)   { Group = StrokeGroup.Text },
                new Line(0, 6.5, -1.9, -0.6)   { Group = StrokeGroup.Text },
                new Line(-0.6, 5.2, 6.5)       { Group = StrokeGroup.Text },
                new Line(-1.9, 5.2, 6.5)       { Group = StrokeGroup.Text },

                new Line(-1.3, 7.4, 8.6)       { Group = StrokeGroup.Text },
                new Line(0, 7.4, -1.9, -0.6)   { Group = StrokeGroup.Text },
                new Line(0, 8.6, -1.9, -0.6)   { Group = StrokeGroup.Text },

                new Line(0, 9.6, -1.9, -0.6)   { Group = StrokeGroup.Text },


            });

            foreach (var line in lines)
            {
                if (!line.IsHorizontal)
                {
                    line.YMin += offset;
                    line.YMax -= offset;
                }

                line.DurationSeconds = 0.9;
                line.DelaySeconds = 0.6;
            }

            fillRegions.Add(new FillRegion(
                new[]
                {
                    new PointF(-0.1f, 0.0f),
                    new PointF(0.85f, 1.9f),
                    new PointF(0f, 3.6f),
                    new PointF(1.67f, 7f),
                    new PointF(2.26f, 8.11f),
                    new PointF(3.03f, 7.27f),
                    new PointF(9.8f, 0f),
                    new PointF(8.2f, 0f),
                    new PointF(2.5f, 6.15f),
                    new PointF(1.2f, 3.6f),
                    new PointF(2.05f, 1.9f),
                    new PointF(1.1f, 0f)
                },
                Color.FromArgb(255, 40, 24, 119))
            {
                DelaySeconds = 0.0,
                DurationSeconds = 1.2
            });

            fillRegions.Add(new FillRegion(
                new[]
                {
                    new PointF(3.2f, 3.6f),
                    new PointF(4.4f, 3.6f),
                    new PointF(5.25f, 1.9f),
                    new PointF(4.3f, 0f),
                    new PointF(3.1f, 0f),
                    new PointF(4.05f, 1.9f),
                },
                Color.FromArgb(255, 40, 24, 119))
            {
                DelaySeconds = 0.08,
                DurationSeconds = 1.0
            });

            fillRegions.Add(new FillRegion(
                new[]
                {
                    new PointF(1.6f, 3.6f),
                    new PointF(2.8f, 3.6f),
                    new PointF(3.65f, 1.9f),
                    new PointF(2.7f, 0f),
                    new PointF(1.5f, 0f),
                    new PointF(2.45f, 1.9f),
                },
                Color.FromArgb(255, 237, 127, 14))
            {
                DelaySeconds = 0.25,
                DurationSeconds = 0.9
            });

            ComputeWorldBounds();

            animTimer.Interval = 16;
            animTimer.Tick += AnimTimer_Tick;

            ResetAnimation();
            StartAnimation();
        }

        #endregion

        #region Types

        public class Line
        {
            public double? Slope { get; set; }
            public double? Intercept { get; set; }
            public double? YMin { get; set; }
            public double? YMax { get; set; }
            public double? XMin { get; set; }
            public double? XMax { get; set; }
            public bool IsHorizontal { get; set; }

            public double DelaySeconds { get; set; } = 0.0;
            public double DurationSeconds { get; set; } = 1.0;
            public double ElapsedSeconds { get; set; } = 0.0;
            public double Progress => (DurationSeconds <= 0) ? 1.0 : Math.Min(1.0, ElapsedSeconds / DurationSeconds);
            public StrokeGroup Group { get; set; } = StrokeGroup.Logo;
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

            public (PointF start, PointF end) GetWorldEndpoints()
            {
                if (IsHorizontal)
                {
                    float y = (float)Intercept.GetValueOrDefault();
                    float x1 = (float)XMin.GetValueOrDefault();
                    float x2 = (float)XMax.GetValueOrDefault();
                    return (new PointF(x1, y), new PointF(x2, y));
                }

                float y1 = (float)YMin.GetValueOrDefault();
                float y2 = (float)YMax.GetValueOrDefault();
                float m = (float)Slope.GetValueOrDefault();
                float c = (float)Intercept.GetValueOrDefault();
                float x1v = m * y1 + c;
                float x2v = m * y2 + c;
                return (new PointF(x1v, y1), new PointF(x2v, y2));
            }

            public PointF GetWorldCenter()
            {
                var (s, e) = GetWorldEndpoints();
                return new PointF((s.X + e.X) / 2f, (s.Y + e.Y) / 2f);
            }

            public (PointF left, PointF right) GetPointsFromCenter(double t)
            {
                var (s, e) = GetWorldEndpoints();
                var c = GetWorldCenter();

                float lx = c.X + (s.X - c.X) * (float)t;
                float ly = c.Y + (s.Y - c.Y) * (float)t;
                float rx = c.X + (e.X - c.X) * (float)t;
                float ry = c.Y + (e.Y - c.Y) * (float)t;

                return (new PointF(lx, ly), new PointF(rx, ry));
            }
        }

        public class FillRegion
        {
            public PointF[] PolygonWorld { get; set; }
            public Color FillColor { get; set; }
            public double DelaySeconds { get; set; } = 0.0;
            public double DurationSeconds { get; set; } = 1.0;
            public double ElapsedSeconds { get; set; } = 0.0;
            public double Progress => (DurationSeconds <= 0) ? 1.0 : Math.Min(1.0, ElapsedSeconds / DurationSeconds);

            public EasingType? EasingTypeOverride { get; set; }
            public Func<double, double> CustomEasing { get; set; }

            public FillRegion(PointF[] polygonWorld, Color color)
            {
                PolygonWorld = polygonWorld ?? Array.Empty<PointF>();
                FillColor = color;
            }

            public PointF GetWorldCentroid()
            {
                var pts = PolygonWorld;
                int n = pts.Length;
                if (n == 0) return new PointF(0, 0);

                double a = 0, cx = 0, cy = 0;

                for (int i = 0; i < n; ++i)
                {
                    var p0 = pts[i];
                    var p1 = pts[(i + 1) % n];
                    double cross = p0.X * p1.Y - p1.X * p0.Y;
                    a += cross;
                    cx += (p0.X + p1.X) * cross;
                    cy += (p0.Y + p1.Y) * cross;
                }

                a *= 0.5;

                if (Math.Abs(a) < 1e-9)
                {
                    float sx = 0, sy = 0;
                    for (int i = 0; i < n; i++) { sx += pts[i].X; sy += pts[i].Y; }
                    return new PointF(sx / n, sy / n);
                }

                cx /= (6.0 * a);
                cy /= (6.0 * a);
                return new PointF((float)cx, (float)cy);
            }
        }

        #endregion

        #region Public API

        public void SetFillOnTop(bool fillOnTop)
        {
            drawFillAboveStrokes = fillOnTop;
            Invalidate();
        }

        public void SetFillTiming(FillTimingMode mode, double afterOffsetSeconds = 0.05)
        {
            fillTiming = mode;
            fillAfterOffsetSeconds = afterOffsetSeconds;
        }

        public void StartReflection()
        {
            EnableReflection = true;
            if (!animTimer.Enabled) animTimer.Start();
        }

        public void StopReflection()
        {
            EnableReflection = false;
        }

        public void SetFillEasing(EasingType easing)
        {
            easingType = easing;
        }

        public void SetRegionEasing(FillRegion region, EasingType easing)
        {
            if (region != null) region.EasingTypeOverride = easing;
        }

        public void SetRegionCustomEasing(FillRegion region, Func<double, double> customEasingFunc)
        {
            if (region != null) region.CustomEasing = customEasingFunc;
        }

        public void SetFillRegions(IEnumerable<FillRegion> regions)
        {
            fillRegions.Clear();
            if (regions != null)
            {
                foreach (var r in regions)
                {
                    if (r.DurationSeconds <= 0) r.DurationSeconds = globalDurationSeconds;
                    fillRegions.Add(r);
                }
            }
            ComputeWorldBounds();
            ResetAnimation();
        }

        public void AddFillRegion(FillRegion region)
        {
            if (region == null) return;
            if (region.DurationSeconds <= 0) region.DurationSeconds = globalDurationSeconds;
            fillRegions.Add(region);
            ComputeWorldBounds();
        }

        public void ClearFillRegions()
        {
            fillRegions.Clear();
            ComputeWorldBounds();
            Invalidate();
        }

        public void StartAnimation()
        {
            ApplyFillTimingToRegions();
            guideFadeStarted = false;
            guideAlphaFactor = 1.0;

            double maxFillDelay = fillRegions.Count > 0 ? fillRegions.Max(r => r.DelaySeconds) : 0.0;
            foreach (var l in lines)
                l.DelaySeconds = Math.Max(l.DelaySeconds, maxFillDelay * 0.6);

            strokeFadeStarted = false;
            strokeAlphaFactor = 1.0;

            sw.Restart();
            lastElapsedMs = sw.ElapsedMilliseconds;
            animTimer.Start();
        }

        public void StopAnimation()
        {
            animTimer.Stop();
            sw.Reset();
        }

        public void PauseAnimation()
        {
            animTimer.Stop();
            sw.Stop();
        }

        public void ResumeAnimation()
        {
            if (!sw.IsRunning) sw.Start();
            animTimer.Start();
        }

        public void ResetAnimation()
        {
            foreach (var l in lines) l.ElapsedSeconds = 0.0;
            foreach (var r in fillRegions) r.ElapsedSeconds = 0.0;
            guideFadeStarted = false;
            guideAlphaFactor = 1.0;

            sw.Reset();
            strokeFadeStarted = false;
            strokeAlphaFactor = 1.0;

            animTimer.Stop();
            Invalidate();
        }

        public void SetEasing(EasingType type)
        {
            easingType = type;
            customEasing = null;
        }

        public void SetCustomEasing(Func<double, double> func)
        {
            customEasing = func;
        }

        public void SetGlobalDuration(double seconds, bool applyToExisting = false)
        {
            if (seconds <= 0) throw new ArgumentException("duration > 0");
            globalDurationSeconds = seconds;

            if (applyToExisting)
            {
                foreach (var l in lines) l.DurationSeconds = seconds;
                foreach (var r in fillRegions) r.DurationSeconds = seconds;
            }
        }

        #endregion

        #region Timing

        private void ApplyFillTimingToRegions()
        {
            if (fillTiming == FillTimingMode.Custom) return;

            double earliestLineStart = double.PositiveInfinity;
            double latestLineEnd = 0.0;

            if (lines.Count > 0)
            {
                foreach (var l in lines)
                {
                    earliestLineStart = Math.Min(earliestLineStart, l.DelaySeconds);
                    latestLineEnd = Math.Max(latestLineEnd, l.DelaySeconds + l.DurationSeconds);
                }

                if (double.IsInfinity(earliestLineStart))
                    earliestLineStart = 0.0;
            }
            else
            {
                earliestLineStart = 0.0;
                latestLineEnd = 0.0;
            }

            foreach (var r in fillRegions)
            {
                if (fillTiming == FillTimingMode.WithStrokes)
                    r.DelaySeconds = Math.Max(0.0, earliestLineStart);
                else
                    r.DelaySeconds = latestLineEnd + fillAfterOffsetSeconds;
            }
        }

        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            if (!sw.IsRunning)
            {
                sw.Start();
                lastElapsedMs = sw.ElapsedMilliseconds;
                return;
            }

            long now = sw.ElapsedMilliseconds;
            double dt = (now - lastElapsedMs) / 1000.0;
            lastElapsedMs = now;

            if (dt <= 0) return;
            if (dt > 0.25) dt = 0.25;

            bool needInvalidate = false;
            double globalTime = sw.Elapsed.TotalSeconds;

            for (int i = 0; i < fillRegions.Count; i++)
            {
                var r = fillRegions[i];
                if (r.ElapsedSeconds < r.DurationSeconds && globalTime >= r.DelaySeconds)
                {
                    r.ElapsedSeconds = Math.Min(r.DurationSeconds, r.ElapsedSeconds + dt);
                    needInvalidate = true;
                }
            }

            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                if (l.ElapsedSeconds < l.DurationSeconds && globalTime >= l.DelaySeconds)
                {
                    l.ElapsedSeconds = Math.Min(l.DurationSeconds, l.ElapsedSeconds + dt);
                    needInvalidate = true;
                }
            }

            bool allFillsDone = fillRegions.All(fr => fr.ElapsedSeconds >= fr.DurationSeconds - 1e-6);
            if (allFillsDone && !guideFadeStarted)
            {
                guideFadeStarted = true;
                guideFadeStartTime = sw.Elapsed.TotalSeconds + GuideFadeDelay;
            }

            if (guideFadeStarted)
            {
                double fadeT = (globalTime - guideFadeStartTime) / Math.Max(1e-9, GuideFadeDuration);
                fadeT = Math.Max(0.0, Math.Min(1.0, fadeT));

                guideAlphaFactor = (1.0 - fadeT) * (1.0 - fadeT);

                if (guideAlphaFactor < 0.0) guideAlphaFactor = 0.0;
                if (guideAlphaFactor > 1.0) guideAlphaFactor = 1.0;

                needInvalidate = true;
            }

            if (FadeStrokesAfterFill && allFillsDone && !strokeFadeStarted)
            {
                strokeFadeStarted = true;
                strokeFadeStartTime = sw.Elapsed.TotalSeconds + StrokeFadeDelay;
            }

            if (strokeFadeStarted)
            {
                double fadeT = (globalTime - strokeFadeStartTime) / Math.Max(1e-9, StrokeFadeDuration);
                fadeT = Math.Max(0.0, Math.Min(1.0, fadeT));
                strokeAlphaFactor = (1.0 - fadeT) * (1.0 - fadeT);
                if (strokeAlphaFactor < 0.0) strokeAlphaFactor = 0.0;
                if (strokeAlphaFactor > 1.0) strokeAlphaFactor = 1.0;
                needInvalidate = true;
            }

            bool anyActive =
                lines.Any(x => x.ElapsedSeconds < x.DurationSeconds) ||
                fillRegions.Any(x => x.ElapsedSeconds < x.DurationSeconds) ||
                (FadeStrokesAfterFill && strokeAlphaFactor > 0.001) ||
                EnableReflection;

            if (!anyActive)
            {
                animTimer.Stop();
                sw.Stop();
            }

            if (needInvalidate) Invalidate();
        }

        #endregion

        #region Rendering

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            int guideAlpha = (int)(180 * guideAlphaFactor);
            guideAlpha = Math.Max(0, Math.Min(180, guideAlpha));

            if (guideAlpha > 0)
            {
                using (var penTrack = new Pen(Color.FromArgb(guideAlpha, 200, 200, 200), 1f))
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        var l = lines[i];
                        var (wStart, wEnd) = l.GetWorldEndpoints();
                        g.DrawLine(penTrack, MapToScreen(wStart), MapToScreen(wEnd));
                    }
                }
            }

            if (drawFillAboveStrokes)
            {
                DrawStrokes(g, strokeAlphaFactor);

                for (int i = 0; i < fillRegions.Count; i++)
                    PaintFillRegion(g, fillRegions[i]);

                if (EnableReflection) PaintReflection(g);
            }
            else
            {
                for (int i = 0; i < fillRegions.Count; i++)
                    PaintFillRegion(g, fillRegions[i]);

                if (EnableReflection) PaintReflection(g);

                DrawStrokes(g, strokeAlphaFactor);
            }
        }

        private void DrawStrokes(Graphics g, double alphaFactor)
        {
            int logoAlpha = (int)(255 * alphaFactor);
            logoAlpha = Math.Max(0, Math.Min(255, logoAlpha));

            const int textAlpha = 255; // <- TEXT never fades

            using (var penLogo = new Pen(Color.FromArgb(logoAlpha, 40, 24, 119), 2f))
            using (var penText = new Pen(Color.FromArgb(textAlpha, 40, 24, 119), 5f)) // or your text color
            {
                penLogo.StartCap = penLogo.EndCap = LineCap.Round;
                penText.StartCap = penText.EndCap = LineCap.Round;

                for (int i = 0; i < lines.Count; i++)
                {
                    var l = lines[i];

                    // choose pen based on group
                    var pen = (l.Group == StrokeGroup.Text) ? penText : penLogo;

                    double rawT = l.Progress;
                    var (leftW, rightW) = l.GetPointsFromCenter(ApplyEasing(rawT));

                    g.DrawLine(pen, MapToScreen(l.GetWorldCenter()), MapToScreen(leftW));
                    g.DrawLine(pen, MapToScreen(l.GetWorldCenter()), MapToScreen(rightW));
                }
            }
        }

        private void PaintFillRegion(Graphics g, FillRegion region)
        {
            if (region == null || region.PolygonWorld == null || region.PolygonWorld.Length < 3)
                return;

            double rawT = region.Progress;
            double easedT = GetEasedForRegion(region, rawT);

            var polyScreen = region.PolygonWorld.Select(MapToScreen).ToArray();
            var centroidScreen = MapToScreen(region.GetWorldCentroid());

            float minX = polyScreen.Min(p => p.X);
            float maxX = polyScreen.Max(p => p.X);
            float minY = polyScreen.Min(p => p.Y);
            float maxY = polyScreen.Max(p => p.Y);

            float width = Math.Max(1f, maxX - minX);
            float height = Math.Max(1f, maxY - minY);

            float maxR = 0f;
            for (int i = 0; i < polyScreen.Length; i++)
            {
                float dx = polyScreen[i].X - centroidScreen.X;
                float dy = polyScreen[i].Y - centroidScreen.Y;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d > maxR) maxR = d;
            }

            using (var revealPath = new GraphicsPath())
            {
                if (fillRevealMode == RevealMode.Radial)
                {
                    float currentR = (float)Math.Max(2.0, easedT * maxR);
                    revealPath.AddEllipse(centroidScreen.X - currentR, centroidScreen.Y - currentR, currentR * 2f, currentR * 2f);
                }
                else if (fillRevealMode == RevealMode.Vertical)
                {
                    float h = Math.Max(2f, (float)(easedT * height));
                    revealPath.AddRectangle(new RectangleF(minX - 1f, maxY - h, width + 2f, h + 2f));
                }
                else
                {
                    float w = Math.Max(2f, (float)(easedT * width));
                    revealPath.AddRectangle(new RectangleF(minX, minY - 1f, w + 2f, height + 2f));
                }

                using (var polyPath = new GraphicsPath())
                {
                    polyPath.AddPolygon(polyScreen);

                    GraphicsState state = g.Save();
                    try
                    {
                        g.SetClip(polyPath);

                        byte alpha = (byte)(255 * Math.Min(1.0, rawT * 1.1));
                        using (var brush = new SolidBrush(Color.FromArgb(alpha, region.FillColor.R, region.FillColor.G, region.FillColor.B)))
                            g.FillPath(brush, revealPath);
                    }
                    finally
                    {
                        g.Restore(state);
                    }
                }
            }

            if (region.Progress >= 1.0 - 1e-6)
            {
                using (var path = new GraphicsPath())
                {
                    path.AddPolygon(polyScreen);

                    using (var brush = new SolidBrush(region.FillColor))
                        g.FillPath(brush, path);

                    using (var pen = new Pen(Color.FromArgb(200,
                        Math.Max(0, region.FillColor.R - 20),
                        Math.Max(0, region.FillColor.G - 20),
                        Math.Max(0, region.FillColor.B - 20)), 2f))
                    {
                        pen.LineJoin = LineJoin.Round;
                        g.DrawPath(pen, path);
                    }
                }
            }
        }

        private void PaintReflection(Graphics g)
        {
            if (fillRegions.Count == 0) return;

            using (var combined = new GraphicsPath())
            {
                for (int i = 0; i < fillRegions.Count; i++)
                {
                    var r = fillRegions[i];
                    if (r.PolygonWorld == null || r.PolygonWorld.Length < 3) continue;
                    combined.AddPolygon(r.PolygonWorld.Select(MapToScreen).ToArray());
                }

                if (combined.PointCount == 0) return;

                GraphicsState state = g.Save();
                try
                {
                    g.SetClip(combined);

                    var bounds = combined.GetBounds();
                    double t = (DateTime.UtcNow.TimeOfDay.TotalSeconds * ReflectionSpeed) % 1.0;

                    float stripThickness = Math.Max(2f, (float)(ReflectionThickness * bounds.Width));
                    float stripLength = Math.Max(bounds.Height * 1.4f, bounds.Height + 10f);

                    float startX = bounds.Left - stripThickness * 1.5f;
                    float endX = bounds.Right + stripThickness * 1.5f;
                    float centerX = (float)(startX + (endX - startX) * t);
                    float centerY = bounds.Top + bounds.Height * 0.5f;

                    var rect = new RectangleF(centerX - stripThickness / 2f, centerY - stripLength / 2f, stripThickness, stripLength);

                    using (var brush = new LinearGradientBrush(rect, Color.Transparent, Color.Transparent, ReflectionAngle))
                    {
                        int centerAlpha = (int)(255 * Math.Max(0f, Math.Min(1f, ReflectionIntensity)));
                        var blend = new ColorBlend
                        {
                            Colors = new[]
                            {
                                Color.FromArgb(0, Color.White),
                                Color.FromArgb((int)(centerAlpha * 0.25), Color.White),
                                Color.FromArgb(centerAlpha, Color.White),
                                Color.FromArgb((int)(centerAlpha * 0.25), Color.White),
                                Color.FromArgb(0, Color.White)
                            },
                            Positions = new[] { 0f, 0.35f, 0.5f, 0.65f, 1f }
                        };
                        brush.InterpolationColors = blend;

                        using (var stripPath = new GraphicsPath())
                        {
                            stripPath.AddRectangle(rect);

                            if (Math.Abs(ReflectionAngle) > 0.001f)
                            {
                                using (var m = new Matrix())
                                {
                                    m.RotateAt(ReflectionAngle, new PointF(centerX, centerY));
                                    stripPath.Transform(m);
                                }
                            }

                            g.FillPath(brush, stripPath);
                        }
                    }
                }
                finally
                {
                    g.Restore(state);
                }
            }
        }

        #endregion

        #region Easing

        private double GetEasedForRegion(FillRegion region, double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));

            if (region.CustomEasing != null)
            {
                try
                {
                    double v = region.CustomEasing(t);
                    if (v < 0.0) v = 0.0;
                    if (v > 1.0) v = 1.0;
                    return v;
                }
                catch { }
            }

            if (region.EasingTypeOverride.HasValue)
                return ApplyEasingWithType(region.EasingTypeOverride.Value, t);

            return ApplyEasing(t);
        }

        private double ApplyEasing(double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));

            if (customEasing != null)
            {
                try
                {
                    double v = customEasing(t);
                    if (v < 0.0) v = 0.0;
                    if (v > 1.0) v = 1.0;
                    return v;
                }
                catch { }
            }

            return ApplyEasingWithType(easingType, t);
        }

        private static double ApplyEasingWithType(EasingType type, double t)
        {
            switch (type)
            {
                case EasingType.Linear:
                    return t;

                case EasingType.EaseIn:
                    return t * t;

                case EasingType.EaseOut:
                    return 1 - (1 - t) * (1 - t);

                case EasingType.EaseInOut:
                    return (t < 0.5) ? (2 * t * t) : (1 - Math.Pow(-2 * t + 2, 2) / 2);

                case EasingType.SmoothStep:
                    return t * t * (3 - 2 * t);

                default:
                    return t;
            }
        }

        #endregion

        #region Bounds & Mapping

        private void ComputeWorldBounds()
        {
            bool first = true;
            float minX = 0, maxX = 0, minY = 0, maxY = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                var (s, e) = lines[i].GetWorldEndpoints();
                float lx = Math.Min(s.X, e.X);
                float rx = Math.Max(s.X, e.X);
                float ty = Math.Min(s.Y, e.Y);
                float by = Math.Max(s.Y, e.Y);

                if (first)
                {
                    minX = lx; maxX = rx; minY = ty; maxY = by;
                    first = false;
                }
                else
                {
                    minX = Math.Min(minX, lx);
                    maxX = Math.Max(maxX, rx);
                    minY = Math.Min(minY, ty);
                    maxY = Math.Max(maxY, by);
                }
            }

            for (int i = 0; i < fillRegions.Count; i++)
            {
                var r = fillRegions[i];
                if (r.PolygonWorld == null) continue;

                for (int j = 0; j < r.PolygonWorld.Length; j++)
                {
                    var p = r.PolygonWorld[j];
                    if (first)
                    {
                        minX = maxX = p.X;
                        minY = maxY = p.Y;
                        first = false;
                    }
                    else
                    {
                        minX = Math.Min(minX, p.X);
                        maxX = Math.Max(maxX, p.X);
                        minY = Math.Min(minY, p.Y);
                        maxY = Math.Max(maxY, p.Y);
                    }
                }
            }

            if (first)
            {
                worldBounds = new RectangleF(0, 0, 1, 1);
                return;
            }

            if (Math.Abs(maxX - minX) < 1e-6f) maxX = minX + 1.0f;
            if (Math.Abs(maxY - minY) < 1e-6f) maxY = minY + 1.0f;

            worldBounds = new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        private PointF MapToScreen(PointF worldPt)
        {
            float cw = ClientSize.Width - 2 * marginPixels;
            float ch = ClientSize.Height - 2 * marginPixels;

            if (cw <= 1 || ch <= 1)
                return new PointF(marginPixels, marginPixels);

            float wbW = Math.Max(1e-6f, worldBounds.Width);
            float wbH = Math.Max(1e-6f, worldBounds.Height);

            // ✅ one uniform scale -> no vertical/horizontal shrink
            float s = Math.Min(cw / wbW, ch / wbH);

            // center the drawing
            float drawW = wbW * s;
            float drawH = wbH * s;
            float ox = marginPixels + (cw - drawW) / 2f;
            float oy = marginPixels + (ch - drawH) / 2f;

            float x = ox + (worldPt.X - worldBounds.Left) * s;
            float y = oy + (worldBounds.Bottom - worldPt.Y) * s; // flip Y (top-down screen)

            return new PointF(x, y);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ComputeWorldBounds();
            Invalidate();
        }

        #endregion
    }
}
