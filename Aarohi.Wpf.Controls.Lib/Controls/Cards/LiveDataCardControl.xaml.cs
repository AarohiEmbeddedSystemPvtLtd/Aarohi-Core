using System.Windows;
using System.Windows.Controls;

namespace Aarohi.Wpf.Controls.Lib.Controls.Cards
{
    public partial class LiveDataCardControl : UserControl
    {
        public LiveDataCardControl()
        {
            InitializeComponent();
        }

        #region Label

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(
                nameof(Label),
                typeof(string),
                typeof(LiveDataCardControl),
                new PropertyMetadata("LABEL"));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        #endregion

        #region Value

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(string),
                typeof(LiveDataCardControl),
                new PropertyMetadata("0.00"));

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        #endregion

        #region Unit

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(
                nameof(Unit),
                typeof(string),
                typeof(LiveDataCardControl),
                new PropertyMetadata("UNIT"));

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        #endregion

        #region Parameter

        public static readonly DependencyProperty ParameterProperty =
            DependencyProperty.Register(
                nameof(Parameter),
                typeof(string),
                typeof(LiveDataCardControl),
                new PropertyMetadata(string.Empty));

        public string Parameter
        {
            get => (string)GetValue(ParameterProperty);
            set => SetValue(ParameterProperty, value);
        }

        #endregion

        #region Column_Name

        public static readonly DependencyProperty Column_NameProperty =
            DependencyProperty.Register(
                nameof(Column_Name),
                typeof(string),
                typeof(LiveDataCardControl),
                new PropertyMetadata(string.Empty));

        public string Column_Name
        {
            get => (string)GetValue(Column_NameProperty);
            set => SetValue(Column_NameProperty, value);
        }

        #endregion

        #region Order

        public static readonly DependencyProperty OrderProperty =
            DependencyProperty.Register(
                nameof(Order),
                typeof(int),
                typeof(LiveDataCardControl),
                new PropertyMetadata(int.MaxValue));

        public int Order
        {
            get => (int)GetValue(OrderProperty);
            set => SetValue(OrderProperty, value);
        }

        #endregion

    }
}
