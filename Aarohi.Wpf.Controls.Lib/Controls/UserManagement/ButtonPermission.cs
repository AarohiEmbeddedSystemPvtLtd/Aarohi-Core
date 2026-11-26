using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aarohi.Wpf.Controls.Lib.Controls.UserManagement;

public static class ButtonPermission
{
    public static readonly DependencyProperty PermissionProperty =
        DependencyProperty.RegisterAttached(
            "Permission",
            typeof(object),
            typeof(ButtonPermission),
            new PropertyMetadata(null, OnPermissionPropertyChanged));

    public static readonly DependencyProperty RequiredPermissionsProperty =
        DependencyProperty.RegisterAttached(
            "RequiredPermissions",
            typeof(string),
            typeof(ButtonPermission),
            new PropertyMetadata(string.Empty, OnPermissionPropertyChanged));

    private static readonly DependencyProperty IsBehaviorAttachedProperty =
        DependencyProperty.RegisterAttached(
            "IsBehaviorAttached",
            typeof(bool),
            typeof(ButtonPermission),
            new PropertyMetadata(false));

    private static readonly DependencyProperty PermissionStateProperty =
        DependencyProperty.RegisterAttached(
            "PermissionState",
            typeof(PermissionState),
            typeof(ButtonPermission),
            new PropertyMetadata(null));

    private static bool permissionsChangedHandlerAttached;

    public static object? GetPermission(DependencyObject dependencyObject)
    {
        return dependencyObject.GetValue(PermissionProperty);
    }

    public static void SetPermission(DependencyObject dependencyObject, object? value)
    {
        dependencyObject.SetValue(PermissionProperty, value);
    }

    public static string GetRequiredPermissions(DependencyObject dependencyObject)
    {
        return (string)dependencyObject.GetValue(RequiredPermissionsProperty);
    }

    public static void SetRequiredPermissions(DependencyObject dependencyObject, string value)
    {
        dependencyObject.SetValue(RequiredPermissionsProperty, value);
    }

    private static bool GetIsBehaviorAttached(DependencyObject dependencyObject)
    {
        return (bool)dependencyObject.GetValue(IsBehaviorAttachedProperty);
    }

    private static void SetIsBehaviorAttached(DependencyObject dependencyObject, bool value)
    {
        dependencyObject.SetValue(IsBehaviorAttachedProperty, value);
    }

    private static PermissionState? GetPermissionState(DependencyObject dependencyObject)
    {
        return (PermissionState?)dependencyObject.GetValue(PermissionStateProperty);
    }

    private static void SetPermissionState(DependencyObject dependencyObject, PermissionState? value)
    {
        dependencyObject.SetValue(PermissionStateProperty, value);
    }

    private static void OnPermissionPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is Button button)
        {
            AttachBehavior(button);
            Apply(button);
        }
    }

    private static void AttachBehavior(Button button)
    {
        if (GetIsBehaviorAttached(button))
        {
            return;
        }

        button.Loaded += Button_Loaded;
        if (!permissionsChangedHandlerAttached)
        {
            AppSession.Current.PermissionsChanged += AppSession_PermissionsChanged;
            permissionsChangedHandlerAttached = true;
        }

        SetIsBehaviorAttached(button, true);
    }

    private static void Button_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            Apply(button);
        }
    }

    private static void AppSession_PermissionsChanged(object? sender, EventArgs e)
    {
        if (sender is not AppSession || Application.Current == null)
        {
            return;
        }

        foreach (Window window in Application.Current.Windows)
        {
            ApplyPermissionsInVisualTree(window);
        }
    }

    private static void ApplyPermissionsInVisualTree(DependencyObject root)
    {
        if (root is Button button && GetIsBehaviorAttached(button))
        {
            Apply(button);
        }

        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childCount; i++)
        {
            ApplyPermissionsInVisualTree(VisualTreeHelper.GetChild(root, i));
        }
    }

    private static void Apply(Button button)
    {
        bool allowed = HasRequiredPermission(button);

        if (allowed)
        {
            RestoreContent(button);
            button.IsEnabled = true;
            return;
        }

        ShowDeniedContent(button);
        button.IsEnabled = false;
    }

    private static bool HasRequiredPermission(Button button)
    {
        string requiredPermissions = GetRequiredPermissions(button);
        if (!string.IsNullOrWhiteSpace(requiredPermissions))
        {
            string[] permissionNames = requiredPermissions.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string permissionName in permissionNames)
            {
                if (AppSession.Current.HasPermission(permissionName.Trim()))
                {
                    return true;
                }
            }

            return false;
        }

        object? permissionObj = GetPermission(button);
        if (permissionObj == null)
        {
            return true;
        }

        string permissionStr = permissionObj.ToString() ?? string.Empty;
        return AppSession.Current.HasPermission(permissionStr);
    }

    private static void RestoreContent(Button button)
    {
        PermissionState? state = GetPermissionState(button);
        if (state?.OriginalContent is not null)
        {
            button.Content = state.OriginalContent;
        }

        SetPermissionState(button, null);
    }

    private static void ShowDeniedContent(Button button)
    {
        PermissionState? state = GetPermissionState(button);
        if (state?.OriginalContent is null)
        {
            state = new PermissionState
            {
                OriginalContent = button.Content
            };
            SetPermissionState(button, state);
        }

        button.Content = CreateDeniedContent(state.OriginalContent ?? button.Content);
    }

    private static Grid CreateDeniedContent(object originalContent)
    {
        Grid wrapper = new();

        ContentPresenter originalPresenter = new()
        {
            Content = originalContent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.38
        };

        Border lockBadge = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 2, 6, 2),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(24, 0, 0, 0)),
            Child = new TextBlock
            {
                Text = "\uE72E",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 15,
                FontWeight = FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        lockBadge.SetResourceReference(Border.BackgroundProperty, "AppBrush.SurfaceAlt");
        if (lockBadge.Child is TextBlock lockText)
        {
            lockText.SetResourceReference(TextBlock.ForegroundProperty, "AppBrush.TextMuted");
        }

        wrapper.Children.Add(originalPresenter);
        wrapper.Children.Add(lockBadge);
        Panel.SetZIndex(lockBadge, 1);

        return wrapper;
    }

    private sealed class PermissionState
    {
        public object? OriginalContent { get; set; }
    }
}
