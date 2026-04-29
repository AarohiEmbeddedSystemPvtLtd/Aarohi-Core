using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace AarohiWpfControls.Models
{
    public class ThemePaletteItem
    {
        public string ThemeName { get; set; } = string.Empty;
        public ObservableCollection<Brush> Colors { get; set; } = new ObservableCollection<Brush>();
    }
}
