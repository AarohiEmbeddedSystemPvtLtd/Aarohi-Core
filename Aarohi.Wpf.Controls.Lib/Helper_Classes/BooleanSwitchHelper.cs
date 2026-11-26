using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Aarohi.Classes;
using ExtendedTextBox = AarohiWpfControls.Controls.TextBox.ExtendedTextBox;

namespace Aarohi.Wpf.Controls.Lib.Helper_Classes;

public static class BooleanSwitchHelper
{
    public static void ApplyBitSwitches(
        string table,
        IReadOnlyList<DynamicClass.ColumnInfo> columns,
        IReadOnlyDictionary<string, FrameworkElement> inputs,
        FrameworkElement resourceHost)
    {
        foreach (DynamicClass.ColumnInfo column in columns.Where(IsBitColumn))
        {
            string key = $"{table}::{column.Name}";

            if (!inputs.TryGetValue(key, out FrameworkElement? existingInput))
            {
                continue;
            }

            ToggleButton toggle = existingInput as ToggleButton ?? CreateToggle(resourceHost, existingInput);
            toggle.IsChecked = ReadInitialValue(existingInput, column);

            if (!ReferenceEquals(existingInput, toggle))
            {
                ReplaceInput(existingInput, toggle);
            }

            if (inputs is System.Collections.IDictionary dictionary)
            {
                dictionary[key] = toggle;
            }
        }
    }

    private static ToggleButton CreateToggle(FrameworkElement resourceHost, FrameworkElement sourceInput)
    {
        ToggleButton toggle = new()
        {
            Style = resourceHost.TryFindResource("BitSwitchToggleStyle") as Style ?? Application.Current.TryFindResource("BitSwitchToggleStyle") as Style,
            Margin = sourceInput.Margin,
            HorizontalAlignment = sourceInput.HorizontalAlignment,
            VerticalAlignment = sourceInput.VerticalAlignment,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            MinWidth = 104
        };

        if (sourceInput is Control control)
        {
            toggle.IsEnabled = control.IsEnabled;
            toggle.ToolTip = control.ToolTip;
        }

        return toggle;
    }

    private static void ReplaceInput(FrameworkElement existingInput, ToggleButton toggle)
    {
        if (existingInput.Parent is Panel panel)
        {
            int index = panel.Children.IndexOf(existingInput);
            if (index >= 0)
            {
                panel.Children.RemoveAt(index);
                panel.Children.Insert(index, toggle);
                return;
            }
        }

        if (existingInput.Parent is Decorator decorator && ReferenceEquals(decorator.Child, existingInput))
        {
            decorator.Child = toggle;
        }
    }

    private static bool? ReadInitialValue(FrameworkElement input, DynamicClass.ColumnInfo column)
    {
        if (input is ToggleButton toggleButton)
        {
            return toggleButton.IsChecked;
        }

        if (input is CheckBox checkBox)
        {
            return checkBox.IsChecked;
        }

        string? text = input switch
        {
            ExtendedTextBox extendedTextBox => extendedTextBox.LeftText,
            TextBox textBox => textBox.Text,
            _ => input.Tag?.ToString()
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return column.DefaultValue is bool boolValue ? boolValue : null;
        }

        if (bool.TryParse(text, out bool parsedBool))
        {
            return parsedBool;
        }

        if (text == "1")
        {
            return true;
        }

        if (text == "0")
        {
            return false;
        }

        if (column.DefaultValue is bool defaultBool)
        {
            return defaultBool;
        }

        if (column.DefaultValue is string stringValue && bool.TryParse(stringValue, out bool parsedStringBool))
        {
            return parsedStringBool;
        }

        if (column.DefaultValue is string defaultString)
        {
            if (defaultString == "1")
            {
                return true;
            }

            if (defaultString == "0")
            {
                return false;
            }
        }

        return null;
    }

    private static bool IsBitColumn(DynamicClass.ColumnInfo column)
    {
        return string.Equals(column.DataType?.Trim(), "bit", System.StringComparison.OrdinalIgnoreCase);
    }
}
