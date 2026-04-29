using System;
using System.Collections.Generic;
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

namespace AarohiWpfControls.Controls.UserBadge
{
    /// <summary>
    /// Interaction logic for Avatar.xaml
    /// </summary>
    public partial class Avatar : UserControl
    {
        public Avatar()
        {
            InitializeComponent();

            Loaded += Avatar_Loaded;
            MouseEnter += Avatar_MouseEnter;
            MouseLeave += Avatar_MouseLeave;
        }

        private void Avatar_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateAvatarText();
            UpdateSecondaryTextVisibility();
            ApplyNormalState();
        }

        private void Avatar_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            MainBorder.Background = HoverBackgroundBrush;
            MainBorder.BorderBrush = HoverBorderBrush;
        }

        private void Avatar_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ApplyNormalState();
        }

        private void ApplyNormalState()
        {
            MainBorder.Background = BackgroundBrush;
            MainBorder.BorderBrush = BorderBrushEx;
        }

        private void UpdateSecondaryTextVisibility()
        {
            if (txtSecondary == null)
                return;

            txtSecondary.Visibility =
                string.IsNullOrWhiteSpace(SecondaryText)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void UpdateAvatarText()
        {
            if (!AutoGenerateAvatarText)
                return;

            AvatarText = GenerateInitials(UserName);
        }

        private static string GenerateInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "U";

            string[] parts = name.Trim()
                                 .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
                return parts[0].Substring(0, 1).ToUpperInvariant();

            string first = parts[0].Substring(0, 1).ToUpperInvariant();
            string last = parts[parts.Length - 1].Substring(0, 1).ToUpperInvariant();
            return first + last;
        }

        private static SolidColorBrush CreateBrush(string hex)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
        }

        public string UserName
        {
            get => (string)GetValue(UserNameProperty);
            set => SetValue(UserNameProperty, value);
        }

        public static readonly DependencyProperty UserNameProperty =
            DependencyProperty.Register(
                nameof(UserName),
                typeof(string),
                typeof(Avatar),
                new PropertyMetadata("User", OnUserDataChanged));

        public string Email
        {
            get => (string)GetValue(EmailProperty);
            set => SetValue(EmailProperty, value);
        }

        public static readonly DependencyProperty EmailProperty =
            DependencyProperty.Register(
                nameof(Email),
                typeof(string),
                typeof(Avatar),
                new PropertyMetadata("user@example.com"));

        public string SecondaryText
        {
            get => (string)GetValue(SecondaryTextProperty);
            set => SetValue(SecondaryTextProperty, value);
        }

        public static readonly DependencyProperty SecondaryTextProperty =
            DependencyProperty.Register(
                nameof(SecondaryText),
                typeof(string),
                typeof(Avatar),
                new PropertyMetadata("", OnUserDataChanged));

        public string AvatarText
        {
            get => (string)GetValue(AvatarTextProperty);
            set => SetValue(AvatarTextProperty, value);
        }

        public static readonly DependencyProperty AvatarTextProperty =
            DependencyProperty.Register(
                nameof(AvatarText),
                typeof(string),
                typeof(Avatar),
                new PropertyMetadata("U"));

        public bool AutoGenerateAvatarText
        {
            get => (bool)GetValue(AutoGenerateAvatarTextProperty);
            set => SetValue(AutoGenerateAvatarTextProperty, value);
        }

        public static readonly DependencyProperty AutoGenerateAvatarTextProperty =
            DependencyProperty.Register(
                nameof(AutoGenerateAvatarText),
                typeof(bool),
                typeof(Avatar),
                new PropertyMetadata(true, OnUserDataChanged));

        public Brush BackgroundBrush
        {
            get => (Brush)GetValue(BackgroundBrushProperty);
            set => SetValue(BackgroundBrushProperty, value);
        }

        public static readonly DependencyProperty BackgroundBrushProperty =
            DependencyProperty.Register(
                nameof(BackgroundBrush),
                typeof(Brush),
                typeof(Avatar),
                new PropertyMetadata(CreateBrush("#FFFFFF")));

        public Brush HoverBackgroundBrush
        {
            get => (Brush)GetValue(HoverBackgroundBrushProperty);
            set => SetValue(HoverBackgroundBrushProperty, value);
        }

        public static readonly DependencyProperty HoverBackgroundBrushProperty =
            DependencyProperty.Register(
                nameof(HoverBackgroundBrush),
                typeof(Brush),
                typeof(Avatar),
                new PropertyMetadata(CreateBrush("#F8FAFC")));

        public Brush BorderBrushEx
        {
            get => (Brush)GetValue(BorderBrushExProperty);
            set => SetValue(BorderBrushExProperty, value);
        }

        public static readonly DependencyProperty BorderBrushExProperty =
            DependencyProperty.Register(
                nameof(BorderBrushEx),
                typeof(Brush),
                typeof(Avatar),
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
                typeof(Avatar),
                new PropertyMetadata(CreateBrush("#C7D2E0")));

        public Brush TextBrush
        {
            get => (Brush)GetValue(TextBrushProperty);
            set => SetValue(TextBrushProperty, value);
        }

        public static readonly DependencyProperty TextBrushProperty =
            DependencyProperty.Register(
                nameof(TextBrush),
                typeof(Brush),
                typeof(Avatar),
                new PropertyMetadata(CreateBrush("#1F2937")));

        public Brush SecondaryTextBrush
        {
            get => (Brush)GetValue(SecondaryTextBrushProperty);
            set => SetValue(SecondaryTextBrushProperty, value);
        }

        public static readonly DependencyProperty SecondaryTextBrushProperty =
            DependencyProperty.Register(
                nameof(SecondaryTextBrush),
                typeof(Brush),
                typeof(Avatar),
                new PropertyMetadata(CreateBrush("#6B7280")));

        public Brush AvatarBackgroundBrush
        {
            get => (Brush)GetValue(AvatarBackgroundBrushProperty);
            set => SetValue(AvatarBackgroundBrushProperty, value);
        }

        public static readonly DependencyProperty AvatarBackgroundBrushProperty =
            DependencyProperty.Register(
                nameof(AvatarBackgroundBrush),
                typeof(Brush),
                typeof(Avatar),
                new PropertyMetadata(CreateBrush("#E8EEF9")));

        public Brush AvatarBorderBrush
        {
            get => (Brush)GetValue(AvatarBorderBrushProperty);
            set => SetValue(AvatarBorderBrushProperty, value);
        }

        public static readonly DependencyProperty AvatarBorderBrushProperty =
            DependencyProperty.Register(
                nameof(AvatarBorderBrush),
                typeof(Brush),
                typeof(Avatar),
                new PropertyMetadata(CreateBrush("#D4DEEE")));

        public Brush AvatarTextBrush
        {
            get => (Brush)GetValue(AvatarTextBrushProperty);
            set => SetValue(AvatarTextBrushProperty, value);
        }

        public static readonly DependencyProperty AvatarTextBrushProperty =
            DependencyProperty.Register(
                nameof(AvatarTextBrush),
                typeof(Brush),
                typeof(Avatar),
                new PropertyMetadata(CreateBrush("#355070")));

        public Brush ToolTipBackgroundBrush
        {
            get => (Brush)GetValue(ToolTipBackgroundBrushProperty);
            set => SetValue(ToolTipBackgroundBrushProperty, value);
        }

        public static readonly DependencyProperty ToolTipBackgroundBrushProperty =
            DependencyProperty.Register(
                nameof(ToolTipBackgroundBrush),
                typeof(Brush),
                typeof(Avatar),
                new PropertyMetadata(CreateBrush("#111827")));

        public Brush ToolTipTextBrush
        {
            get => (Brush)GetValue(ToolTipTextBrushProperty);
            set => SetValue(ToolTipTextBrushProperty, value);
        }

        public static readonly DependencyProperty ToolTipTextBrushProperty =
            DependencyProperty.Register(
                nameof(ToolTipTextBrush),
                typeof(Brush),
                typeof(Avatar),
                new PropertyMetadata(CreateBrush("#F9FAFB")));

        public Brush ToolTipBorderBrush
        {
            get => (Brush)GetValue(ToolTipBorderBrushProperty);
            set => SetValue(ToolTipBorderBrushProperty, value);
        }

        public static readonly DependencyProperty ToolTipBorderBrushProperty =
            DependencyProperty.Register(
                nameof(ToolTipBorderBrush),
                typeof(Brush),
                typeof(Avatar),
                new PropertyMetadata(CreateBrush("#374151")));

        private static void OnUserDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Avatar badge)
            {
                badge.UpdateAvatarText();
                badge.UpdateSecondaryTextVisibility();
            }
        }
    }
}
