using System.Collections.ObjectModel;
using System.Windows.Media;

namespace AarohiWpfControls.Helper_Classes
{
    public class ThemePaletteItem
    {
        public string ThemeName { get; set; } = string.Empty;
        public ObservableCollection<Brush> Colors { get; set; } = new ObservableCollection<Brush>();
    }
}
