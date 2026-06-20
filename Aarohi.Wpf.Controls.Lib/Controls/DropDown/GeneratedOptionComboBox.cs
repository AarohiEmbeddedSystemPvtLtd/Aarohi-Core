using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AarohiWpfControls.Controls.DropDown;

public sealed class GeneratedOptionComboBox : ComboBox
{
    private bool _customMode;
    private bool _updating;

    public GeneratedOptionComboBox()
    {
        IsTextSearchEnabled = true;
        IsEditable = false;
        IsReadOnly = true;
        StaysOpenOnEdit = true;
        MaxDropDownHeight = 260;
        MinHeight = 38;
        BorderThickness = new Thickness(1);
        Padding = new Thickness(10, 0, 8, 0);
        VerticalContentAlignment = VerticalAlignment.Center;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;

        SetResourceReference(BackgroundProperty, "PanelBgBrush");
        SetResourceReference(ForegroundProperty, "TextBrush");
        SetResourceReference(BorderBrushProperty, "BorderBrush");
    }

    public bool HasOtherOption => Items
        .Cast<object?>()
        .Any(item => IsOtherText(Convert.ToString(item, CultureInfo.InvariantCulture)));

    public bool IsCustomMode
    {
        get => _customMode;
        private set
        {
            if (_customMode == value)
                return;

            _customMode = value;
            IsEditable = value;
            IsReadOnly = !value;
        }
    }

    public string ValueText
    {
        get
        {
            if (IsCustomMode)
                return Text?.Trim() ?? string.Empty;

            return Convert.ToString(SelectedValue ?? SelectedItem, CultureInfo.InvariantCulture)?.Trim()
                   ?? Text?.Trim()
                   ?? string.Empty;
        }
    }

    public void SetOptions(IEnumerable<string?> options)
    {
        _updating = true;
        try
        {
            Items.Clear();

            foreach (string value in NormalizeOptions(options))
                Items.Add(value);
        }
        finally
        {
            _updating = false;
        }
    }

    public void SetInitialValue(object? value)
    {
        string text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            SelectedIndex = -1;
            IsCustomMode = false;
            Text = string.Empty;
            return;
        }

        int index = FindItemIndex(text);
        if (index >= 0)
        {
            IsCustomMode = false;
            SelectedIndex = index;
            return;
        }

        if (HasOtherOption)
        {
            IsCustomMode = true;
            SelectedIndex = -1;
            Text = text;
            return;
        }

        IsCustomMode = false;
        SelectedIndex = -1;
        Text = string.Empty;
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);

        if (_updating)
            return;

        string selected = Convert.ToString(SelectedItem, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        if (IsOtherText(selected))
        {
            EnterCustomMode();
            return;
        }

        if (SelectedItem != null)
            IsCustomMode = false;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!IsCustomMode && e.Key != Key.Tab && e.Key != Key.Enter && e.Key != Key.Escape)
        {
            IsDropDownOpen = true;
        }

        base.OnPreviewKeyDown(e);
    }

    private void EnterCustomMode()
    {
        IsCustomMode = true;
        Text = string.Empty;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            Focus();
            if (Template.FindName("PART_EditableTextBox", this) is System.Windows.Controls.TextBox editBox)
                editBox.Focus();
        }));
    }

    private int FindItemIndex(string value)
    {
        for (int i = 0; i < Items.Count; i++)
        {
            string item = Convert.ToString(Items[i], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            if (string.Equals(item, value, StringComparison.InvariantCultureIgnoreCase))
                return i;
        }

        return -1;
    }

    private static bool IsOtherText(string? value)
    {
        return string.Compare(
            value?.Trim(),
            "Other",
            CultureInfo.InvariantCulture,
            CompareOptions.IgnoreCase) == 0;
    }

    private static IEnumerable<string> NormalizeOptions(IEnumerable<string?> options)
    {
        return (options ?? Enumerable.Empty<string?>())
            .Select(item => item?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.InvariantCultureIgnoreCase);
    }
}
