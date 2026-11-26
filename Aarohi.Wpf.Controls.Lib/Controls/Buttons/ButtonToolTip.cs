using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Aarohi.Wpf.Controls.Lib.Controls.Buttons;

public static class ButtonToolTip
{
    private const int OpenDelayMs = 1500;
    private const int CloseDelayMs = 180;
    private const int OpenAnimationMs = 250;

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.RegisterAttached(
            "Title",
            typeof(string),
            typeof(ButtonToolTip),
            new PropertyMetadata(string.Empty, OnToolTipPropertyChanged));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.RegisterAttached(
            "Description",
            typeof(string),
            typeof(ButtonToolTip),
            new PropertyMetadata(string.Empty, OnToolTipPropertyChanged));

    public static readonly DependencyProperty LinkProperty =
        DependencyProperty.RegisterAttached(
            "Link",
            typeof(string),
            typeof(ButtonToolTip),
            new PropertyMetadata(string.Empty, OnToolTipPropertyChanged));

    public static readonly DependencyProperty HelpTopicProperty =
        DependencyProperty.RegisterAttached(
            "HelpTopic",
            typeof(string),
            typeof(ButtonToolTip),
            new PropertyMetadata(string.Empty, OnToolTipPropertyChanged));

    public static readonly DependencyProperty LinkToolTipProperty =
        DependencyProperty.RegisterAttached(
            "LinkToolTip",
            typeof(string),
            typeof(ButtonToolTip),
            new PropertyMetadata("Open help", OnToolTipPropertyChanged));

    public static readonly RoutedEvent HelpRequestedEvent =
        EventManager.RegisterRoutedEvent(
            "HelpRequested",
            RoutingStrategy.Bubble,
            typeof(EventHandler<HelpRequestedEventArgs>),
            typeof(ButtonToolTip));

    private static readonly DependencyProperty IsManagedToolTipProperty =
        DependencyProperty.RegisterAttached(
            "IsManagedToolTip",
            typeof(bool),
            typeof(ButtonToolTip),
            new PropertyMetadata(false));

    private static readonly DependencyProperty IsBehaviorAttachedProperty =
        DependencyProperty.RegisterAttached(
            "IsBehaviorAttached",
            typeof(bool),
            typeof(ButtonToolTip),
            new PropertyMetadata(false));

    private static readonly DependencyProperty PopupProperty =
        DependencyProperty.RegisterAttached(
            "Popup",
            typeof(Popup),
            typeof(ButtonToolTip),
            new PropertyMetadata(null));

    private static readonly DependencyProperty CloseTimerProperty =
        DependencyProperty.RegisterAttached(
            "CloseTimer",
            typeof(DispatcherTimer),
            typeof(ButtonToolTip),
            new PropertyMetadata(null));

    private static readonly DependencyProperty OpenTimerProperty =
        DependencyProperty.RegisterAttached(
            "OpenTimer",
            typeof(DispatcherTimer),
            typeof(ButtonToolTip),
            new PropertyMetadata(null));

    public static string GetTitle(DependencyObject dependencyObject)
    {
        return (string)dependencyObject.GetValue(TitleProperty);
    }

    public static void SetTitle(DependencyObject dependencyObject, string value)
    {
        dependencyObject.SetValue(TitleProperty, value);
    }

    public static string GetDescription(DependencyObject dependencyObject)
    {
        return (string)dependencyObject.GetValue(DescriptionProperty);
    }

    public static void SetDescription(DependencyObject dependencyObject, string value)
    {
        dependencyObject.SetValue(DescriptionProperty, value);
    }

    public static string GetLink(DependencyObject dependencyObject)
    {
        return (string)dependencyObject.GetValue(LinkProperty);
    }

    public static void SetLink(DependencyObject dependencyObject, string value)
    {
        dependencyObject.SetValue(LinkProperty, value);
    }

    public static string GetHelpTopic(DependencyObject dependencyObject)
    {
        return (string)dependencyObject.GetValue(HelpTopicProperty);
    }

    public static void SetHelpTopic(DependencyObject dependencyObject, string value)
    {
        dependencyObject.SetValue(HelpTopicProperty, value);
    }

    public static string GetLinkToolTip(DependencyObject dependencyObject)
    {
        return (string)dependencyObject.GetValue(LinkToolTipProperty);
    }

    public static void SetLinkToolTip(DependencyObject dependencyObject, string value)
    {
        dependencyObject.SetValue(LinkToolTipProperty, value);
    }

    public static void AddHelpRequestedHandler(DependencyObject dependencyObject, EventHandler<HelpRequestedEventArgs> handler)
    {
        if (dependencyObject is UIElement element)
        {
            element.AddHandler(HelpRequestedEvent, handler);
        }
    }

    public static void RemoveHelpRequestedHandler(DependencyObject dependencyObject, EventHandler<HelpRequestedEventArgs> handler)
    {
        if (dependencyObject is UIElement element)
        {
            element.RemoveHandler(HelpRequestedEvent, handler);
        }
    }

    private static bool GetIsManagedToolTip(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsManagedToolTipProperty);
    }

    private static void SetIsManagedToolTip(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsManagedToolTipProperty, value);
    }

    private static bool GetIsBehaviorAttached(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsBehaviorAttachedProperty);
    }

    private static void SetIsBehaviorAttached(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsBehaviorAttachedProperty, value);
    }

    private static Popup? GetPopup(DependencyObject dependencyObject)
    {
        return (Popup?)dependencyObject.GetValue(PopupProperty);
    }

    private static void SetPopup(DependencyObject dependencyObject, Popup? value)
    {
        dependencyObject.SetValue(PopupProperty, value);
    }

    private static DispatcherTimer? GetCloseTimer(DependencyObject dependencyObject)
    {
        return (DispatcherTimer?)dependencyObject.GetValue(CloseTimerProperty);
    }

    private static void SetCloseTimer(DependencyObject dependencyObject, DispatcherTimer? value)
    {
        dependencyObject.SetValue(CloseTimerProperty, value);
    }

    private static DispatcherTimer? GetOpenTimer(DependencyObject dependencyObject)
    {
        return (DispatcherTimer?)dependencyObject.GetValue(OpenTimerProperty);
    }

    private static void SetOpenTimer(DependencyObject dependencyObject, DispatcherTimer? value)
    {
        dependencyObject.SetValue(OpenTimerProperty, value);
    }

    private static void OnToolTipPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is Button button)
        {
            Apply(button);
        }
    }

    private static void Apply(Button button)
    {
        AttachBehavior(button);

        string description = GetDescription(button)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(description))
        {
            if (GetIsManagedToolTip(button))
            {
                ClosePopup(button);
                SetPopup(button, null);
                button.ClearValue(FrameworkElement.ToolTipProperty);
                SetIsManagedToolTip(button, false);
            }

            return;
        }

        ClosePopup(button);
        SetPopup(button, CreatePopup(button, description));
        button.ClearValue(FrameworkElement.ToolTipProperty);
        SetIsManagedToolTip(button, true);
    }

    private static void AttachBehavior(Button button)
    {
        if (GetIsBehaviorAttached(button))
        {
            return;
        }

        button.MouseEnter += Button_MouseEnter;
        button.MouseLeave += Button_MouseLeave;
        button.Unloaded += Button_Unloaded;
        SetIsBehaviorAttached(button, true);
    }

    private static Popup CreatePopup(Button owner, string description)
    {
        FrameworkElement content = CreatePopupContent(owner, description);

        Popup popup = new()
        {
            Child = content,
            Placement = PlacementMode.Bottom,
            PlacementTarget = owner,
            VerticalOffset = 8,
            AllowsTransparency = true,
            StaysOpen = true,
            Focusable = false
        };

        content.MouseEnter += (_, _) => StopCloseTimer(owner);
        content.MouseLeave += (_, _) => ScheduleClose(owner);

        return popup;
    }

    private static FrameworkElement CreatePopupContent(Button owner, string description)
    {
        string link = GetLink(owner)?.Trim() ?? string.Empty;
        string helpTopic = GetHelpTopic(owner)?.Trim() ?? string.Empty;

        Border chrome = new()
        {
            Opacity = 0,
            Padding = new Thickness(10, 8, 10, 8),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            MaxWidth = 380,
            SnapsToDevicePixels = true,
            RenderTransform = new TranslateTransform(0, -4)
        };

        chrome.SetResourceReference(Border.BackgroundProperty, "AppBrush.SurfaceAlt");
        chrome.SetResourceReference(Border.BorderBrushProperty, "AppBrush.Border");

        Grid row = new()
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        TextBlock descriptionText = new()
        {
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

        descriptionText.SetResourceReference(TextBlock.ForegroundProperty, "AppBrush.TextPrimary");

        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(descriptionText, 0);
        row.Children.Add(descriptionText);

        if (!string.IsNullOrWhiteSpace(helpTopic) || !string.IsNullOrWhiteSpace(link))
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock divider = new()
            {
                Margin = new Thickness(10, 0, 10, 0),
                Text = "|",
                VerticalAlignment = VerticalAlignment.Center
            };

            divider.SetResourceReference(TextBlock.ForegroundProperty, "AppBrush.BorderStrong");

            Border infoButton = CreateInfoButton(owner, helpTopic, link);

            Grid.SetColumn(divider, 1);
            Grid.SetColumn(infoButton, 2);

            row.Children.Add(divider);
            row.Children.Add(infoButton);
        }

        chrome.Child = row;
        return chrome;
    }

    private static Border CreateInfoButton(Button owner, string helpTopic, string link)
    {
        TextBlock icon = new()
        {
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "i",
            TextAlignment = TextAlignment.Center,
            IsHitTestVisible = false
        };

        icon.SetResourceReference(TextBlock.ForegroundProperty, "AppBrush.Primary");

        Border infoButton = new()
        {
            Width = 20,
            Height = 20,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(10),
            Cursor = Cursors.Hand,
            Child = icon,
            Focusable = true,
            ToolTip = GetLinkToolTip(owner)
        };

        infoButton.SetResourceReference(Border.BackgroundProperty, "AppBrush.PrimarySubtle");

        infoButton.MouseEnter += (_, _) =>
        {
            infoButton.SetResourceReference(Border.BackgroundProperty, "AppBrush.PrimaryHover");
        };

        infoButton.MouseLeave += (_, _) =>
        {
            infoButton.SetResourceReference(Border.BackgroundProperty, "AppBrush.PrimarySubtle");
        };

        infoButton.PreviewMouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;

            if (!string.IsNullOrWhiteSpace(helpTopic))
            {
                OpenHelpTopic(helpTopic, owner);
                return;
            }

            OpenLink(link, owner);
        };

        return infoButton;
    }

    private static void Button_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Button button)
        {
            ScheduleOpen(button);
        }
    }

    private static void Button_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Button button)
        {
            StopOpenTimer(button);
            ScheduleClose(button);
        }
    }

    private static void Button_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            ClosePopup(button);
        }
    }

    private static void ScheduleOpen(Button button)
    {
        string description = GetDescription(button)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        StopCloseTimer(button);

        DispatcherTimer? timer = GetOpenTimer(button);
        if (timer is null)
        {
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(OpenDelayMs)
            };

            timer.Tick += (_, _) =>
            {
                timer.Stop();

                if (button.IsMouseOver)
                {
                    OpenPopup(button);
                }
            };

            SetOpenTimer(button, timer);
        }

        timer.Stop();
        timer.Start();
    }

    private static void OpenPopup(Button button)
    {
        string description = GetDescription(button)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        StopCloseTimer(button);

        Popup? popup = GetPopup(button);
        if (popup is null)
        {
            popup = CreatePopup(button, description);
            SetPopup(button, popup);
        }

        popup.IsOpen = true;
        AnimatePopupOpen(popup);
    }

    private static void AnimatePopupOpen(Popup popup)
    {
        if (popup.Child is not FrameworkElement content)
        {
            return;
        }

        content.BeginAnimation(UIElement.OpacityProperty, null);
        content.Opacity = 0;

        DoubleAnimation fade = new(0, 1, TimeSpan.FromMilliseconds(OpenAnimationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        content.BeginAnimation(UIElement.OpacityProperty, fade);

        if (content.RenderTransform is TranslateTransform translate)
        {
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            translate.Y = -4;

            DoubleAnimation slide = new(-4, 0, TimeSpan.FromMilliseconds(OpenAnimationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            translate.BeginAnimation(TranslateTransform.YProperty, slide);
        }
    }

    private static void ScheduleClose(Button button)
    {
        DispatcherTimer? timer = GetCloseTimer(button);
        if (timer is null)
        {
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(CloseDelayMs)
            };

            timer.Tick += (_, _) =>
            {
                timer.Stop();

                Popup? popup = GetPopup(button);
                if (button.IsMouseOver || popup?.Child is FrameworkElement { IsMouseOver: true })
                {
                    return;
                }

                ClosePopup(button);
            };

            SetCloseTimer(button, timer);
        }

        timer.Stop();
        timer.Start();
    }

    private static void StopCloseTimer(Button button)
    {
        GetCloseTimer(button)?.Stop();
    }

    private static void StopOpenTimer(Button button)
    {
        GetOpenTimer(button)?.Stop();
    }

    private static void ClosePopup(Button button)
    {
        StopOpenTimer(button);
        StopCloseTimer(button);

        Popup? popup = GetPopup(button);
        if (popup is not null)
        {
            popup.IsOpen = false;
        }
    }

    private static void OpenLink(string link, Button owner)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out Uri? uri))
        {
            System.Windows.MessageBox.Show(Window.GetWindow(owner), "The help link is not valid.", "Open Link", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(Window.GetWindow(owner), ex.Message, "Open Link", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void OpenHelpTopic(string helpTopic, Button owner)
    {
        owner.RaiseEvent(new HelpRequestedEventArgs(HelpRequestedEvent, owner, helpTopic));
    }

    public sealed class HelpRequestedEventArgs : RoutedEventArgs
    {
        public HelpRequestedEventArgs(RoutedEvent routedEvent, object source, string helpTopic)
            : base(routedEvent, source)
        {
            HelpTopic = helpTopic;
        }

        public string HelpTopic { get; }
    }
}
