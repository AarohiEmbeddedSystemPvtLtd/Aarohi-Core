using Aarohi.Classes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Aarohi.Wpf.Controls.Lib.Controls.TextBox
{
    /// <summary>
    /// Interaction logic for ExtendedTextBox.xaml
    /// </summary>
    public partial class ExtendedTextBox : UserControl
    {
        // -------------------------------------------------
        // Dependency Properties (Allow Binding in WPF)
        // -------------------------------------------------

        public static readonly DependencyProperty LeftTextProperty =
            DependencyProperty.Register(nameof(LeftText), typeof(string), typeof(ExtendedTextBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLeftTextChanged));

        public string LeftText
        {
            get => (string)GetValue(LeftTextProperty);
            set => SetValue(LeftTextProperty, value);
        }

        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items), typeof(ObservableCollection<string>), typeof(ExtendedTextBox),
                new PropertyMetadata(null, OnItemsChanged));

        public ObservableCollection<string> Items
        {
            get => (ObservableCollection<string>)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public static readonly DependencyProperty LeftWidthProperty =
            DependencyProperty.Register(nameof(LeftWidth), typeof(int), typeof(ExtendedTextBox),
                new FrameworkPropertyMetadata(100, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutChanged));

        public int LeftWidth
        {
            get => (int)GetValue(LeftWidthProperty);
            set => SetValue(LeftWidthProperty, value);
        }

        public static readonly DependencyProperty RightWidthProperty =
            DependencyProperty.Register(nameof(RightWidth), typeof(int), typeof(ExtendedTextBox),
                new FrameworkPropertyMetadata(90, FrameworkPropertyMetadataOptions.AffectsMeasure, OnLayoutChanged));

        public int RightWidth
        {
            get => (int)GetValue(RightWidthProperty);
            set => SetValue(RightWidthProperty, value);
        }

        public static readonly DependencyProperty UseRightWidthProperty =
            DependencyProperty.Register(nameof(UseRightWidth), typeof(bool), typeof(ExtendedTextBox),
                new PropertyMetadata(true, OnLayoutChanged));

        public bool UseRightWidth
        {
            get => (bool)GetValue(UseRightWidthProperty);
            set => SetValue(UseRightWidthProperty, value);
        }

        public static readonly DependencyProperty LeftEditableProperty =
            DependencyProperty.Register(nameof(LeftEditable), typeof(bool), typeof(ExtendedTextBox),
                new PropertyMetadata(true, (d, e) => ((ExtendedTextBox)d).txtLeft.IsReadOnly = !(bool)e.NewValue));

        public bool LeftEditable
        {
            get => (bool)GetValue(LeftEditableProperty);
            set => SetValue(LeftEditableProperty, value);
        }

        public static readonly DependencyProperty LeftNumericOnlyProperty =
            DependencyProperty.Register(nameof(LeftNumericOnly), typeof(bool), typeof(ExtendedTextBox), new PropertyMetadata(false));

        public bool LeftNumericOnly
        {
            get => (bool)GetValue(LeftNumericOnlyProperty);
            set => SetValue(LeftNumericOnlyProperty, value);
        }

        public static readonly DependencyProperty LeftAllowDecimalProperty =
            DependencyProperty.Register(nameof(LeftAllowDecimal), typeof(bool), typeof(ExtendedTextBox), new PropertyMetadata(true));

        public bool LeftAllowDecimal
        {
            get => (bool)GetValue(LeftAllowDecimalProperty);
            set => SetValue(LeftAllowDecimalProperty, value);
        }

        public bool AutoConvertOnUnitChange { get; set; } = true;
        public bool ShowConversionErrorMessageBox { get; set; } = true;
        public string ConversionErrorTitle { get; set; } = "Unit Conversion Error";
        public string? QuantityName { get; set; }
        public string? DefaultUnit { get; set; }
        public string? ParameterName { get; set; }
        public string NumberFormat { get; set; } = "0.###";

        // State
        private bool _suppressTextChanged;
        private bool _suppressComboChanged;
        private bool _autoConverting;
        private bool _bulkItemsUpdate;
        private int _lastGoodSelectedIndex = -1;
        private string? _previousUnit;

        // Events
        public event EventHandler? LeftTextChanged;
        public event EventHandler? SelectedIndexChanged;
        public event EventHandler? ValuePairChanged;

        public ExtendedTextBox()
        {
            InitializeComponent();
            Items = new ObservableCollection<string>();
        }

        // -------------------------------------------------
        // DP Callbacks
        // -------------------------------------------------

        private static void OnLeftTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (ExtendedTextBox)d;
            ctrl._suppressTextChanged = true;
            ctrl.txtLeft.Text = (string)e.NewValue ?? string.Empty;
            ctrl._suppressTextChanged = false;
            ctrl.LeftTextChanged?.Invoke(ctrl, EventArgs.Empty);
            ctrl.ValuePairChanged?.Invoke(ctrl, EventArgs.Empty);
        }

        private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ExtendedTextBox)d).SyncComboFromItems();
        }

        private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ExtendedTextBox)d).UpdateLayoutParts();
        }

        // -------------------------------------------------
        // Layout Logic
        // -------------------------------------------------

        private void UpdateLayoutParts()
        {
            if (Items == null || Items.Count == 0)
            {
                ColLeft.Width = new GridLength(1, GridUnitType.Star);
                ColRight.Width = new GridLength(0);
                cmbRight.Visibility = Visibility.Collapsed;
                return;
            }

            cmbRight.Visibility = Visibility.Visible;
            if (UseRightWidth)
            {
                ColRight.Width = new GridLength(RightWidth);
                ColLeft.Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                ColLeft.Width = new GridLength(LeftWidth);
                ColRight.Width = new GridLength(1, GridUnitType.Star);
            }
        }

        // -------------------------------------------------
        // Logic Ported from WinForms
        // -------------------------------------------------

        private void SyncComboFromItems()
        {
            string? oldSelected = SelectedItem;
            _suppressComboChanged = true;

            cmbRight.ItemsSource = null;
            cmbRight.ItemsSource = Items;

            if (Items == null || Items.Count == 0)
            {
                cmbRight.SelectedIndex = -1;
                _lastGoodSelectedIndex = -1;
                _previousUnit = null;
            }
            else
            {
                int idx = -1;
                if (!string.IsNullOrWhiteSpace(DefaultUnit))
                    idx = FindUnitIndex(DefaultUnit);

                if (idx < 0 && !string.IsNullOrWhiteSpace(oldSelected))
                    idx = FindUnitIndex(oldSelected);

                if (idx < 0) idx = 0;

                cmbRight.SelectedIndex = idx;
                _lastGoodSelectedIndex = idx;
                _previousUnit = SelectedItem;
            }

            _suppressComboChanged = false;
            UpdateLayoutParts();
            ValuePairChanged?.Invoke(this, EventArgs.Empty);
        }

        private int FindUnitIndex(string? unit)
        {
            if (string.IsNullOrWhiteSpace(unit)) return -1;
            for (int i = 0; i < (Items?.Count ?? 0); i++)
            {
                if (string.Equals(Items[i].Trim(), unit.Trim(), StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        public string? SelectedItem => cmbRight.SelectedItem as string;
        public int SelectedIndex
        {
            get => cmbRight.SelectedIndex;
            set => cmbRight.SelectedIndex = value;
        }

        public void SetUnits(IEnumerable<string>? units)
        {
            _bulkItemsUpdate = true;
            Items.Clear();
            if (units != null)
            {
                foreach (var u in units.Where(x => !string.IsNullOrWhiteSpace(x)))
                    Items.Add(u.Trim());
            }
            _bulkItemsUpdate = false;
            SyncComboFromItems();
        }

        // -------------------------------------------------
        // Input Handling
        // -------------------------------------------------

        private void TxtLeft_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_suppressTextChanged) return;
            LeftText = txtLeft.Text; // Sync DP
            LeftTextChanged?.Invoke(this, EventArgs.Empty);
            ValuePairChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TxtLeft_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!LeftNumericOnly) return;

            char dec = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
            bool isDigit = char.IsDigit(e.Text, 0);
            bool isDecimal = LeftAllowDecimal && (e.Text == "." || e.Text == dec.ToString());

            if (!isDigit && !isDecimal)
            {
                e.Handled = true;
                return;
            }

            if (isDecimal && txtLeft.Text.Contains(dec))
            {
                e.Handled = true;
            }
        }

        private void CmbRight_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressComboChanged) return;

            string? oldUnit = _previousUnit;
            string? newUnit = SelectedItem;

            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);

            if (AutoConvertOnUnitChange)
            {
                if (!TryConvertLeftTextOnUnitChange_WithResult(oldUnit, newUnit, out string? error))
                {
                    _suppressComboChanged = true;
                    cmbRight.SelectedIndex = _lastGoodSelectedIndex;
                    _suppressComboChanged = false;

                    if (ShowConversionErrorMessageBox && !string.IsNullOrWhiteSpace(error))
                    {
                        MessageBox.Show(error, ConversionErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }
            }

            _lastGoodSelectedIndex = cmbRight.SelectedIndex;
            _previousUnit = SelectedItem;
            ValuePairChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool TryConvertLeftTextOnUnitChange_WithResult(string? oldUnit, string? newUnit, out string? error)
        {
            error = null;
            if (_autoConverting || string.IsNullOrWhiteSpace(QuantityName) ||
                string.IsNullOrWhiteSpace(oldUnit) || string.IsNullOrWhiteSpace(newUnit))
                return true;

            if (oldUnit.Equals(newUnit, StringComparison.OrdinalIgnoreCase)) return true;

            if (!TryGetCurrentValue(out double oldValue)) return true;

            try
            {
                _autoConverting = true;
                var result = UnitConverisonEngine.convert(QuantityName!, oldValue, oldUnit!, newUnit!);

                LeftText = result.value.ToString(NumberFormat, CultureInfo.CurrentCulture);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Conversion failed.\n\nQuantity: {QuantityName}\nFrom: {oldUnit}\nTo: {newUnit}\nReason: {ex.Message}";
                return false;
            }
            finally
            {
                _autoConverting = false;
            }
        }

        public bool TryGetCurrentValue(out double value) => TryParseDoubleAnyCulture(LeftText, out value);

        private static bool TryParseDoubleAnyCulture(string? s, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out value)) return true;
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) return true;
            if (double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out value)) return true;
            return false;
        }

        // Note: Port SetDefaultValue, SetValueAndUnit, etc., using the same logic as WinForms, 
        // ensuring you update 'LeftText' (the DP) rather than txtLeft.Text directly.
    }
}
