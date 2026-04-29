using AarohiWpfControls.Helper_Classes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace AarohiWpfControls.Controls.DropDown
{
    /// <summary>
    /// Interaction logic for DropDown.xaml
    /// </summary>
    public partial class DropDown : UserControl
    {
        public DropDown()
        {
            InitializeComponent();
        }
        public ObservableCollection<ThemePaletteItem> ThemeItems
        {
            get => (ObservableCollection<ThemePaletteItem>)GetValue(ThemeItemsProperty);
            set => SetValue(ThemeItemsProperty, value);
        }

        public static readonly DependencyProperty ThemeItemsProperty =
            DependencyProperty.Register(
                nameof(ThemeItems),
                typeof(ObservableCollection<ThemePaletteItem>),
                typeof(DropDown),
                new PropertyMetadata(new ObservableCollection<ThemePaletteItem>()));

        public ThemePaletteItem SelectedTheme
        {
            get => (ThemePaletteItem)GetValue(SelectedThemeProperty);
            set => SetValue(SelectedThemeProperty, value);
        }

        public static readonly DependencyProperty SelectedThemeProperty =
            DependencyProperty.Register(
                nameof(SelectedTheme),
                typeof(ThemePaletteItem),
                typeof(DropDown),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public Brush ControlBackgroundBrush
        {
            get => (Brush)GetValue(ControlBackgroundBrushProperty);
            set => SetValue(ControlBackgroundBrushProperty, value);
        }

        public static readonly DependencyProperty ControlBackgroundBrushProperty =
            DependencyProperty.Register(
                nameof(ControlBackgroundBrush),
                typeof(Brush),
                typeof(DropDown),
                new PropertyMetadata(CreateBrush("#FCFCFD")));

        public Brush PopupBackgroundBrush
        {
            get => (Brush)GetValue(PopupBackgroundBrushProperty);
            set => SetValue(PopupBackgroundBrushProperty, value);
        }

        public static readonly DependencyProperty PopupBackgroundBrushProperty =
            DependencyProperty.Register(
                nameof(PopupBackgroundBrush),
                typeof(Brush),
                typeof(DropDown),
                new PropertyMetadata(CreateBrush("#FFFFFF")));

        public Brush BorderBrushEx
        {
            get => (Brush)GetValue(BorderBrushExProperty);
            set => SetValue(BorderBrushExProperty, value);
        }

        public static readonly DependencyProperty BorderBrushExProperty =
            DependencyProperty.Register(
                nameof(BorderBrushEx),
                typeof(Brush),
                typeof(DropDown),
                new PropertyMetadata(CreateBrush("#D9DEE7")));

        public Brush HoverBorderBrush
        {
            get => (Brush)GetValue(HoverBorderBrushProperty);
            set => SetValue(HoverBorderBrushProperty, value);
        }

        public static readonly DependencyProperty HoverBorderBrushProperty =
            DependencyProperty.Register(
                nameof(HoverBorderBrush),
                typeof(Brush),
                typeof(DropDown),
                new PropertyMetadata(CreateBrush("#BFC8D8")));

        public Brush FocusBorderBrush
        {
            get => (Brush)GetValue(FocusBorderBrushProperty);
            set => SetValue(FocusBorderBrushProperty, value);
        }

        public static readonly DependencyProperty FocusBorderBrushProperty =
            DependencyProperty.Register(
                nameof(FocusBorderBrush),
                typeof(Brush),
                typeof(DropDown),
                new PropertyMetadata(CreateBrush("#9AA8C7")));

        public Brush PopupBorderBrush
        {
            get => (Brush)GetValue(PopupBorderBrushProperty);
            set => SetValue(PopupBorderBrushProperty, value);
        }

        public static readonly DependencyProperty PopupBorderBrushProperty =
            DependencyProperty.Register(
                nameof(PopupBorderBrush),
                typeof(Brush),
                typeof(DropDown),
                new PropertyMetadata(CreateBrush("#E3E8EF")));

        public Brush TextBrush
        {
            get => (Brush)GetValue(TextBrushProperty);
            set => SetValue(TextBrushProperty, value);
        }

        public static readonly DependencyProperty TextBrushProperty =
            DependencyProperty.Register(
                nameof(TextBrush),
                typeof(Brush),
                typeof(DropDown),
                new PropertyMetadata(CreateBrush("#1F2937")));

        public Brush PlaceholderBrush
        {
            get => (Brush)GetValue(PlaceholderBrushProperty);
            set => SetValue(PlaceholderBrushProperty, value);
        }

        public static readonly DependencyProperty PlaceholderBrushProperty =
            DependencyProperty.Register(
                nameof(PlaceholderBrush),
                typeof(Brush),
                typeof(DropDown),
                new PropertyMetadata(CreateBrush("#98A2B3")));

        public Brush PopupItemHoverBrush
        {
            get => (Brush)GetValue(PopupItemHoverBrushProperty);
            set => SetValue(PopupItemHoverBrushProperty, value);
        }

        public static readonly DependencyProperty PopupItemHoverBrushProperty =
            DependencyProperty.Register(
                nameof(PopupItemHoverBrush),
                typeof(Brush),
                typeof(DropDown),
                new PropertyMetadata(CreateBrush("#F5F7FA")));

        public Brush PopupItemSelectedBrush
        {
            get => (Brush)GetValue(PopupItemSelectedBrushProperty);
            set => SetValue(PopupItemSelectedBrushProperty, value);
        }

        public static readonly DependencyProperty PopupItemSelectedBrushProperty =
            DependencyProperty.Register(
                nameof(PopupItemSelectedBrush),
                typeof(Brush),
                typeof(DropDown),
                new PropertyMetadata(CreateBrush("#EEF2F7")));

        public Brush PaletteCircleBorderBrush
        {
            get => (Brush)GetValue(PaletteCircleBorderBrushProperty);
            set => SetValue(PaletteCircleBorderBrushProperty, value);
        }

        public static readonly DependencyProperty PaletteCircleBorderBrushProperty =
            DependencyProperty.Register(
                nameof(PaletteCircleBorderBrush),
                typeof(Brush),
                typeof(DropDown),
                new PropertyMetadata(CreateBrush("#D6DCE5")));

        public Brush ArrowPanelBrush
        {
            get => (Brush)GetValue(ArrowPanelBrushProperty);
            set => SetValue(ArrowPanelBrushProperty, value);
        }

        public static readonly DependencyProperty ArrowPanelBrushProperty =
            DependencyProperty.Register(
                nameof(ArrowPanelBrush),
                typeof(Brush),
                typeof(DropDown),
                new PropertyMetadata(CreateBrush("#F1F5F9")));

        public Brush ArrowPanelHoverBrush
        {
            get => (Brush)GetValue(ArrowPanelHoverBrushProperty);
            set => SetValue(ArrowPanelHoverBrushProperty, value);
        }

        public static readonly DependencyProperty ArrowPanelHoverBrushProperty =
            DependencyProperty.Register(
                nameof(ArrowPanelHoverBrush),
                typeof(Brush),
                typeof(DropDown),
                new PropertyMetadata(CreateBrush("#E2E8F0")));

        public Brush ArrowPanelOpenBrush
        {
            get => (Brush)GetValue(ArrowPanelOpenBrushProperty);
            set => SetValue(ArrowPanelOpenBrushProperty, value);
        }

        public static readonly DependencyProperty ArrowPanelOpenBrushProperty =
            DependencyProperty.Register(
                nameof(ArrowPanelOpenBrush),
                typeof(Brush),
                typeof(DropDown),
                new PropertyMetadata(CreateBrush("#CBD5E1")));

        public Brush ArrowIconBrush
        {
            get => (Brush)GetValue(ArrowIconBrushProperty);
            set => SetValue(ArrowIconBrushProperty, value);
        }

        public static readonly DependencyProperty ArrowIconBrushProperty =
            DependencyProperty.Register(
                nameof(ArrowIconBrush),
                typeof(Brush),
                typeof(DropDown),
                new PropertyMetadata(CreateBrush("#475569")));

        private static SolidColorBrush CreateBrush(string hex)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
        }
    }
}
