using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AarohiWpfControls.Controls.DropDown;

public partial class GeneratedOptionDropDown : UserControl
{
    private bool _customMode;
    private bool _updating;
    private bool _suppressValueChanged;

    public GeneratedOptionDropDown()
    {
        InitializeComponent();
        Options = new ObservableCollection<string>();

        ControlChrome.SetResourceReference(Border.BackgroundProperty, "PanelBgBrush");
        ControlChrome.SetResourceReference(Border.BorderBrushProperty, "AppBrush.Border");
        PART_Combo.SetResourceReference(ComboBox.ForegroundProperty, "TextBrush");

        MouseEnter += (_, _) => UpdateChromeState();
        MouseLeave += (_, _) => UpdateChromeState();
        IsKeyboardFocusWithinChanged += (_, _) => UpdateChromeState();
        PART_Combo.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(Combo_TextChanged));
    }

    public event EventHandler? ValueChanged;

    public ObservableCollection<string> Options
    {
        get => (ObservableCollection<string>)GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public static readonly DependencyProperty OptionsProperty =
        DependencyProperty.Register(
            nameof(Options),
            typeof(ObservableCollection<string>),
            typeof(GeneratedOptionDropDown),
            new PropertyMetadata(new ObservableCollection<string>()));

    public string ValueText
    {
        get
        {
            if (IsCustomMode)
                return PART_Combo.Text?.Trim() ?? string.Empty;

            return Convert.ToString(PART_Combo.SelectedValue ?? PART_Combo.SelectedItem, CultureInfo.InvariantCulture)?.Trim()
                   ?? PART_Combo.Text?.Trim()
                   ?? string.Empty;
        }
    }

    public bool IsCustomMode
    {
        get => _customMode;
        private set
        {
            if (_customMode == value)
                return;

            _customMode = value;
            PART_Combo.IsEditable = value;
            PART_Combo.IsReadOnly = !value;
        }
    }

    public bool HasOtherOption => Options.Any(IsOtherText);

    public void SetOptions(IEnumerable<string?> options)
    {
        _updating = true;
        try
        {
            Options.Clear();

            foreach (string option in NormalizeOptions(options))
                Options.Add(option);
        }
        finally
        {
            _updating = false;
        }
    }

    public void SetInitialValue(object? value)
    {
        string text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        RunWithoutEvents(() =>
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                PART_Combo.SelectedIndex = -1;
                IsCustomMode = false;
                PART_Combo.Text = string.Empty;
                return;
            }

            int index = FindItemIndex(text);
            if (index >= 0)
            {
                IsCustomMode = false;
                PART_Combo.SelectedIndex = index;
                return;
            }

            if (HasOtherOption)
            {
                IsCustomMode = true;
                PART_Combo.SelectedIndex = -1;
                PART_Combo.Text = text;
                return;
            }

            IsCustomMode = false;
            PART_Combo.SelectedIndex = -1;
            PART_Combo.Text = string.Empty;
        });
    }

    private void Combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || _suppressValueChanged)
            return;

        string selected = Convert.ToString(PART_Combo.SelectedItem, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        if (IsOtherText(selected))
        {
            EnterCustomMode();
            RaiseValueChanged();
            return;
        }

        if (PART_Combo.SelectedItem != null)
        {
            IsCustomMode = false;
            PART_Combo.Text = selected;
        }

        RaiseValueChanged();
    }

    private void Combo_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updating && !_suppressValueChanged && IsCustomMode)
            RaiseValueChanged();
    }

    private void EnterCustomMode()
    {
        RunWithoutEvents(() =>
        {
            IsCustomMode = true;
            PART_Combo.SelectedIndex = -1;
            PART_Combo.Text = string.Empty;
            PART_Combo.IsDropDownOpen = false;
        });

        Dispatcher.BeginInvoke(new Action(() =>
        {
            PART_Combo.Focus();
            if (PART_Combo.Template.FindName("PART_EditableTextBox", PART_Combo) is System.Windows.Controls.TextBox editBox)
            {
                editBox.Focus();
                Keyboard.Focus(editBox);
            }
        }));
    }

    private void RaiseValueChanged()
    {
        if (!_updating && !_suppressValueChanged)
            ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RunWithoutEvents(Action action)
    {
        bool previous = _suppressValueChanged;
        _suppressValueChanged = true;
        try
        {
            action();
        }
        finally
        {
            _suppressValueChanged = previous;
        }
    }

    private int FindItemIndex(string value)
    {
        for (int i = 0; i < Options.Count; i++)
        {
            if (string.Equals(Options[i]?.Trim(), value, StringComparison.InvariantCultureIgnoreCase))
                return i;
        }

        return -1;
    }

    private void UpdateChromeState()
    {
        if (IsKeyboardFocusWithin)
        {
            ControlChrome.BorderThickness = new Thickness(1.5);
            ControlChrome.SetResourceReference(Border.BorderBrushProperty, "AppBrush.Primary");
            return;
        }

        ControlChrome.BorderThickness = new Thickness(1);
        ControlChrome.SetResourceReference(
            Border.BorderBrushProperty,
            IsMouseOver ? "AppBrush.BorderStrong" : "AppBrush.Border");
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

    public void SetSelectedIndex(int index)
    {
        RunWithoutEvents(() =>
        {
            if (index < 0 || index >= Options.Count)
            {
                PART_Combo.SelectedIndex = -1;
                IsCustomMode = false;
                PART_Combo.Text = string.Empty;
                return;
            }

            string selected = Options[index]?.Trim() ?? string.Empty;

            if (IsOtherText(selected))
            {
                IsCustomMode = true;
                PART_Combo.SelectedIndex = -1;
                PART_Combo.Text = string.Empty;
                return;
            }

            IsCustomMode = false;
            PART_Combo.SelectedIndex = index;
            PART_Combo.Text = selected;
        });
    }
}
