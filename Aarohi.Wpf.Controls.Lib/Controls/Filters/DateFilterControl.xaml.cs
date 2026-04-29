using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AarohiWpfControls.Filters
{
    public partial class DateFilterControl : UserControl
    {
        private bool _isInternalUpdate;

        public DateFilterControl()
        {
            InitializeComponent();
            Loaded += DateFilterControl_Loaded;
        }

        #region Dependency Properties

        public static readonly DependencyProperty StartDateProperty =
            DependencyProperty.Register(nameof(StartDate), typeof(DateTime), typeof(DateFilterControl),
                new FrameworkPropertyMetadata(DateTime.Today, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public DateTime StartDate
        {
            get => (DateTime)GetValue(StartDateProperty);
            set => SetValue(StartDateProperty, value);
        }

        public static readonly DependencyProperty EndDateProperty =
            DependencyProperty.Register(nameof(EndDate), typeof(DateTime), typeof(DateFilterControl),
                new FrameworkPropertyMetadata(DateTime.Today, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public DateTime EndDate
        {
            get => (DateTime)GetValue(EndDateProperty);
            set => SetValue(EndDateProperty, value);
        }

        public static readonly DependencyProperty FinancialYearStartMonthProperty =
            DependencyProperty.Register(nameof(FinancialYearStartMonth), typeof(int), typeof(DateFilterControl),
                new PropertyMetadata(4));

        public int FinancialYearStartMonth
        {
            get => (int)GetValue(FinancialYearStartMonthProperty);
            set => SetValue(FinancialYearStartMonthProperty, value);
        }

        public static readonly DependencyProperty CardBackgroundProperty =
            DependencyProperty.Register(nameof(CardBackground), typeof(Brush), typeof(DateFilterControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(248, 250, 252))));

        public Brush CardBackground
        {
            get => (Brush)GetValue(CardBackgroundProperty);
            set => SetValue(CardBackgroundProperty, value);
        }

        public static readonly DependencyProperty FooterBackgroundProperty =
            DependencyProperty.Register(nameof(FooterBackground), typeof(Brush), typeof(DateFilterControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 255, 255))));

        public Brush FooterBackground
        {
            get => (Brush)GetValue(FooterBackgroundProperty);
            set => SetValue(FooterBackgroundProperty, value);
        }

        public static readonly DependencyProperty BadgeBackgroundProperty =
            DependencyProperty.Register(nameof(BadgeBackground), typeof(Brush), typeof(DateFilterControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(219, 234, 254))));

        public Brush BadgeBackground
        {
            get => (Brush)GetValue(BadgeBackgroundProperty);
            set => SetValue(BadgeBackgroundProperty, value);
        }

        public static readonly DependencyProperty BorderBrushColorProperty =
            DependencyProperty.Register(nameof(BorderBrushColor), typeof(Brush), typeof(DateFilterControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(203, 213, 225))));

        public Brush BorderBrushColor
        {
            get => (Brush)GetValue(BorderBrushColorProperty);
            set => SetValue(BorderBrushColorProperty, value);
        }

        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(DateFilterControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(37, 99, 235))));

        public Brush AccentBrush
        {
            get => (Brush)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        public static readonly DependencyProperty TitleForegroundProperty =
            DependencyProperty.Register(nameof(TitleForeground), typeof(Brush), typeof(DateFilterControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(15, 23, 42))));

        public Brush TitleForeground
        {
            get => (Brush)GetValue(TitleForegroundProperty);
            set => SetValue(TitleForegroundProperty, value);
        }

        public static readonly DependencyProperty LabelForegroundProperty =
            DependencyProperty.Register(nameof(LabelForeground), typeof(Brush), typeof(DateFilterControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(71, 85, 105))));

        public Brush LabelForeground
        {
            get => (Brush)GetValue(LabelForegroundProperty);
            set => SetValue(LabelForegroundProperty, value);
        }

        public static readonly DependencyProperty ValueForegroundProperty =
            DependencyProperty.Register(nameof(ValueForeground), typeof(Brush), typeof(DateFilterControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(30, 41, 59))));

        public Brush ValueForeground
        {
            get => (Brush)GetValue(ValueForegroundProperty);
            set => SetValue(ValueForegroundProperty, value);
        }

        public static readonly DependencyProperty InputBackgroundProperty =
            DependencyProperty.Register(nameof(InputBackground), typeof(Brush), typeof(DateFilterControl),
                new PropertyMetadata(Brushes.White));

        public Brush InputBackground
        {
            get => (Brush)GetValue(InputBackgroundProperty);
            set => SetValue(InputBackgroundProperty, value);
        }

        public static readonly DependencyProperty InputForegroundProperty =
            DependencyProperty.Register(nameof(InputForeground), typeof(Brush), typeof(DateFilterControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(15, 23, 42))));

        public Brush InputForeground
        {
            get => (Brush)GetValue(InputForegroundProperty);
            set => SetValue(InputForegroundProperty, value);
        }

        public static readonly DependencyProperty InputBorderBrushProperty =
            DependencyProperty.Register(nameof(InputBorderBrush), typeof(Brush), typeof(DateFilterControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(188, 201, 219))));

        public Brush InputBorderBrush
        {
            get => (Brush)GetValue(InputBorderBrushProperty);
            set => SetValue(InputBorderBrushProperty, value);
        }

        public static readonly DependencyProperty ButtonBackgroundProperty =
            DependencyProperty.Register(nameof(ButtonBackground), typeof(Brush), typeof(DateFilterControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(37, 99, 235))));

        public Brush ButtonBackground
        {
            get => (Brush)GetValue(ButtonBackgroundProperty);
            set => SetValue(ButtonBackgroundProperty, value);
        }

        public static readonly DependencyProperty ButtonForegroundProperty =
            DependencyProperty.Register(nameof(ButtonForeground), typeof(Brush), typeof(DateFilterControl),
                new PropertyMetadata(Brushes.White));

        public Brush ButtonForeground
        {
            get => (Brush)GetValue(ButtonForegroundProperty);
            set => SetValue(ButtonForegroundProperty, value);
        }

        public static readonly DependencyProperty MainPaddingProperty =
            DependencyProperty.Register(nameof(MainPadding), typeof(Thickness), typeof(DateFilterControl),
                new PropertyMetadata(new Thickness(18, 16, 18, 16)));

        public Thickness MainPadding
        {
            get => (Thickness)GetValue(MainPaddingProperty);
            set => SetValue(MainPaddingProperty, value);
        }

        #endregion

        #region Event

        public static readonly RoutedEvent FilterAppliedEvent =
            EventManager.RegisterRoutedEvent(nameof(FilterApplied), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(DateFilterControl));

        public event RoutedEventHandler FilterApplied
        {
            add => AddHandler(FilterAppliedEvent, value);
            remove => RemoveHandler(FilterAppliedEvent, value);
        }

        #endregion

        private void DateFilterControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (cmbFilterType.SelectedIndex < 0)
                cmbFilterType.SelectedIndex = 0;
        }

        private void cmbFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _isInternalUpdate) return;

            _isInternalUpdate = true;
            try
            {
                ResetInputs();

                switch (GetSelectedMode())
                {
                    case "FY":
                        SetupFinancialYearMode();
                        break;
                    case "Month":
                        SetupMonthMode();
                        break;
                    case "Week":
                        SetupWeekMode();
                        break;
                    case "Day":
                        SetupDayMode();
                        break;
                    case "Custom":
                        SetupCustomMode();
                        break;
                }
            }
            finally
            {
                _isInternalUpdate = false;
            }

            UpdateRange();
        }

        private void cmbPrimary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _isInternalUpdate) return;

            if (GetSelectedMode() == "Month")
            {
                _isInternalUpdate = true;
                PopulateMonths();
                _isInternalUpdate = false;
            }
            else if (GetSelectedMode() == "Week")
            {
                _isInternalUpdate = true;
                PopulateWeeksForSelectedYear();
                _isInternalUpdate = false;
            }

            UpdateRange();
        }

        private void cmbSecondary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _isInternalUpdate) return;
            UpdateRange();
        }

        private void dpFrom_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _isInternalUpdate) return;
            UpdateCustomRange();
        }

        private void dpTo_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _isInternalUpdate) return;
            UpdateCustomRange();
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            UpdateRange();
            RaiseEvent(new RoutedEventArgs(FilterAppliedEvent));
        }

        private void ResetInputs()
        {
            cmbPrimary.ItemsSource = null;
            cmbSecondary.ItemsSource = null;
            cmbPrimary.Visibility = Visibility.Visible;
            cmbSecondary.Visibility = Visibility.Visible;
            dpFrom.Visibility = Visibility.Collapsed;
            dpTo.Visibility = Visibility.Collapsed;
            txtPrimaryLabel.Visibility = Visibility.Visible;
            txtSecondaryLabel.Visibility = Visibility.Visible;
        }

        private void SetupFinancialYearMode()
        {
            txtPrimaryLabel.Text = "Financial Year";
            txtSecondaryLabel.Visibility = Visibility.Hidden;
            cmbSecondary.Visibility = Visibility.Hidden;

            cmbPrimary.ItemsSource = GetFinancialYears();
            cmbPrimary.DisplayMemberPath = nameof(FinancialYearItem.DisplayText);
            cmbPrimary.SelectedIndex = 0;
        }

        private void SetupMonthMode()
        {
            txtPrimaryLabel.Text = "Year";
            txtSecondaryLabel.Text = "Month";

            cmbPrimary.ItemsSource = GetYears();
            cmbPrimary.SelectedItem = DateTime.Today.Year;
            PopulateMonths();
        }

        private void SetupWeekMode()
        {
            txtPrimaryLabel.Text = "Year";
            txtSecondaryLabel.Text = "Week";

            cmbPrimary.ItemsSource = GetYears();
            cmbPrimary.SelectedItem = DateTime.Today.Year;
            PopulateWeeksForSelectedYear();
        }

        private void SetupDayMode()
        {
            txtPrimaryLabel.Text = "Day";
            txtSecondaryLabel.Visibility = Visibility.Hidden;
            cmbSecondary.Visibility = Visibility.Hidden;

            cmbPrimary.ItemsSource = new List<string>
            {
                "Today",
                "Yesterday",
                "Last 7 Days",
                "Last 30 Days",
                "This Month"
            };
            cmbPrimary.SelectedIndex = 0;
        }

        private void SetupCustomMode()
        {
            txtPrimaryLabel.Text = "From Date";
            txtSecondaryLabel.Text = "To Date";

            cmbPrimary.Visibility = Visibility.Collapsed;
            cmbSecondary.Visibility = Visibility.Collapsed;
            dpFrom.Visibility = Visibility.Visible;
            dpTo.Visibility = Visibility.Visible;

            dpFrom.SelectedDate = DateTime.Today.AddDays(-7);
            dpTo.SelectedDate = DateTime.Today;
        }

        private List<int> GetYears()
        {
            int currentYear = DateTime.Today.Year;
            return Enumerable.Range(currentYear - 5, 11).Reverse().ToList();
        }

        private List<FinancialYearItem> GetFinancialYears()
        {
            int currentYear = DateTime.Today.Year;
            var list = new List<FinancialYearItem>();

            for (int i = currentYear - 5; i <= currentYear + 2; i++)
            {
                list.Add(new FinancialYearItem
                {
                    StartYear = i,
                    EndYear = i + 1,
                    DisplayText = $"FY {i}-{i + 1}"
                });
            }

            list.Reverse();
            return list;
        }

        private void PopulateMonths()
        {
            cmbSecondary.ItemsSource = CultureInfo.CurrentCulture.DateTimeFormat.MonthNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select((name, index) => new MonthItem
                {
                    MonthNumber = index + 1,
                    MonthName = name
                })
                .ToList();

            cmbSecondary.DisplayMemberPath = nameof(MonthItem.MonthName);
            cmbSecondary.SelectedIndex = DateTime.Today.Month - 1;
        }

        private void PopulateWeeksForSelectedYear()
        {
            if (cmbPrimary.SelectedItem == null) return;

            int year = Convert.ToInt32(cmbPrimary.SelectedItem);
            var weeks = new List<WeekItem>();

            DateTime firstDay = new DateTime(year, 1, 1);
            DateTime lastDay = new DateTime(year, 12, 31);
            DateTime current = StartOfWeek(firstDay, DayOfWeek.Monday);
            int weekNo = 1;

            while (current <= lastDay)
            {
                DateTime weekStart = current;
                DateTime weekEnd = current.AddDays(6);

                weeks.Add(new WeekItem
                {
                    WeekNumber = weekNo,
                    Start = weekStart,
                    End = weekEnd,
                    DisplayText = $"Week {weekNo}  •  {weekStart:dd MMM} - {weekEnd:dd MMM}"
                });

                current = current.AddDays(7);
                weekNo++;
            }

            cmbSecondary.ItemsSource = weeks;
            cmbSecondary.DisplayMemberPath = nameof(WeekItem.DisplayText);

            int index = weeks.FindIndex(w => DateTime.Today >= w.Start && DateTime.Today <= w.End);
            cmbSecondary.SelectedIndex = index >= 0 ? index : 0;
        }

        private void UpdateRange()
        {
            switch (GetSelectedMode())
            {
                case "FY":
                    if (cmbPrimary.SelectedItem is FinancialYearItem fy)
                    {
                        StartDate = new DateTime(fy.StartYear, FinancialYearStartMonth, 1);
                        EndDate = new DateTime(fy.EndYear, FinancialYearStartMonth, 1).AddDays(-1);
                    }
                    break;

                case "Month":
                    if (cmbPrimary.SelectedItem != null && cmbSecondary.SelectedItem is MonthItem month)
                    {
                        int year = Convert.ToInt32(cmbPrimary.SelectedItem);
                        StartDate = new DateTime(year, month.MonthNumber, 1);
                        EndDate = StartDate.AddMonths(1).AddDays(-1);
                    }
                    break;

                case "Week":
                    if (cmbSecondary.SelectedItem is WeekItem week)
                    {
                        StartDate = week.Start.Date;
                        EndDate = week.End.Date;
                    }
                    break;

                case "Day":
                    if (cmbPrimary.SelectedItem == null) return;
                    ApplyDayRange(cmbPrimary.SelectedItem.ToString());
                    break;

                case "Custom":
                    UpdateCustomRange();
                    break;
            }
        }

        private void ApplyDayRange(string selected)
        {
            switch (selected)
            {
                case "Today":
                    StartDate = DateTime.Today;
                    EndDate = DateTime.Today;
                    break;
                case "Yesterday":
                    StartDate = DateTime.Today.AddDays(-1);
                    EndDate = DateTime.Today.AddDays(-1);
                    break;
                case "Last 7 Days":
                    StartDate = DateTime.Today.AddDays(-6);
                    EndDate = DateTime.Today;
                    break;
                case "Last 30 Days":
                    StartDate = DateTime.Today.AddDays(-29);
                    EndDate = DateTime.Today;
                    break;
                case "This Month":
                    StartDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    EndDate = StartDate.AddMonths(1).AddDays(-1);
                    break;
            }
        }

        private void UpdateCustomRange()
        {
            if (dpFrom.SelectedDate.HasValue)
                StartDate = dpFrom.SelectedDate.Value.Date;

            if (dpTo.SelectedDate.HasValue)
                EndDate = dpTo.SelectedDate.Value.Date;

            if (EndDate < StartDate)
            {
                EndDate = StartDate;
                dpTo.SelectedDate = EndDate;
            }
        }

        private static DateTime StartOfWeek(DateTime date, DayOfWeek startOfWeek)
        {
            int diff = (7 + (date.DayOfWeek - startOfWeek)) % 7;
            return date.AddDays(-diff).Date;
        }

        private string GetSelectedMode()
        {
            return cmbFilterType.SelectedItem is ComboBoxItem item
                ? item.Tag?.ToString() ?? "FY"
                : "FY";
        }
    }

    public class FinancialYearItem
    {
        public int StartYear { get; set; }
        public int EndYear { get; set; }
        public string DisplayText { get; set; } = string.Empty;

        public override string ToString() => DisplayText;
    }

    public class MonthItem
    {
        public int MonthNumber { get; set; }
        public string MonthName { get; set; } = string.Empty;

        public override string ToString() => MonthName;
    }

    public class WeekItem
    {
        public int WeekNumber { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string DisplayText { get; set; } = string.Empty;

        public override string ToString() => DisplayText;
    }
}
