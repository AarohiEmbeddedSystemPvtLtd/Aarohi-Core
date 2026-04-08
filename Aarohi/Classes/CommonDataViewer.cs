using Aarohi.ExtendedUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aarohi.Classes
{
    public partial class CommonDataViewer : Form
    {
        // Keep references to created cells for fast filtering
        private readonly List<ExtendedPanel> _cells = new();

        public CommonDataViewer(string label)
        {
            InitializeComponent();
            LabelHeading.Text = label;

            // live filter as user types
            SearchTextBox.TextChanged += (_, __) => ApplySearchFilter(SearchTextBox.Text);
        }

        public ExtendedPanel CreateCell(string heading, string value)
        {
            var cellPanel = new ExtendedPanel
            {
                BackColor = Color.White,
                BlurTint = Color.FromArgb(40, 255, 255, 255),
                BorderColor = Color.Transparent,
                BorderWidth = 1,
                CornerRadius = 12,
                Padding = new Padding(8),
                Margin = new Padding(4),
                // store the heading for filtering (lower-cased for quick compare)
                Tag = heading ?? string.Empty
            };
            cellPanel.GradientColors.Add(Color.DeepSkyBlue);
            cellPanel.GradientColors.Add(Color.MediumBlue);
            cellPanel.GradientOpacity = 0.5f;

            var labelHeading = new Label
            {
                Text = heading + ":",
                Font = new Font("Gadugi", 14f, FontStyle.Bold),
                ForeColor = Color.Black,
                BackColor = Color.Transparent,
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };

            var labelValue = new Label
            {
                Text = value,
                Font = new Font("Gadugi", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                AutoSize = true,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };

            cellPanel.Controls.Add(labelHeading);
            cellPanel.Controls.Add(labelValue);

            void Recalc()
            {
                const int gap = 10;
                var headingSz = TextRenderer.MeasureText(labelHeading.Text, labelHeading.Font, Size.Empty, TextFormatFlags.NoPadding);
                var valueSz = TextRenderer.MeasureText(labelValue.Text, labelValue.Font, Size.Empty, TextFormatFlags.NoPadding);

                int x = cellPanel.Padding.Left;
                int y = cellPanel.Padding.Top;
                labelHeading.Location = new Point(x, y);
                labelValue.Location = new Point(x + headingSz.Width + gap, y);

                int w = cellPanel.Padding.Left + headingSz.Width + gap + valueSz.Width + cellPanel.Padding.Right;
                int h = cellPanel.Padding.Top + Math.Max(headingSz.Height, valueSz.Height) + cellPanel.Padding.Bottom;

                cellPanel.MinimumSize = new Size(w, h);
                cellPanel.Size = new Size(w, h);
            }

            labelHeading.TextChanged += (_, __) => Recalc();
            labelHeading.FontChanged += (_, __) => Recalc();
            labelValue.TextChanged += (_, __) => Recalc();
            labelValue.FontChanged += (_, __) => Recalc();
            cellPanel.PaddingChanged += (_, __) => Recalc();

            Recalc();
            return cellPanel;
        }

        public void set_childs(Dictionary<string, string> values)
        {
            PanelCellHolder.SuspendLayout();
            PanelCellHolder.Controls.Clear();
            _cells.Clear();

            foreach (var kv in values)
            {
                var heading = kv.Key;
                var cell = CreateCell(heading, kv.Value ?? string.Empty);
                PanelCellHolder.Controls.Add(cell);
                _cells.Add(cell);
            }

            PanelCellHolder.ResumeLayout(true);

            // apply any existing search text after rebuild
            ApplySearchFilter(SearchTextBox.Text);
        }

        private void ApplySearchFilter(string? query)
        {
            string q = (query ?? string.Empty).Trim();
            if (q.Length == 0)
            {
                // show all
                foreach (var cell in _cells) cell.Visible = true;
            }
            else
            {
                foreach (var cell in _cells)
                {
                    var heading = (cell.Tag as string) ?? string.Empty;
                    bool match = heading.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                    cell.Visible = match;
                }
            }

            // Optional: force layout to close up gaps when many are hidden
            PanelCellHolder.PerformLayout();
            PanelCellHolder.Invalidate();
        }
    }
}
