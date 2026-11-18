using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Aarohi.Wpf.Controls.Lib.Controls.MessageBox;

internal partial class ModernMessageBoxWindow : Window
{
    private const string ResourcePrefix = "Aarohi.ModernMessageBox.";

    private readonly ModernMessageBoxOptions _options;
    private readonly IReadOnlyList<ModernMessageBoxAction> _actions;
    private readonly ModernMessageBoxAction? _cancelAction;
    private bool _isCompleting;

    internal ModernMessageBoxWindow(
        ModernMessageBoxOptions options,
        IReadOnlyList<ModernMessageBoxAction> actions)
    {
        _options = options;
        _actions = actions;
        _cancelAction = actions.FirstOrDefault(action => action.IsCancel);

        InitializeComponent();
        ApplyTheme();
        ConfigureContent();
        ConfigureIcon(options.Icon);
        CreateButtons();
    }

    internal ModernMessageBoxAction? SelectedAction { get; private set; }

    private void ConfigureContent()
    {
        Title = string.IsNullOrWhiteSpace(_options.Caption) ? "Message" : _options.Caption;
        TitleTextBlock.Text = Title;
        MessageTextBlock.Text = _options.Message ?? string.Empty;
        AutomationProperties.SetName(this, Title);

        if (_options.FlowDirection.HasValue)
            FlowDirection = _options.FlowDirection.Value;

        if (_options.AdditionalContent is not null)
        {
            AdditionalContentPresenter.Content = _options.AdditionalContent;
            AdditionalContentPresenter.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(_options.Details))
        {
            DetailsTextBox.Text = _options.Details;
            DetailsExpander.Visibility = Visibility.Visible;
        }

        // A close affordance is valid only when closing maps to a deterministic result.
        CloseButton.Visibility = _cancelAction is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void CreateButtons()
    {
        ButtonsPanel.Children.Clear();

        foreach (ModernMessageBoxAction action in _actions)
        {
            var button = new Button
            {
                Content = action.Text,
                IsDefault = action.IsDefault,
                Style = (Style)FindResource(action.IsDestructive
                    ? "DangerButtonStyle"
                    : action.IsDefault
                        ? "PrimaryButtonStyle"
                        : "SecondaryButtonStyle"),
                Tag = action
            };

            AutomationProperties.SetName(button, action.Text);
            button.Click += ActionButton_Click;
            ButtonsPanel.Children.Add(button);
        }
    }

    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ModernMessageBoxAction action })
            Complete(action);
    }

    private void Complete(ModernMessageBoxAction action)
    {
        if (_isCompleting)
            return;

        SelectedAction = action;
        _isCompleting = true;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cancelAction is not null)
            Complete(_cancelAction);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _cancelAction is not null)
        {
            e.Handled = true;
            Complete(_cancelAction);
            return;
        }

        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && _options.EnableCopyToClipboard)
        {
            e.Handled = true;
            CopyDialogText();
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isCompleting)
            return;

        if (_cancelAction is null)
        {
            e.Cancel = true;
            return;
        }

        SelectedAction = _cancelAction;
        _isCompleting = true;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyMonitorConstraints();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
            return;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The button state can change between the mouse event and DragMove.
        }
    }

    private void CopyDialogText()
    {
        var text = new StringBuilder()
            .AppendLine(Title)
            .AppendLine(new string('-', Math.Max(5, Title.Length)))
            .AppendLine(_options.Message);

        if (!string.IsNullOrWhiteSpace(_options.Details))
            text.AppendLine().AppendLine(_options.Details);

        text.AppendLine().Append(string.Join("   ", _actions.Select(action => $"[{action.Text}]")));

        try
        {
            Clipboard.SetText(text.ToString());
        }
        catch (ExternalException)
        {
            // Another process can temporarily own the clipboard. The dialog remains usable.
        }
    }

    private void ConfigureIcon(ModernMessageBoxIcon icon)
    {
        if (icon == ModernMessageBoxIcon.None)
        {
            IconBorder.Visibility = Visibility.Collapsed;
            MessageTextBlock.SetValue(Grid.ColumnProperty, 0);
            MessageTextBlock.SetValue(Grid.ColumnSpanProperty, 3);
            AdditionalContentPresenter.Margin = new Thickness(0, 14, 0, 0);
            DetailsExpander.Margin = new Thickness(0, 14, 0, 0);
            return;
        }

        (string geometry, string background, string foreground, string accessibleName) = icon switch
        {
            ModernMessageBoxIcon.Success =>
                ("M3,12 L9,18 L21,5 L18.8,3 L9,14.5 L5.2,10.2 Z", "SuccessSubtleBrush", "SuccessBrush", "Success"),
            ModernMessageBoxIcon.Warning =>
                ("M10.8,4 L13.2,4 L13.2,14 L10.8,14 Z M10.8,17 L13.2,17 L13.2,19.4 L10.8,19.4 Z", "WarningSubtleBrush", "WarningBrush", "Warning"),
            ModernMessageBoxIcon.Error =>
                ("M5.2,3.8 L12,10.6 L18.8,3.8 L20.2,5.2 L13.4,12 L20.2,18.8 L18.8,20.2 L12,13.4 L5.2,20.2 L3.8,18.8 L10.6,12 L3.8,5.2 Z", "DangerSubtleBrush", "DangerBrush", "Error"),
            ModernMessageBoxIcon.Question =>
                ("M8.2,8.4 C8.4,4.8 15.8,4.2 16.2,8.2 C16.4,10.2 15.2,11.2 13.6,12.2 C12.5,12.9 12.1,13.5 12.1,15 L10,15 C10,12.6 10.8,11.6 12.4,10.6 C13.6,9.8 14.2,9.3 14.1,8.4 C13.9,6.8 10.5,6.8 10.3,8.5 Z M10,17 L12.3,17 L12.3,19.3 L10,19.3 Z", "PrimarySubtleBrush", "PrimaryBrush", "Question"),
            _ =>
                ("M10.8,10 L13.2,10 L13.2,19 L10.8,19 Z M10.8,5 L13.2,5 L13.2,7.5 L10.8,7.5 Z", "PrimarySubtleBrush", "PrimaryBrush", "Information")
        };

        IconPath.Data = Geometry.Parse(geometry);
        IconBorder.SetResourceReference(BackgroundProperty, ResourcePrefix + background);
        IconPath.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, ResourcePrefix + foreground);
        AutomationProperties.SetName(IconBorder, accessibleName);
    }

    private void ApplyTheme()
    {
        if (SystemParameters.HighContrast)
        {
            Resources[ResourcePrefix + "SurfaceBrush"] = SystemColors.WindowBrush;
            Resources[ResourcePrefix + "SurfaceAltBrush"] = SystemColors.ControlBrush;
            Resources[ResourcePrefix + "ControlBrush"] = SystemColors.ControlBrush;
            Resources[ResourcePrefix + "ControlHoverBrush"] = SystemColors.HighlightBrush;
            Resources[ResourcePrefix + "ControlPressedBrush"] = SystemColors.HighlightBrush;
            Resources[ResourcePrefix + "BorderBrush"] = SystemColors.WindowTextBrush;
            Resources[ResourcePrefix + "BorderStrongBrush"] = SystemColors.WindowTextBrush;
            Resources[ResourcePrefix + "TextPrimaryBrush"] = SystemColors.WindowTextBrush;
            Resources[ResourcePrefix + "TextSecondaryBrush"] = SystemColors.WindowTextBrush;
            Resources[ResourcePrefix + "TextOnAccentBrush"] = SystemColors.HighlightTextBrush;
            Resources[ResourcePrefix + "PrimaryBrush"] = SystemColors.HighlightBrush;
            Resources[ResourcePrefix + "PrimaryHoverBrush"] = SystemColors.HighlightBrush;
            Resources[ResourcePrefix + "PrimaryPressedBrush"] = SystemColors.HighlightBrush;
            Resources[ResourcePrefix + "FocusBrush"] = SystemColors.HighlightBrush;
            return;
        }

        // Specific message-box keys win. Existing Aarohi AppBrush keys are the compatibility fallback.
        ApplyHostResource("SurfaceBrush", "AppBrush.Surface");
        ApplyHostResource("SurfaceAltBrush", "AppBrush.SurfaceAlt");
        ApplyHostResource("ControlBrush", "AppBrush.Control");
        ApplyHostResource("ControlHoverBrush", "AppBrush.ControlHover");
        ApplyHostResource("ControlPressedBrush", "AppBrush.ControlPressed");
        ApplyHostResource("BorderBrush", "AppBrush.Border");
        ApplyHostResource("BorderStrongBrush", "AppBrush.BorderStrong");
        ApplyHostResource("TextPrimaryBrush", "AppBrush.TextPrimary");
        ApplyHostResource("TextSecondaryBrush", "AppBrush.TextSecondary");
        ApplyHostResource("TextOnAccentBrush", "AppBrush.TextOnAccent");
        ApplyHostResource("PrimaryBrush", "AppBrush.Primary");
        ApplyHostResource("PrimaryHoverBrush", "AppBrush.PrimaryHover");
        ApplyHostResource("PrimaryPressedBrush", "AppBrush.PrimaryPressed");
        ApplyHostResource("PrimarySubtleBrush", "AppBrush.PrimarySubtle");
        ApplyHostResource("SuccessBrush", "AppBrush.Success");
        ApplyHostResource("SuccessSubtleBrush", "AppBrush.SuccessSubtle");
        ApplyHostResource("WarningBrush", "AppBrush.Warning");
        ApplyHostResource("WarningSubtleBrush", "AppBrush.WarningSubtle");
        ApplyHostResource("DangerBrush", "AppBrush.Danger");
        ApplyHostResource("DangerSubtleBrush", "AppBrush.DangerSubtle");
        ApplyHostResource("FocusBrush", "AppBrush.Focus");
    }

    private void ApplyHostResource(string localName, string compatibilityKey)
    {
        Application? application = Application.Current;
        if (application is null)
            return;

        string key = ResourcePrefix + localName;
        object? value = application.TryFindResource(key) ?? application.TryFindResource(compatibilityKey);
        if (value is not null)
            Resources[key] = value;
    }

    private void ApplyMonitorConstraints()
    {
        Rect workArea = GetMonitorWorkArea();
        MaxWidth = Math.Max(MinWidth, workArea.Width * 0.9);
        MaxHeight = Math.Max(300, workArea.Height * 0.9);
        ContentScroller.MaxHeight = Math.Max(120, MaxHeight - 190);
    }

    private Rect GetMonitorWorkArea()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
            return SystemParameters.WorkArea;

        IntPtr monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            return SystemParameters.WorkArea;

        double dpiScaleX = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double dpiScaleY = VisualTreeHelper.GetDpi(this).DpiScaleY;
        return new Rect(
            info.Work.Left / dpiScaleX,
            info.Work.Top / dpiScaleY,
            (info.Work.Right - info.Work.Left) / dpiScaleX,
            (info.Work.Bottom - info.Work.Top) / dpiScaleY);
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }
}
