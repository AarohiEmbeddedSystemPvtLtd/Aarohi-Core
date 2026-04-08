using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aarohi.Core.Logger
{

    public sealed class FormLogViewer : Form
    {
        // ---- Level filter mask ----
        [Flags]
        public enum LevelMask
        {
            None = 0,
            Trace = 1 << 0,
            Debug = 1 << 1,
            Info = 1 << 2,
            Warn = 1 << 3,
            Error = 1 << 4,
            Fatal = 1 << 5,
            All = Trace | Debug | Info | Warn | Error | Fatal
        }

        public static LevelMask MaskFor(LogLevel lvl) => lvl switch
        {
            LogLevel.Trace => LevelMask.Trace,
            LogLevel.Debug => LevelMask.Debug,
            LogLevel.Info => LevelMask.Info,
            LogLevel.Warn => LevelMask.Warn,
            LogLevel.Error => LevelMask.Error,
            LogLevel.Fatal => LevelMask.Fatal,
            _ => LevelMask.Info
        };

        // ---- Private backing field (controls Debug/Trace visibility) ----
        private bool _showDebug;

        // ---- Public property (hidden from Designer serialization) ----
        // If false => hides Debug + Trace level checkboxes (and forces them unchecked)
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowDebug
        {
            get => _showDebug;
            set
            {
                if (_showDebug == value) return;
                _showDebug = value;

                if (!IsHandleCreated || IsDisposed) return;

                if (InvokeRequired)
                    BeginInvoke((MethodInvoker)(() =>
                    {
                        ApplyDebugVisibility();
                        _ = ReloadAsync();
                    }));
                else
                {
                    ApplyDebugVisibility();
                    _ = ReloadAsync();
                }
            }
        }

        // ---- UI ----
        private readonly DateTimePicker dtFrom = new() { Format = DateTimePickerFormat.Short, Width = 120 };
        private readonly DateTimePicker dtTo = new() { Format = DateTimePickerFormat.Short, Width = 120 };
        private readonly Button btnToday = new() { Text = "Today", AutoSize = true };
        private readonly Button btnYesterday = new() { Text = "Yesterday", AutoSize = true };
        private readonly Button btnReload = new() { Text = "Reload", AutoSize = true };
        private readonly TextBox txtSearch = new() { PlaceholderText = "Search message/source/username...", Width = 360 };

        // ---- Level checkboxes ----
        private readonly CheckBox chkTrace = new() { Text = "Trace", AutoSize = true };
        private readonly CheckBox chkDebug = new() { Text = "Debug", AutoSize = true };
        private readonly CheckBox chkInfo = new() { Text = "Info", AutoSize = true };
        private readonly CheckBox chkWarn = new() { Text = "Warn", AutoSize = true };
        private readonly CheckBox chkError = new() { Text = "Error", AutoSize = true };
        private readonly CheckBox chkFatal = new() { Text = "Fatal", AutoSize = true };

        private readonly Button btnAllLevels = new() { Text = "All", AutoSize = true };
        private readonly Button btnNoneLevels = new() { Text = "None", AutoSize = true };

        private readonly BufferedDataGridView grid = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoGenerateColumns = false
        };

        private readonly RichTextBox rtbDetails = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = false,
            Font = new Font("Consolas", 10f)
        };

        private readonly BindingList<LogRow> _rows = new();
        private readonly BindingSource _bs = new();

        private readonly ToolStripStatusLabel statusLeft = new() { Text = "Ready" };
        private readonly ToolStripStatusLabel statusRight = new() { Text = "" };

        // ---- Load control ----
        private CancellationTokenSource? _cts;
        private int _loadVersion;

        // ---- Log settings ----
        private readonly string _dir;
        private string _prefix;
        private string _ext;

        /// <summary>
        /// If you pass only directoryPath, it will auto-detect prefix/ext based on actual files.
        /// File name pattern expected: PREFIX_yyyy-MM-dd.EXT  (example: DefaultLog_2026-01-09.txt)
        /// </summary>
        public FormLogViewer(string? directoryPath = null, string? fileNamePrefix = null, string? extension = null)
        {
            Text = "Log Viewer";
            Width = 1250;
            Height = 780;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);

            _dir = directoryPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

            if (!string.IsNullOrWhiteSpace(fileNamePrefix) && !string.IsNullOrWhiteSpace(extension))
            {
                _prefix = fileNamePrefix!;
                _ext = extension!;
            }
            else
            {
                (_prefix, _ext) = AutoDetectPrefixExt(_dir, fallbackPrefix: "DefaultLog", fallbackExt: ".txt");
            }

            BuildUi();

            // set defaults BEFORE wiring events (avoid constructor reload storms)
            SetToday();

            // default levels
            chkInfo.Checked = true;
            chkWarn.Checked = true;
            chkError.Checked = true;
            chkFatal.Checked = true;

            // default: hide Debug/Trace (you can set ShowDebug=true from outside)
            _showDebug = false;
            ApplyDebugVisibility();

            WireEvents();

            Shown += async (_, __) => await ReloadAsync();
        }

        private void BuildUi()
        {
            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 62,
                Padding = new Padding(10, 10, 10, 6),
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.FromArgb(245, 246, 248)
            };

            top.Controls.Add(MakeLabel("From:"));
            top.Controls.Add(dtFrom);
            top.Controls.Add(MakeSpacer(10));
            top.Controls.Add(MakeLabel("To:"));
            top.Controls.Add(dtTo);
            top.Controls.Add(MakeSpacer(10));
            top.Controls.Add(btnToday);
            top.Controls.Add(btnYesterday);
            top.Controls.Add(MakeSpacer(12));

            top.Controls.Add(MakeLabel("Levels:"));
            top.Controls.Add(chkTrace);
            top.Controls.Add(chkDebug);
            top.Controls.Add(chkInfo);
            top.Controls.Add(chkWarn);
            top.Controls.Add(chkError);
            top.Controls.Add(chkFatal);
            top.Controls.Add(MakeSpacer(6));
            top.Controls.Add(btnAllLevels);
            top.Controls.Add(btnNoneLevels);

            top.Controls.Add(MakeSpacer(12));
            top.Controls.Add(txtSearch);
            top.Controls.Add(btnReload);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 800
            };

            ConfigureGrid();

            _bs.DataSource = _rows;
            grid.DataSource = _bs;

            split.Panel1.Controls.Add(grid);
            split.Panel2.Controls.Add(rtbDetails);

            var status = new StatusStrip();
            status.Items.Add(statusLeft);
            status.Items.Add(new ToolStripStatusLabel { Spring = true });
            status.Items.Add(statusRight);

            Controls.Add(split);
            Controls.Add(status);
            Controls.Add(top);

            UpdateTitle();
        }

        private void ConfigureGrid()
        {
            grid.Columns.Clear();

            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LogRow.Timestamp), HeaderText = "Time", Width = 175 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LogRow.Level), HeaderText = "Level", Width = 70 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LogRow.Source), HeaderText = "Source", Width = 160 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LogRow.UserName), HeaderText = "User", Width = 120 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LogRow.SessionCode), HeaderText = "Session ID", Width = 120 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LogRow.Message), HeaderText = "Message", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            grid.ColumnHeadersHeight = 34;
            grid.RowTemplate.Height = 26;

            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = Color.FromArgb(230, 230, 230);

            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 252);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(40, 40, 40);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);

            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 255);
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;

            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 251);

            // Subtle severity tint
            grid.CellFormatting += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                if (grid.Rows[e.RowIndex].DataBoundItem is not LogRow r) return;

                var st = grid.Rows[e.RowIndex].DefaultCellStyle;
                st.ForeColor = Color.FromArgb(30, 30, 30);

                if (r.Level == LogLevel.Error || r.Level == LogLevel.Fatal)
                    st.BackColor = Color.FromArgb(255, 240, 240);
                else if (r.Level == LogLevel.Warn)
                    st.BackColor = Color.FromArgb(255, 250, 235);
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Copy Row", null, (_, __) => CopySelectedRow());
            menu.Items.Add("Copy Details", null, (_, __) => CopyDetails());
            grid.ContextMenuStrip = menu;
        }

        private void WireEvents()
        {
            btnToday.Click += async (_, __) => { SetToday(); await ReloadAsync(); };
            btnYesterday.Click += async (_, __) => { SetYesterday(); await ReloadAsync(); };
            btnReload.Click += async (_, __) => await ReloadAsync();

            dtFrom.ValueChanged += async (_, __) => await ReloadAsync();
            dtTo.ValueChanged += async (_, __) => await ReloadAsync();

            txtSearch.TextChanged += Debounce(async () => await ReloadAsync(), 350);

            // Debounced reload for level changes (so multiple toggles feel smooth)
            var levelsChanged = Debounce(async () => await ReloadAsync(), 250);
            chkTrace.CheckedChanged += levelsChanged;
            chkDebug.CheckedChanged += levelsChanged;
            chkInfo.CheckedChanged += levelsChanged;
            chkWarn.CheckedChanged += levelsChanged;
            chkError.CheckedChanged += levelsChanged;
            chkFatal.CheckedChanged += levelsChanged;

            btnAllLevels.Click += async (_, __) =>
            {
                SetAllLevels(true);
                await ReloadAsync();
            };

            btnNoneLevels.Click += async (_, __) =>
            {
                SetAllLevels(false);
                await ReloadAsync();
            };

            grid.SelectionChanged += (_, __) => ShowSelectedDetails();
            grid.DoubleClick += (_, __) => CopyDetails();

            FormClosing += (_, __) =>
            {
                try { _cts?.Cancel(); }
                finally { _cts?.Dispose(); }
            };
        }

        private void ApplyDebugVisibility()
        {
            // Hide/show Debug + Trace checkboxes based on ShowDebug
            chkTrace.Visible = _showDebug;
            chkDebug.Visible = _showDebug;

            if (!_showDebug)
            {
                chkTrace.Checked = false;
                chkDebug.Checked = false;
            }
            else
            {
                // When enabling, default them ON (you can change manually)
                chkTrace.Checked = true;
                chkDebug.Checked = true;
            }
        }

        private void SetAllLevels(bool on)
        {
            // Respect ShowDebug: only affect Trace/Debug if they are visible
            if (chkTrace.Visible) chkTrace.Checked = on;
            if (chkDebug.Visible) chkDebug.Checked = on;

            chkInfo.Checked = on;
            chkWarn.Checked = on;
            chkError.Checked = on;
            chkFatal.Checked = on;
        }

        private LevelMask GetSelectedLevelMask()
        {
            LevelMask m = LevelMask.None;

            if (chkTrace.Visible && chkTrace.Checked) m |= LevelMask.Trace;
            if (chkDebug.Visible && chkDebug.Checked) m |= LevelMask.Debug;
            if (chkInfo.Checked) m |= LevelMask.Info;
            if (chkWarn.Checked) m |= LevelMask.Warn;
            if (chkError.Checked) m |= LevelMask.Error;
            if (chkFatal.Checked) m |= LevelMask.Fatal;

            return m;
        }

        private void SetToday()
        {
            var d = DateTime.Now.Date;
            dtFrom.Value = d;
            dtTo.Value = d;
        }

        private void SetYesterday()
        {
            var d = DateTime.Now.Date.AddDays(-1);
            dtFrom.Value = d;
            dtTo.Value = d;
        }

        private async Task ReloadAsync()
        {
            var myVersion = Interlocked.Increment(ref _loadVersion);

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var from = dtFrom.Value.Date;
            var to = dtTo.Value.Date;
            if (to < from) (from, to) = (to, from);

            (_prefix, _ext) = AutoDetectPrefixExt(_dir, _prefix, _ext);
            UpdateTitle();

            var search = txtSearch.Text?.Trim();
            var levelMask = GetSelectedLevelMask();

            SetUiLoading(true, "Loading...");

            try
            {
                var res = await Task.Run(() =>
                    LogLoader.LoadRange(_dir, _prefix, _ext, from, to, levelMask, search, token), token);

                if (myVersion != Volatile.Read(ref _loadVersion)) return;
                if (token.IsCancellationRequested) return;
                if (IsDisposed || !IsHandleCreated) return;

                _rows.RaiseListChangedEvents = false;
                _rows.Clear();
                foreach (var r in res.Rows)
                    _rows.Add(r);
                _rows.RaiseListChangedEvents = true;
                _rows.ResetBindings();

                statusLeft.Text = "Ready";
                statusRight.Text = $"Rows: {_rows.Count} | Files: {res.FilesFound} | Lines: {res.LinesRead} | Parsed: {res.ParsedOk}";

                if (_rows.Count > 0)
                {
                    grid.ClearSelection();
                    grid.Rows[0].Selected = true;
                }

                ShowSelectedDetails();
            }
            catch (OperationCanceledException)
            {
                // Expected during fast typing/date changes/toggles
            }
            catch (Exception ex)
            {
                statusLeft.Text = "Error";
                MessageBox.Show(ex.Message, "Log Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetUiLoading(false, "Ready");
            }
        }

        private void SetUiLoading(bool loading, string text)
        {
            statusLeft.Text = text;
            btnReload.Enabled = !loading;
            dtFrom.Enabled = !loading;
            dtTo.Enabled = !loading;
            txtSearch.Enabled = !loading;

            chkTrace.Enabled = !loading;
            chkDebug.Enabled = !loading;
            chkInfo.Enabled = !loading;
            chkWarn.Enabled = !loading;
            chkError.Enabled = !loading;
            chkFatal.Enabled = !loading;

            btnAllLevels.Enabled = !loading;
            btnNoneLevels.Enabled = !loading;
        }

        private void ShowSelectedDetails()
        {
            if (grid.CurrentRow?.DataBoundItem is not LogRow row)
            {
                rtbDetails.Clear();
                return;
            }

            rtbDetails.Text =
$@"Time      : {row.Timestamp}
Level     : {row.Level}
Source    : {row.Source}
User      : {row.UserName}
SessionId : {row.SessionCode}
Message   : {row.Message}

Exception :
{row.Exception}

Extras    :
{row.ExtrasJson}";
        }

        private void CopySelectedRow()
        {
            if (grid.CurrentRow?.DataBoundItem is not LogRow r) return;

            var sb = new StringBuilder();
            sb.AppendLine($"Time: {r.Timestamp}");
            sb.AppendLine($"Level: {r.Level}");
            sb.AppendLine($"Source: {r.Source}");
            sb.AppendLine($"User: {r.UserName}");
            sb.AppendLine($"Message: {r.Message}");
            Clipboard.SetText(sb.ToString());
        }

        private void CopyDetails()
        {
            if (!string.IsNullOrWhiteSpace(rtbDetails.Text))
                Clipboard.SetText(rtbDetails.Text);
        }

        private void UpdateTitle()
        {
            Text = $"Log Viewer  [{_prefix}{_ext}]";
        }

        private static Label MakeLabel(string t) => new() { Text = t, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        private static Control MakeSpacer(int w) => new Panel { Width = w, Height = 1 };

        private static EventHandler Debounce(Func<Task> action, int ms)
        {
            System.Windows.Forms.Timer? t = null;

            return (_, __) =>
            {
                t?.Stop();
                t?.Dispose();

                t = new System.Windows.Forms.Timer { Interval = ms };
                t.Tick += async (_, __) =>
                {
                    t!.Stop();
                    t.Dispose();
                    t = null;

                    try { await action(); }
                    catch (OperationCanceledException) { }
                    catch { /* optional: log */ }
                };
                t.Start();
            };
        }

        private static (string prefix, string ext) AutoDetectPrefixExt(string dir, string fallbackPrefix, string fallbackExt)
        {
            Directory.CreateDirectory(dir);

            var rx = new Regex(@"^(?<prefix>.+)_(?<date>\d{4}-\d{2}-\d{2})(?<ext>\.[^.]+)$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

            var counts = new Dictionary<(string p, string e), int>();

            foreach (var file in Directory.EnumerateFiles(dir))
            {
                var name = Path.GetFileName(file);
                if (string.IsNullOrWhiteSpace(name)) continue;

                var m = rx.Match(name);
                if (!m.Success) continue;

                var p = m.Groups["prefix"].Value;
                var e = m.Groups["ext"].Value;

                var key = (p, e);
                counts.TryGetValue(key, out var c);
                counts[key] = c + 1;
            }

            if (counts.Count == 0)
                return (fallbackPrefix, fallbackExt);

            (string p, string e) best = (fallbackPrefix, fallbackExt);
            int bestCount = -1;
            foreach (var kv in counts)
            {
                if (kv.Value > bestCount)
                {
                    bestCount = kv.Value;
                    best = kv.Key;
                }
            }

            return best;
        }
    }

    internal sealed class BufferedDataGridView : DataGridView
    {
        public BufferedDataGridView()
        {
            DoubleBuffered = true;
            EnableHeadersVisualStyles = false;
        }
    }

    internal sealed class LoadResult
    {
        public List<LogRow> Rows { get; } = new();
        public int FilesFound { get; set; }
        public int LinesRead { get; set; }
        public int ParsedOk { get; set; }
    }

    internal static class LogLoader
    {
        public static LoadResult LoadRange(
            string dir,
            string prefix,
            string ext,
            DateTime fromDate,
            DateTime toDate,
            // NEW: level mask instead of includeDebug
            object levelMaskObj,
            string? search,
            CancellationToken ct)
        {
            var levelMask = (FormLogViewer.LevelMask)levelMaskObj; // internal enum access workaround if needed

            Directory.CreateDirectory(dir);

            var res = new LoadResult();
            var files = new List<string>();

            for (var d = fromDate.Date; d <= toDate.Date; d = d.AddDays(1))
            {
                if (ct.IsCancellationRequested) return res;

                var name = $"{prefix}_{d:yyyy-MM-dd}{ext}";
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                {
                    files.Add(path);
                    res.FilesFound++;
                }
            }

            // If no levels selected -> show nothing (fast return after counting files)
            if (levelMask == FormLogViewer.LevelMask.None)
                return res;

            var hasSearch = !string.IsNullOrWhiteSpace(search);
            var needle = hasSearch ? search!.Trim() : "";

            foreach (var file in files)
            {
                if (ct.IsCancellationRequested) return res;

                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);

                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (ct.IsCancellationRequested) return res;

                    res.LinesRead++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (!TryParseJsonLine(line, out var r) && !TryParseTabLine(line, out r))
                        continue;

                    res.ParsedOk++;

                    // NEW: Level filter
                    var rowMask = FormLogViewer.MaskFor(r.Level);
                    if ((levelMask & rowMask) == 0)
                        continue;

                    // Search filter
                    if (hasSearch)
                    {
                        if (!ContainsCI(r.Message, needle) &&
                            !ContainsCI(r.Source, needle) &&
                            !ContainsCI(r.UserName, needle) &&
                            !ContainsCI(r.Exception, needle) &&
                            !ContainsCI(r.ExtrasJson, needle))
                            continue;
                    }

                    res.Rows.Add(r);
                }
            }

            if (!ct.IsCancellationRequested)
                res.Rows.Sort((a, b) => b.TimestampUtc.CompareTo(a.TimestampUtc));

            return res;
        }

        private static bool ContainsCI(string? hay, string needle)
            => !string.IsNullOrEmpty(hay) &&
               hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool TryParseJsonLine(string line, out LogRow row)
        {
            row = default!;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var tsStr = root.TryGetProperty("timestamp", out var tsEl) ? tsEl.GetString() : null;
                var levelStr = root.TryGetProperty("level", out var lvEl) ? lvEl.GetString() : null;

                if (!TryParseTimestamp(tsStr, out var tsUtc))
                    return false;

                var level = ParseLevel(levelStr);
                var source = root.TryGetProperty("source", out var srcEl) ? (srcEl.GetString() ?? "") : "";
                var msg = root.TryGetProperty("message", out var msgEl) ? (msgEl.GetString() ?? "") : "";

                string exText = "";
                if (root.TryGetProperty("exceptionType", out _) ||
                    root.TryGetProperty("exceptionMessage", out _) ||
                    root.TryGetProperty("stackTrace", out _))
                {
                    var t = root.TryGetProperty("exceptionType", out var x1) ? x1.GetString() : "";
                    var m = root.TryGetProperty("exceptionMessage", out var x2) ? x2.GetString() : "";
                    var s = root.TryGetProperty("stackTrace", out var x3) ? x3.GetString() : "";
                    exText = $"{t}\r\n{m}\r\n{s}".Trim();
                }

                string extrasJson = "";
                string user = "";
                string sessionCode = "";
                if (root.TryGetProperty("extras", out var extrasEl) && extrasEl.ValueKind != JsonValueKind.Null)
                {
                    extrasJson = extrasEl.GetRawText();
                    if (extrasEl.ValueKind == JsonValueKind.Object &&
                        extrasEl.TryGetProperty("username", out var unEl))
                        user = unEl.GetString() ?? "";
                    if (extrasEl.ValueKind == JsonValueKind.Object &&
                        extrasEl.TryGetProperty("SessionCode", out var scEl))
                        sessionCode = scEl.GetString() ?? "";
                }

                row = new LogRow(tsUtc, level, source, msg, user, sessionCode, exText, extrasJson);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseTabLine(string line, out LogRow row)
        {
            row = default!;

            var parts = line.Split(new[] { '\t' }, 6);
            if (parts.Length < 4) return false;

            var tsStr = parts[0];
            var levelStr = parts.Length > 1 ? parts[1] : "";
            var source = parts.Length > 2 ? parts[2] : "";
            var msg = parts.Length > 3 ? parts[3] : "";
            var exPart = parts.Length > 4 ? parts[4] : "";
            var extrasJson = parts.Length > 5 ? parts[5] : "";

            if (!TryParseTimestamp(tsStr, out var tsUtc))
                return false;

            var level = ParseLevel(levelStr);

            var user = "";
            var sessionCode = "";
            if (!string.IsNullOrWhiteSpace(extrasJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(extrasJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                        doc.RootElement.TryGetProperty("username", out var un))
                        user = un.GetString() ?? "";
                    if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                        doc.RootElement.TryGetProperty("SessionCode", out var sc))
                        sessionCode = sc.GetString() ?? "";
                }
                catch { }
            }

            row = new LogRow(tsUtc, level, source, msg, user, sessionCode, exPart, extrasJson);
            return true;
        }

        private static bool TryParseTimestamp(string? s, out DateTimeOffset utc)
        {
            utc = default;
            if (string.IsNullOrWhiteSpace(s)) return false;

            var formats = new[]
            {
                "yyyy-MM-dd HH:mm:ss.fff zzz",
                "yyyy-MM-dd HH:mm:ss zzz",
                "yyyy-MM-ddTHH:mm:ss.fffzzz",
                "yyyy-MM-ddTHH:mm:sszzz"
            };

            if (DateTimeOffset.TryParseExact(s, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces, out var dto))
            {
                utc = dto.ToUniversalTime();
                return true;
            }

            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out dto))
            {
                utc = dto.ToUniversalTime();
                return true;
            }

            return false;
        }

        private static LogLevel ParseLevel(string? s)
        {
            if (Enum.TryParse(s ?? "", true, out LogLevel lvl))
                return lvl;
            return LogLevel.Info;
        }
    }

    internal readonly struct LogRow
    {
        public LogRow(DateTimeOffset timestampUtc, LogLevel level, string source, string message,
            string userName, string sessionCode, string exception, string extrasJson)
        {
            TimestampUtc = timestampUtc;
            Level = level;
            Source = source ?? "";
            Message = message ?? "";
            UserName = userName ?? "";
            Exception = exception ?? "";
            ExtrasJson = extrasJson ?? "";
            SessionCode = sessionCode ?? "";
        }

        public DateTimeOffset TimestampUtc { get; }
        public string Timestamp => TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");

        public LogLevel Level { get; }
        public string Source { get; }
        public string UserName { get; }
        public string SessionCode { get; }
        public string Message { get; }
        public string Exception { get; }
        public string ExtrasJson { get; }
    }
}
