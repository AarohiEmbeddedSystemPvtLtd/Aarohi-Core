using Aarohi.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace AarohiWpfControls.Helper_Classes
{
    public class GridUnitConverter : IValueConverter
    {
        public string Parameter { get; set; }
        public string FromUnit { get; set; }
        public string ToUnit { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value == DBNull.Value || string.IsNullOrEmpty(ToUnit))
                return value;

            try
            {
                return UnitConverisonEngine.convert(Parameter, value, FromUnit, ToUnit);
            }
            catch
            {
                return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
