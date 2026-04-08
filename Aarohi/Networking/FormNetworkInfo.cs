using Aarohi.Core;
using Aarohi.Core.Logger;
using Aarohi.Core.PLC;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aarohi.Networking
{
    public class FormNetworkInfo : Form
    {
        // ---- UI ----
        private ComboBox cmbIp = null!;
        private TextBox txtReach = null!;
        private TextBox txtDns = null!;
        private TextBox txtMac = null!;
        private TextBox txtSerial = null!;
        private TextBox txtOrderMemo = null!;
        private TextBox txtCustomer = null!;

        private Panel hostSerial = null!;
        private Panel hostOrderMemo = null!;
        private Panel hostCustomer = null!;

        private Label lblStatus = null!;
        private Button btnRefresh = null!;
        private Button btnClose = null!;
        private Button? btnSave; // ✅ only in dev mode

        // ---- Behavior ----
        private readonly bool _isDev;
        private readonly List<string> _ips;

        // ---- Async safety ----
        private CancellationTokenSource? _cts;
        private int _refreshVersion;
        private bool _isClosing;

        public FormNetworkInfo(List<string> ips, bool isDev = false)
        {
            _ips = ips ?? new List<string>();
            _isDev = isDev;

            BuildUi();
            WireEvents();
            LoadIps();
            ApplyDevPermissions();
        }

        // ===================== UI =====================

        private void BuildUi()
        {
            Text = "Network Details";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;

            BackColor = Color.FromArgb(246, 248, 252);
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(860, 800);

            // Root layout
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor,
                Padding = new Padding(22),
                RowCount = 3,
                ColumnCount = 1
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            Controls.Add(root);

            // Header
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                RowCount = 2,
                ColumnCount = 1,
                Margin = new Padding(0, 0, 0, 8)
            };
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

            var title = new Label
            {
                Text = "Network Details",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Margin = new Padding(0),
                Dock = DockStyle.Fill
            };

            var sub = new Label
            {
                Text = "Select IP to view Reachable, DNS, MAC and device details",
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = Color.FromArgb(100, 116, 139),
                Margin = new Padding(2, 0, 0, 0),
                Dock = DockStyle.Fill
            };

            header.Controls.Add(title, 0, 0);
            header.Controls.Add(sub, 0, 1);

            // Card panel
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(18),
                Margin = new Padding(0, 0, 0, 10)
            };
            card.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            // Grid inside card (7 rows)
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                BackColor = Color.White,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(2, 6, 2, 6)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            grid.RowStyles.Clear();
            for (int i = 0; i < 7; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

            // Fields
            cmbIp = MakeComboBox();
            txtReach = MakeTextBox("—", readOnly: true);
            txtDns = MakeTextBox("—", readOnly: true);
            txtMac = MakeTextBox("—", readOnly: true);

            txtSerial = MakeTextBox("—", readOnly: true);
            txtOrderMemo = MakeTextBox("—", readOnly: true);
            txtCustomer = MakeTextBox("—", readOnly: true);

            AddRow(grid, 0, "IP Address", WrapInputPanel(cmbIp));
            AddRow(grid, 1, "Reachable", WrapInputPanel(txtReach));
            AddRow(grid, 2, "DNS Name", WrapInputPanel(txtDns));
            AddRow(grid, 3, "MAC Address", WrapInputPanel(txtMac));

            hostSerial = WrapInputPanel(txtSerial);
            hostOrderMemo = WrapInputPanel(txtOrderMemo);
            hostCustomer = WrapInputPanel(txtCustomer);

            AddRow(grid, 4, "Serial Number", hostSerial);
            AddRow(grid, 5, "Order Memo No.", hostOrderMemo);
            AddRow(grid, 6, "Customer Name", hostCustomer);

            card.Controls.Add(grid);

            // Footer
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 6, 0, 0),
                Margin = new Padding(0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380)); // ✅ more width for 3 buttons in dev

            lblStatus = new Label
            {
                Text = "Ready",
                AutoSize = true,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(12, 6, 12, 6),
                Margin = new Padding(0, 12, 0, 0),
                Font = new Font("Segoe UI", 9.5f),
                Anchor = AnchorStyles.Left
            };

            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0, 8, 0, 0)
            };

            btnClose = MakeButton("Close");
            btnRefresh = MakeButton("Refresh");

            btnPanel.Controls.Add(btnClose);
            btnPanel.Controls.Add(btnRefresh);

            // ✅ Save button only when dev
            if (_isDev)
            {
                btnSave = MakePrimaryButton("Save");
                btnPanel.Controls.Add(btnSave); // right-to-left => Save will appear left of Refresh automatically
            }

            footer.Controls.Add(lblStatus, 0, 0);
            footer.Controls.Add(btnPanel, 1, 0);

            // Compose root
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(card, 0, 1);
            root.Controls.Add(footer, 0, 2);
        }

        private static void AddRow(TableLayoutPanel grid, int row, string label, Control inputHost)
        {
            var lbl = new Label
            {
                Text = label,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                Margin = new Padding(0, 18, 0, 0),
                Anchor = AnchorStyles.Left
            };

            grid.Controls.Add(lbl, 0, row);
            grid.Controls.Add(inputHost, 1, row);
        }

        private Panel WrapInputPanel(Control inner)
        {
            var host = new Panel
            {
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Height = 42,
                Padding = new Padding(12, 10, 12, 10),
                Margin = new Padding(0, 8, 0, 8)
            };

            host.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, host.Width - 1, host.Height - 1);
            };

            inner.Dock = DockStyle.Fill;
            host.Controls.Add(inner);
            return host;
        }

        private static ComboBox MakeComboBox()
        {
            return new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(15, 23, 42),
                IntegralHeight = false,
                Height = 28,
                Margin = new Padding(0)
            };
        }

        private static TextBox MakeTextBox(string text, bool readOnly)
        {
            return new TextBox
            {
                Text = text,
                ReadOnly = readOnly,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(15, 23, 42),
                Font = new Font("Segoe UI", 10f),
                Margin = new Padding(0),
                TabStop = !readOnly
            };
        }

        private static Button MakeButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Width = 110,
                Height = 38,
                Margin = new Padding(10, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(15, 23, 42),
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold)
            };

            b.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
            b.FlatAppearance.BorderSize = 1;
            return b;
        }

        // Primary button style (Save)
        private static Button MakePrimaryButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Width = 110,
                Height = 38,
                Margin = new Padding(10, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(37, 99, 235),  // blue
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold)
            };

            b.FlatAppearance.BorderColor = Color.FromArgb(37, 99, 235);
            b.FlatAppearance.BorderSize = 1;
            return b;
        }

        // ===================== Dev Permission =====================

        private void ApplyDevPermissions()
        {
            SetEditable(txtSerial, hostSerial, _isDev);
            SetEditable(txtOrderMemo, hostOrderMemo, _isDev);
            SetEditable(txtCustomer, hostCustomer, _isDev);
        }

        private void SetEditable(TextBox tb, Panel host, bool editable)
        {
            tb.ReadOnly = !editable;
            tb.TabStop = editable;

            if (editable)
            {
                tb.BackColor = Color.FromArgb(248, 250, 252);
                tb.Cursor = Cursors.IBeam;
                host.BackColor = tb.BackColor;
            }
            else
            {
                tb.BackColor = Color.White;
                tb.Cursor = Cursors.Default;
                host.BackColor = tb.BackColor;
            }

            host.Invalidate();
        }

        // ===================== Events =====================

        private void WireEvents()
        {
            btnClose.Click += (s, e) => Close();

            cmbIp.SelectedIndexChanged += async (s, e) =>
            {
                if (cmbIp.SelectedItem is string ip)
                    await RefreshAsync(ip);
            };

            btnRefresh.Click += async (s, e) =>
            {
                if (cmbIp.SelectedItem is string ip)
                    await RefreshAsync(ip);
            };

            if (_isDev && btnSave != null)
            {
                btnSave.Click += (s, e) =>
                {
                    string ip = cmbIp.SelectedItem as string ?? "";
                  
                    MessageBox.Show(
                        $"Saved (DEV)\nIP: {ip}\nSerial: {txtSerial.Text}\nOrderMemo: {txtOrderMemo.Text}\nCustomer: {txtCustomer.Text}",
                        "Saved",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                };
            }

            Shown += async (s, e) =>
            {
                if (cmbIp.SelectedItem is string ip)
                    await RefreshAsync(ip);
            };

            FormClosing += (s, e) =>
            {
                _isClosing = true;
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
            };
        }

        private void LoadIps()
        {
            cmbIp.Items.Clear();
            cmbIp.Items.AddRange(_ips.ToArray());
            cmbIp.SelectedIndex = cmbIp.Items.Count > 0 ? 0 : -1;
        }

        // ===================== Refresh =====================

        private async Task RefreshAsync(string ip)
        {
            if (_isClosing || IsDisposed) return;

            int myVersion = Interlocked.Increment(ref _refreshVersion);

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            SetLoading(true, $"Checking {ip}...");

            try
            {
                PlcNetworkInfo.IP = ip;

                var result = await Task.Run(() =>
                {
                    if (ct.IsCancellationRequested) return new Result(true);

                    bool reach = PlcNetworkInfo.IsReachable;
                    if (ct.IsCancellationRequested) return new Result(true);

                    string dns = PlcNetworkInfo.DnsName;
                    if (ct.IsCancellationRequested) return new Result(true);

                    string mac = PlcNetworkInfo.MacAddress;
                    if (ct.IsCancellationRequested) return new Result(true);

                    // NOTE:
                    // Serial/Order/Customer are manual (dev editable) -> do not overwrite here.
                    return new Result(false)
                    {
                        Reachable = reach,
                        Dns = dns,
                        Mac = mac
                    };
                });

                if (_isClosing || IsDisposed) return;
                if (myVersion != Volatile.Read(ref _refreshVersion)) return;
                if (result.Cancelled) return;

                txtReach.Text = result.Reachable ? "Yes" : "No";
                txtDns.Text = string.IsNullOrWhiteSpace(result.Dns) ? "—" : result.Dns;
                txtMac.Text = string.IsNullOrWhiteSpace(result.Mac) ? "—" : result.Mac;

                SetLoading(false, "Done");
            }
            catch (Exception ex)
            {
                if (_isClosing || IsDisposed) return;
                if (myVersion != Volatile.Read(ref _refreshVersion)) return;

                txtReach.Text = "—";
                txtDns.Text = "—";
                txtMac.Text = "—";
                SetLoading(false, "Error");

                MessageBox.Show(ex.Message, "Network Info Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetLoading(bool loading, string text)
        {
            if (_isClosing || IsDisposed) return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetLoading(loading, text)));
                return;
            }

            lblStatus.Text = text;
            lblStatus.BackColor = loading
                ? Color.FromArgb(255, 251, 235)
                : (text == "Done" ? Color.FromArgb(236, 253, 245) : Color.FromArgb(241, 245, 249));

            btnRefresh.Enabled = !loading;
            cmbIp.Enabled = !loading;
            if (_isDev && btnSave != null) btnSave.Enabled = !loading;
        }

        // ===================== DTO =====================

        private sealed class Result
        {
            public bool Cancelled { get; }
            public bool Reachable { get; set; }
            public string Dns { get; set; } = "—";
            public string Mac { get; set; } = "—";

            public Result(bool cancelled) => Cancelled = cancelled;
        }
    }

}
