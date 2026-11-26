using System.Reflection;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Aarohi.Classes;

namespace Aarohi.Wpf.Controls.Lib.Helper_Classes;

public static class InputPlaceholderHelper
{
    private static readonly DependencyProperty IsAttachedProperty =
        DependencyProperty.RegisterAttached(
            "IsAttached",
            typeof(bool),
            typeof(InputPlaceholderHelper),
            new PropertyMetadata(false));

    private static readonly DependencyProperty IsSuppressedProperty =
        DependencyProperty.RegisterAttached(
            "IsSuppressed",
            typeof(bool),
            typeof(InputPlaceholderHelper),
            new PropertyMetadata(false));

    private static readonly DependencyProperty AdornerProperty =
        DependencyProperty.RegisterAttached(
            "Adorner",
            typeof(PlaceholderAdorner),
            typeof(InputPlaceholderHelper),
            new PropertyMetadata(null));

    public static void ApplyPlaceholders(
        string table,
        IEnumerable<DynamicClass.ColumnInfo> columns,
        IReadOnlyDictionary<string, FrameworkElement> inputs)
    {
        foreach (DynamicClass.ColumnInfo column in columns)
        {
            if (!inputs.TryGetValue($"{table}::{column.Name}", out FrameworkElement? input))
            {
                continue;
            }

            PlaceholderKind kind = ResolvePlaceholderKind(column);
            if (kind == PlaceholderKind.None)
            {
                continue;
            }

            string placeholderText = GetPlaceholderText(kind);
            AttachPlaceholder(input, placeholderText);
        }
    }

    private static void AttachPlaceholder(FrameworkElement input, string placeholderText)
    {
        if ((bool)input.GetValue(IsAttachedProperty))
        {
            return;
        }

        input.SetValue(IsAttachedProperty, true);

        string textPropertyName = ResolveTextPropertyName(input.GetType());
        if (string.IsNullOrWhiteSpace(textPropertyName))
        {
            return;
        }

        RoutedEventHandler? loadedHandler = null;
        RoutedEventHandler? unloadedHandler = null;
        PlaceholderAdorner? adorner = null;

        void EnsureAdorner()
        {
            if (adorner is not null)
            {
                adorner.UpdateText(GetCurrentText(input, textPropertyName));
                return;
            }

            AdornerLayer? layer = AdornerLayer.GetAdornerLayer(input);
            if (layer is null)
            {
                return;
            }

            adorner = new PlaceholderAdorner(input, placeholderText);
            layer.Add(adorner);
            adorner.SetSuppressed((bool)input.GetValue(IsSuppressedProperty));
            input.SetValue(AdornerProperty, adorner);
            adorner.UpdateText(GetCurrentText(input, textPropertyName));
        }

        loadedHandler = (_, _) => EnsureAdorner();
        unloadedHandler = (_, _) =>
        {
            if (adorner is not null)
            {
                adorner.Remove();
                adorner = null;
                input.ClearValue(AdornerProperty);
            }
        };

        input.Loaded += loadedHandler;
        input.Unloaded += unloadedHandler;

        TryWatchTextProperty(input, textPropertyName, () =>
        {
            if (adorner is not null)
            {
                adorner.UpdateText(GetCurrentText(input, textPropertyName));
            }
        });

        if (input.IsLoaded)
        {
            EnsureAdorner();
        }
    }

    internal static void SetPlaceholdersSuppressed(DependencyObject root, bool isSuppressed)
    {
        if (root is FrameworkElement element)
        {
            element.SetValue(IsSuppressedProperty, isSuppressed);

            if (element.GetValue(AdornerProperty) is PlaceholderAdorner adorner)
            {
                adorner.SetSuppressed(isSuppressed);
            }
        }

        int childrenCount = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < childrenCount; index++)
        {
            SetPlaceholdersSuppressed(VisualTreeHelper.GetChild(root, index), isSuppressed);
        }
    }

    private static void TryWatchTextProperty(FrameworkElement input, string propertyName, Action onChanged)
    {
        PropertyInfo? property = input.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property is null)
        {
            return;
        }

        DependencyProperty? dependencyProperty = GetDependencyProperty(input.GetType(), propertyName);
        if (dependencyProperty is not null)
        {
            DependencyPropertyDescriptor.FromProperty(dependencyProperty, input.GetType())
                .AddValueChanged(input, (_, _) => onChanged());
            return;
        }

        if (input is TextBox textBox)
        {
            textBox.TextChanged += (_, _) => onChanged();
        }
    }

    private static DependencyProperty? GetDependencyProperty(Type type, string propertyName)
    {
        FieldInfo? field = type.GetField(propertyName + "Property", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        return field?.GetValue(null) as DependencyProperty;
    }

    private static string GetCurrentText(FrameworkElement input, string propertyName)
    {
        PropertyInfo? property = input.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property?.GetValue(input) is string text)
        {
            return text;
        }

        return string.Empty;
    }

    private static string ResolveTextPropertyName(Type controlType)
    {
        string[] candidates =
        {
            "LeftText",
            "Text",
            "InputValue"
        };

        foreach (string candidate in candidates)
        {
            if (GetDependencyProperty(controlType, candidate) is not null || controlType.GetProperty(candidate) is not null)
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static PlaceholderKind ResolvePlaceholderKind(DynamicClass.ColumnInfo column)
    {
        string dataType = (column.DataType ?? string.Empty).Trim().ToLowerInvariant();
        string columnName = $"{column.Name} {column.DisplayName}".Trim().ToLowerInvariant();

        if (IsNumericType(dataType))
        {
            return PlaceholderKind.Numeric;
        }

        if (LooksLikeAlphaOnly(columnName))
        {
            return PlaceholderKind.Alpha;
        }

        if (LooksLikeAlphaNumeric(columnName) || IsStringType(dataType))
        {
            return PlaceholderKind.AlphaNumeric;
        }

        return PlaceholderKind.None;
    }

    private static bool IsNumericType(string dataType)
    {
        return dataType is "tinyint" or "smallint" or "int" or "bigint" or "decimal" or "numeric" or "money" or "smallmoney" or "float" or "real";
    }

    private static bool IsStringType(string dataType)
    {
        return dataType is "char" or "nchar" or "varchar" or "nvarchar" or "text" or "ntext" or "xml" or "uniqueidentifier" or "date" or "datetime" or "datetime2" or "smalldatetime";
    }

    private static bool LooksLikeAlphaOnly(string text)
    {
        return text.Contains("name")
               || text.Contains("description")
               || text.Contains("remark")
               || text.Contains("note")
               || text.Contains("title")
               || text.Contains("label")
               || text.Contains("comment");
    }

    private static bool LooksLikeAlphaNumeric(string text)
    {
        return text.Contains("code")
               || text.Contains("id")
               || text.Contains("no")
               || text.Contains("number")
               || text.Contains("serial")
               || text.Contains("model")
               || text.Contains("tag")
               || text.Contains("station")
               || text.Contains("ref");
    }

    private static string GetPlaceholderText(PlaceholderKind kind)
    {
        return kind switch
        {
            PlaceholderKind.Numeric => "e.g. 1234",
            PlaceholderKind.AlphaNumeric => "e.g. AB123",
            PlaceholderKind.Alpha => "e.g. Motor",
            _ => string.Empty
        };
    }

    private enum PlaceholderKind
    {
        None,
        Numeric,
        AlphaNumeric,
        Alpha
    }

    private sealed class PlaceholderAdorner : Adorner
    {
        private readonly TextBlock textBlock;
        private readonly VisualCollection visuals;
        private bool isSuppressed;
        private string currentText = string.Empty;

        public PlaceholderAdorner(FrameworkElement adornedElement, string placeholderText)
            : base(adornedElement)
        {
            IsHitTestVisible = false;
            visuals = new VisualCollection(this);

            textBlock = new TextBlock
            {
                Text = placeholderText,
                Margin = new Thickness(12, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Opacity = 0.9,
                IsHitTestVisible = false
            };
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "AppBrush.TextSecondary");

            visuals.Add(textBlock);
        }

        public void UpdateText(string currentText)
        {
            this.currentText = currentText;
            RefreshVisibility();
            InvalidateMeasure();
            InvalidateVisual();
        }

        public void SetSuppressed(bool value)
        {
            isSuppressed = value;
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            Visibility = !isSuppressed && string.IsNullOrWhiteSpace(currentText) ? Visibility.Visible : Visibility.Collapsed;
        }

        public void Remove()
        {
            if (Parent is AdornerLayer layer)
            {
                layer.Remove(this);
            }
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index)
        {
            if (index != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return textBlock;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            textBlock.Measure(AdornedElement.RenderSize);
            return AdornedElement.RenderSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            textBlock.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}
