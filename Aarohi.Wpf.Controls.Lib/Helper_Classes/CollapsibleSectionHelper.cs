using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Aarohi.Wpf.Controls.Lib.Helper_Classes;

public static class CollapsibleSectionHelper
{
    private const double AnimationSeconds = 0.18;

    public static Border CreateSection(FrameworkElement resourceHost, string title, UIElement content)
    {
        Border contentHost = new()
        {
            Child = content,
            ClipToBounds = true,
            Margin = new Thickness(0, 12, 0, 0)
        };

        RotateTransform arrowTransform = new(90);
        TextBlock arrow = new()
        {
            Text = ">",
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransform = arrowTransform,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        arrow.SetResourceReference(TextBlock.ForegroundProperty, "AppBrush.TextSecondary");

        Border heading = CreateHeading(resourceHost, title, arrow);
        bool isExpanded = true;
        heading.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            isExpanded = !isExpanded;
            AnimateSection(contentHost, arrowTransform, isExpanded);
            e.Handled = true;
        };

        Border section = new()
        {
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(14),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Effect = new DropShadowEffect
            {
                BlurRadius = 16,
                Direction = 270,
                Opacity = 0.06,
                ShadowDepth = 4,
                Color = Colors.Black
            },
            Child = new StackPanel
            {
                Children =
                {
                    heading,
                    contentHost
                }
            }
        };

        section.SetResourceReference(Border.BackgroundProperty, "AppBrush.SectionPastel");
        section.SetResourceReference(Border.BorderBrushProperty, "AppBrush.Border");
        return section;
    }

    private static Border CreateHeading(FrameworkElement resourceHost, string title, UIElement arrow)
    {
        TextBlock headingText = new()
        {
            Text = title,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        headingText.SetResourceReference(TextBlock.ForegroundProperty, "AppBrush.Primary");

        Border accentBar = new()
        {
            Width = 4,
            Margin = new Thickness(0, 2, 10, 2),
            CornerRadius = new CornerRadius(2)
        };
        accentBar.SetResourceReference(Border.BackgroundProperty, "AppBrush.Primary");

        Grid headingContent = new();
        headingContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headingContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headingContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(accentBar, 0);
        Grid.SetColumn(headingText, 1);
        Grid.SetColumn(arrow, 2);
        headingContent.Children.Add(accentBar);
        headingContent.Children.Add(headingText);
        headingContent.Children.Add(arrow);

        Border heading = new()
        {
            Padding = new Thickness(10, 8, 12, 8),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Cursor = Cursors.Hand,
            Child = headingContent
        };
        heading.SetResourceReference(Border.BackgroundProperty, "AppBrush.PrimarySubtle");
        heading.SetResourceReference(Border.BorderBrushProperty, "AppBrush.BorderStrong");
        return heading;
    }

    private static void AnimateSection(Border contentHost, RotateTransform arrowTransform, bool isExpanded)
    {
        IEasingFunction easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        TimeSpan duration = TimeSpan.FromSeconds(AnimationSeconds);

        if (isExpanded)
        {
            contentHost.Visibility = Visibility.Visible;
            InputPlaceholderHelper.SetPlaceholdersSuppressed(contentHost, false);
            contentHost.MaxHeight = 0;
            contentHost.Opacity = 0;
            contentHost.UpdateLayout();
            contentHost.Child?.Measure(new Size(contentHost.ActualWidth > 0 ? contentHost.ActualWidth : double.PositiveInfinity, double.PositiveInfinity));
            double targetHeight = contentHost.Child?.DesiredSize.Height ?? 0;

            DoubleAnimation expandHeight = CreateAnimation(0, targetHeight, duration, easing);
            expandHeight.Completed += (_, _) => contentHost.MaxHeight = double.PositiveInfinity;
            contentHost.BeginAnimation(FrameworkElement.MaxHeightProperty, expandHeight);
            contentHost.BeginAnimation(UIElement.OpacityProperty, CreateAnimation(0, 1, duration, easing));
            arrowTransform.BeginAnimation(RotateTransform.AngleProperty, CreateAnimation(0, 90, duration, easing));
            return;
        }

        double currentHeight = contentHost.ActualHeight;
        InputPlaceholderHelper.SetPlaceholdersSuppressed(contentHost, true);
        contentHost.MaxHeight = currentHeight;
        DoubleAnimation collapseHeight = CreateAnimation(currentHeight, 0, duration, easing);
        collapseHeight.Completed += (_, _) =>
        {
            contentHost.Visibility = Visibility.Collapsed;
            contentHost.MaxHeight = double.PositiveInfinity;
        };

        contentHost.BeginAnimation(FrameworkElement.MaxHeightProperty, collapseHeight);
        contentHost.BeginAnimation(UIElement.OpacityProperty, CreateAnimation(contentHost.Opacity, 0, duration, easing));
        arrowTransform.BeginAnimation(RotateTransform.AngleProperty, CreateAnimation(90, 0, duration, easing));
    }

    private static DoubleAnimation CreateAnimation(double from, double to, TimeSpan duration, IEasingFunction easing)
    {
        return new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = duration,
            EasingFunction = easing
        };
    }
}
