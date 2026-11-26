using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Aarohi.Wpf.Controls.Lib.Controls.MessageBox;

public enum ModernMessageBoxButton
{
    OK,
    OKCancel,
    YesNo,
    YesNoCancel,
    RetryCancel,
    AbortRetryIgnore
}

public enum ModernMessageBoxResult
{
    None,
    OK,
    Cancel,
    Yes,
    No,
    Retry,
    Abort,
    Ignore
}

public enum ModernMessageBoxIcon
{
    None,
    Information,
    Success,
    Warning,
    Error,
    Question,
    Info = Information
}

public static class ModernMessageBoxButtons
{
    public static List<string> OK => new() { "OK" };
    public static List<string> OKCancel => new() { "Cancel", "OK" };
    public static List<string> YesNo => new() { "No", "Yes" };
    public static List<string> YesNoCancel => new() { "Cancel", "No", "Yes" };
    public static List<string> Custom(params string[] buttons)
        => buttons.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
}

public static class ModernMessageBoxResults
{
    public static List<string> OK => new() { "OK" };
    public static List<string> OKCancel => new() { "Cancel", "OK" };
    public static List<string> YesNo => new() { "No", "Yes" };
    public static List<string> YesNoCancel => new() { "Cancel", "No", "Yes" };
    public static List<string> Custom(params string[] results)
        => results.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
}

/// <summary>Describes one button in a custom message box.</summary>
public sealed class ModernMessageBoxAction
{
    public ModernMessageBoxAction(
        string text,
        string resultKey,
        bool isDefault = false,
        bool isCancel = false,
        bool isDestructive = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Button text cannot be empty.", nameof(text));
        if (string.IsNullOrWhiteSpace(resultKey))
            throw new ArgumentException("Result key cannot be empty.", nameof(resultKey));

        Text = text.Trim();
        ResultKey = resultKey.Trim();
        IsDefault = isDefault;
        IsCancel = isCancel;
        IsDestructive = isDestructive;
    }

    public string Text { get; }
    public string ResultKey { get; }
    public bool IsDefault { get; }
    public bool IsCancel { get; }
    public bool IsDestructive { get; }

    internal ModernMessageBoxResult StandardResult { get; init; }
}

/// <summary>Advanced configuration for a standard message box.</summary>
public sealed class ModernMessageBoxOptions
{
    public Window? Owner { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Caption { get; set; } = "Message";
    public ModernMessageBoxButton Buttons { get; set; } = ModernMessageBoxButton.OK;
    public ModernMessageBoxIcon Icon { get; set; } = ModernMessageBoxIcon.Information;
    public ModernMessageBoxResult DefaultResult { get; set; } = ModernMessageBoxResult.None;
    public ModernMessageBoxResult CancelResult { get; set; } = ModernMessageBoxResult.None;
    public string? Details { get; set; }
    public UIElement? AdditionalContent { get; set; }
    public FlowDirection? FlowDirection { get; set; }
    public bool EnableCopyToClipboard { get; set; } = true;
}

/// <summary>
/// Displays the Aarohi modern modal message box. The visual design is derived
/// from the IMTS Type Test message box and is self-contained for library use.
/// </summary>
public static class ModernMessageBox
{
    public static ModernMessageBoxResult Show(string message)
        => Show(message, "Message");

    public static ModernMessageBoxResult Show(
        string message,
        string caption,
        ModernMessageBoxButton buttons = ModernMessageBoxButton.OK,
        ModernMessageBoxIcon icon = ModernMessageBoxIcon.Information,
        ModernMessageBoxResult defaultResult = ModernMessageBoxResult.None)
        => Show(new ModernMessageBoxOptions
        {
            Message = message ?? string.Empty,
            Caption = caption ?? string.Empty,
            Buttons = buttons,
            Icon = icon,
            DefaultResult = defaultResult
        });

    public static ModernMessageBoxResult Show(
        Window owner,
        string message,
        string caption = "Message",
        ModernMessageBoxButton buttons = ModernMessageBoxButton.OK,
        ModernMessageBoxIcon icon = ModernMessageBoxIcon.Information,
        ModernMessageBoxResult defaultResult = ModernMessageBoxResult.None)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return Show(new ModernMessageBoxOptions
        {
            Owner = owner,
            Message = message ?? string.Empty,
            Caption = caption ?? string.Empty,
            Buttons = buttons,
            Icon = icon,
            DefaultResult = defaultResult
        });
    }

    public static string Show(
        string message,
        string caption,
        List<string> buttons,
        List<string>? results = null,
        ModernMessageBoxIcon icon = ModernMessageBoxIcon.Information,
        UIElement? extraControl = null)
    {
        List<string> bList = buttons != null && buttons.Count > 0
            ? buttons.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList()
            : ModernMessageBoxButtons.OK;
        List<string> rList = results != null && results.Count > 0
            ? results.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList()
            : new List<string>(bList);

        var actions = new List<ModernMessageBoxAction>();
        for (int i = 0; i < bList.Count; i++)
        {
            string btnText = bList[i];
            string resKey = (rList.Count > i) ? rList[i] : btnText;
            bool isDef = (i == bList.Count - 1);
            bool isCanc = string.Equals(resKey, "Cancel", StringComparison.OrdinalIgnoreCase);
            actions.Add(new ModernMessageBoxAction(btnText, resKey, isDefault: isDef, isCancel: isCanc));
        }

        return ShowCustom(message, caption, actions, icon, null, null, extraControl) ?? string.Empty;
    }

    public static string Show(
        string message,
        List<string>? buttons = null,
        List<string>? results = null,
        ModernMessageBoxIcon icon = ModernMessageBoxIcon.Information,
        UIElement? extraControl = null,
        string? title = null)
        => Show(message, title ?? "Message", buttons ?? ModernMessageBoxButtons.OK, results, icon, extraControl);

    public static ModernMessageBoxResult Show(ModernMessageBoxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateAdditionalContent(options.AdditionalContent);

        IReadOnlyList<ModernMessageBoxAction> actions = CreateStandardActions(options);
        ModernMessageBoxAction? selected = ShowCore(options, actions);
        return selected?.StandardResult ?? ModernMessageBoxResult.None;
    }

    public static string? ShowCustom(
        string message,
        string caption,
        IEnumerable<ModernMessageBoxAction> actions,
        ModernMessageBoxIcon icon = ModernMessageBoxIcon.Information,
        Window? owner = null,
        string? details = null,
        UIElement? additionalContent = null)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ValidateAdditionalContent(additionalContent);

        List<ModernMessageBoxAction> actionList = actions.ToList();
        ValidateCustomActions(actionList);

        var options = new ModernMessageBoxOptions
        {
            Owner = owner,
            Message = message ?? string.Empty,
            Caption = caption ?? string.Empty,
            Icon = icon,
            Details = details,
            AdditionalContent = additionalContent
        };

        return ShowCore(options, actionList)?.ResultKey;
    }

    private static ModernMessageBoxAction? ShowCore(
        ModernMessageBoxOptions options,
        IReadOnlyList<ModernMessageBoxAction> actions)
    {
        Dispatcher dispatcher = options.Owner?.Dispatcher
            ?? Application.Current?.Dispatcher
            ?? Dispatcher.CurrentDispatcher;

        if (!dispatcher.CheckAccess())
        {
            return dispatcher.Invoke(
                () => ShowCore(options, actions),
                DispatcherPriority.Send);
        }

        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException(
                "ModernMessageBox must be displayed on an STA UI thread.");
        }

        Window? owner = ResolveOwner(options.Owner);
        var window = new ModernMessageBoxWindow(options, actions);

        if (owner is not null && owner != window)
            window.Owner = owner;

        window.ShowDialog();
        return window.SelectedAction;
    }

    private static Window? ResolveOwner(Window? requestedOwner)
    {
        if (requestedOwner is { IsLoaded: true })
            return requestedOwner;

        Application? application = Application.Current;
        if (application is null)
            return null;

        Window? active = application.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive && window.IsVisible);

        if (active is not null)
            return active;

        return application.MainWindow is { IsLoaded: true }
            ? application.MainWindow
            : null;
    }

    private static IReadOnlyList<ModernMessageBoxAction> CreateStandardActions(
        ModernMessageBoxOptions options)
    {
        ModernMessageBoxResult defaultResult = options.DefaultResult == ModernMessageBoxResult.None
            ? GetDefaultResult(options.Buttons)
            : options.DefaultResult;

        ModernMessageBoxResult cancelResult = options.CancelResult == ModernMessageBoxResult.None
            ? GetCancelResult(options.Buttons)
            : options.CancelResult;

        ModernMessageBoxResult[] results = options.Buttons switch
        {
            ModernMessageBoxButton.OK => [ModernMessageBoxResult.OK],
            ModernMessageBoxButton.OKCancel => [ModernMessageBoxResult.Cancel, ModernMessageBoxResult.OK],
            ModernMessageBoxButton.YesNo => [ModernMessageBoxResult.No, ModernMessageBoxResult.Yes],
            ModernMessageBoxButton.YesNoCancel =>
                [ModernMessageBoxResult.Cancel, ModernMessageBoxResult.No, ModernMessageBoxResult.Yes],
            ModernMessageBoxButton.RetryCancel => [ModernMessageBoxResult.Cancel, ModernMessageBoxResult.Retry],
            ModernMessageBoxButton.AbortRetryIgnore =>
                [ModernMessageBoxResult.Abort, ModernMessageBoxResult.Ignore, ModernMessageBoxResult.Retry],
            _ => throw new ArgumentOutOfRangeException(nameof(options.Buttons))
        };

        if (!results.Contains(defaultResult))
            throw new ArgumentException("DefaultResult must be one of the displayed buttons.", nameof(options));
        if (cancelResult != ModernMessageBoxResult.None && !results.Contains(cancelResult))
            throw new ArgumentException("CancelResult must be one of the displayed buttons.", nameof(options));

        return results.Select(result => new ModernMessageBoxAction(
            GetButtonText(result),
            result.ToString(),
            isDefault: result == defaultResult,
            isCancel: result == cancelResult)
        {
            StandardResult = result
        }).ToArray();
    }

    private static ModernMessageBoxResult GetDefaultResult(ModernMessageBoxButton buttons)
        => buttons switch
        {
            ModernMessageBoxButton.OK => ModernMessageBoxResult.OK,
            ModernMessageBoxButton.OKCancel => ModernMessageBoxResult.OK,
            ModernMessageBoxButton.YesNo => ModernMessageBoxResult.Yes,
            ModernMessageBoxButton.YesNoCancel => ModernMessageBoxResult.Yes,
            ModernMessageBoxButton.RetryCancel => ModernMessageBoxResult.Retry,
            ModernMessageBoxButton.AbortRetryIgnore => ModernMessageBoxResult.Retry,
            _ => ModernMessageBoxResult.None
        };

    private static ModernMessageBoxResult GetCancelResult(ModernMessageBoxButton buttons)
        => buttons switch
        {
            ModernMessageBoxButton.OK => ModernMessageBoxResult.OK,
            ModernMessageBoxButton.OKCancel => ModernMessageBoxResult.Cancel,
            ModernMessageBoxButton.YesNoCancel => ModernMessageBoxResult.Cancel,
            ModernMessageBoxButton.RetryCancel => ModernMessageBoxResult.Cancel,
            _ => ModernMessageBoxResult.None
        };

    private static string GetButtonText(ModernMessageBoxResult result)
        => result switch
        {
            ModernMessageBoxResult.OK => "OK",
            ModernMessageBoxResult.Cancel => "Cancel",
            ModernMessageBoxResult.Yes => "Yes",
            ModernMessageBoxResult.No => "No",
            ModernMessageBoxResult.Retry => "Retry",
            ModernMessageBoxResult.Abort => "Abort",
            ModernMessageBoxResult.Ignore => "Ignore",
            _ => result.ToString()
        };

    private static void ValidateCustomActions(IReadOnlyList<ModernMessageBoxAction> actions)
    {
        if (actions.Count == 0)
            throw new ArgumentException("At least one action is required.", nameof(actions));
        if (actions.Count(action => action.IsDefault) > 1)
            throw new ArgumentException("Only one action can be the default action.", nameof(actions));
        if (actions.Count(action => action.IsCancel) > 1)
            throw new ArgumentException("Only one action can be the cancel action.", nameof(actions));
        if (actions.Select(action => action.ResultKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != actions.Count)
            throw new ArgumentException("Custom action result keys must be unique.", nameof(actions));
    }

    private static void ValidateAdditionalContent(UIElement? content)
    {
        if (content is FrameworkElement { Parent: not null })
        {
            throw new ArgumentException(
                "AdditionalContent is already attached to another visual parent.",
                nameof(content));
        }
    }
}
