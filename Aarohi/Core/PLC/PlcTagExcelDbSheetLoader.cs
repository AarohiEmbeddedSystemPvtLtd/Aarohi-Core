using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Aarohi.Core.PLC.ClassPLC;

namespace Aarohi.Core.PLC
{
    public static class PlcTagRegistry
    {
        // Key format: "DbName.TagName"
        private static Dictionary<string, PlcTagInfo> _byFullName =
            new Dictionary<string, PlcTagInfo>(StringComparer.OrdinalIgnoreCase);

        public static void Initialize(string excelPath, bool hasHeader = true)
        {
            var all = PlcTagExcelLoader.LoadAllSheets(excelPath, hasHeader);

            _byFullName = all
                .Where(x => !string.IsNullOrWhiteSpace(x.DbName) && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => MakeKey(x.DbName, x.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }

        private static string Clean(string s)
        {
            if (s == null) return "";
            s = s.Trim();

            // Remove non-breaking spaces and weird whitespace
            s = s.Replace('\u00A0', ' '); // NBSP
            s = s.Replace("\t", " ");
            while (s.Contains("  ")) s = s.Replace("  ", " ");

            return s.Trim();
        }

        private static string MakeKey(string dbName, string tagName)
            => $"{Clean(dbName)}.{Clean(tagName)}";


        /// <summary>
        /// Accepts "DbName.TagName" OR (dbName, tagName)
        /// </summary>
        public static PlcTagInfo? Get(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;

            var key = Clean(fullName);

            // direct
            if (_byFullName.TryGetValue(key, out var exact))
                return exact;

            // fallback leaf: DB.xxx.yyy.zzz -> DB.zzz
            int firstDot = key.IndexOf('.');
            int lastDot = key.LastIndexOf('.');
            if (firstDot <= 0 || lastDot <= 0 || lastDot == key.Length - 1)
                return null;

            var db = Clean(key.Substring(0, firstDot));
            var leaf = Clean(key.Substring(lastDot + 1));

            if (db.Length == 0 || leaf.Length == 0) return null;

            var leafKey = MakeKey(db, leaf);
            return _byFullName.TryGetValue(leafKey, out var info) ? info : null;
        }

        public static bool Contains(string fullName)
    => Get(fullName) != null;

        public static IEnumerable<string> FindKeys(string containsText)
        {
            containsText = Clean(containsText);
            return _byFullName.Keys
                .Where(k => k.IndexOf(containsText, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(50);
        }


        public static PlcTagInfo? Get(string dbName, string tagName)
            => Get(MakeKey(dbName, tagName));

        public static string Address(string fullName)
            => Get(fullName)?.FullAddress ?? string.Empty;

        public static string Address(string dbName, string tagName)
            => Address(MakeKey(dbName, tagName));

        public static PlcDataType DataType(string fullName)
        {
            var dt = (Get(fullName)?.DataType ?? "").Trim().ToUpperInvariant();
            return MapDataType(dt);
        }

        public static PlcDataType DataType(string dbName, string tagName)
            => DataType(MakeKey(dbName, tagName));

        private static PlcDataType MapDataType(string dt) => dt switch
        {
            "BOOL" => PlcDataType.Bool,
            "BYTE" => PlcDataType.Byte,
            "CHAR" => PlcDataType.Byte,
            "INT" => PlcDataType.Int16,
            "DINT" => PlcDataType.DInt,
            "REAL" => PlcDataType.Real,
            "WORD" => PlcDataType.Word,
            "DWORD" => PlcDataType.DWord,
            "TIME" => PlcDataType.DWord,
            "LREAL" => PlcDataType.Real, // adjust if you add LReal
            _ => PlcDataType.Real
        };

        public static IReadOnlyList<PlcTagInfo> GetAllTags(
    bool orderByDbAndName = true,
    bool includeWarningsOnly = false)
        {
            IEnumerable<PlcTagInfo> q = _byFullName.Values;

            if (includeWarningsOnly)
                q = q.Where(t => !string.IsNullOrWhiteSpace(t.Warning));

            if (orderByDbAndName)
            {
                q = q
                    .OrderBy(t => t.DbNumber)
                    .ThenBy(t => t.DbName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase);
            }

            // Snapshot list (read-only wrapper)
            return q.ToList().AsReadOnly();
        }

        public static List<string> GetAllTagsName(bool orderByDbAndName = true)
        {
            // snapshot keys to avoid "collection modified" issues
            var keys = _byFullName.Keys.ToList();

            if (orderByDbAndName)
            {
                keys.Sort(StringComparer.OrdinalIgnoreCase);
            }

            return keys;
        }

    }

    public static class PlcTagExcelLoader
    {
        public static List<PlcTagInfo> LoadAllSheets(string excelPath, bool hasHeader = true)
        {
            if (string.IsNullOrWhiteSpace(excelPath))
                throw new ArgumentException("Excel path is empty.", nameof(excelPath));
            if (!File.Exists(excelPath))
                throw new FileNotFoundException("Excel file not found.", excelPath);

            var result = new List<PlcTagInfo>();

            using var wb = new XLWorkbook(excelPath);

            foreach (var ws in wb.Worksheets)
            {
                string sheetName = ws.Name ?? "";

                var (dbName, dbNumber) = ParseDbInfoFromSheetName(sheetName);

                var lastRow = ws.LastRowUsed();
                if (lastRow == null) continue;

                int startRow = hasHeader ? 2 : 1;
                int endRow = lastRow.RowNumber();

                for (int r = startRow; r <= endRow; r++)
                {
                    string tagName = ws.Cell(r, 1).GetValue<string>().Trim(); // A
                    if (string.IsNullOrWhiteSpace(tagName)) continue;

                    string dataType = ws.Cell(r, 2).GetValue<string>().Trim(); // B
                    string offset = ws.Cell(r, 3).GetValue<string>().Trim(); // C

                    var info = BuildAddress(dbName, dbNumber, sheetName, tagName, dataType, offset);
                    result.Add(info);
                }
            }

            return result;
        }

        /// <summary>
        /// Sheet examples supported:
        /// "Recieve_Data_From_Soft ( DB 3 )"
        /// "Recieve_Data_From_Soft DB 3"
        /// "DB 3"
        /// </summary>
        public static (string DbName, int DbNumber) ParseDbInfoFromSheetName(string sheetName)
        {
            sheetName ??= "";

            // Find DB number
            var m = Regex.Match(sheetName, @"\bDB\s*(\d+)\b", RegexOptions.IgnoreCase);
            int db = (m.Success && int.TryParse(m.Groups[1].Value, out var x)) ? x : 0;

            // DbName = part before DB...
            string namePart = sheetName;
            if (m.Success)
            {
                namePart = sheetName.Substring(0, m.Index).Trim();
            }

            // remove trailing brackets/extra chars
            namePart = namePart.Trim().TrimEnd('(', '-', '_').Trim();

            if (string.IsNullOrWhiteSpace(namePart))
                namePart = $"DB{db}";

            return (namePart, db);
        }

        public static PlcTagInfo BuildAddress(string dbName, int dbNumber, string sheetName, string name, string dataType, string offsetRaw)
        {
            name ??= "";
            dataType ??= "";
            offsetRaw ??= "";
            dbName ??= "";

            string dt = NormalizeType(dataType);
            string off = offsetRaw.Trim();

            string addr;
            int byteLen;
            string? warn = null;

            if (dbNumber <= 0)
                warn = "DB number not found in sheet name (expected 'DB <n>').";

            // BOOL expects byte.bit
            if (dt == "BOOL")
            {
                var parts = off.Replace(',', '.').Split('.');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int b) &&
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int bit))
                {
                    if (bit < 0 || bit > 7) warn = Append(warn, $"Bit index out of range (0..7): {bit}");
                    addr = $"DB{dbNumber}.DBX{b}.{bit}";
                }
                else if (int.TryParse(off, NumberStyles.Integer, CultureInfo.InvariantCulture, out int onlyByte))
                {
                    warn = Append(warn, "BOOL offset missing bit. Assuming .0");
                    addr = $"DB{dbNumber}.DBX{onlyByte}.0";
                }
                else
                {
                    warn = Append(warn, $"Invalid BOOL offset: '{offsetRaw}'");
                    addr = $"DB{dbNumber}.DBX0.0";
                }

                byteLen = 1;

                return new PlcTagInfo
                {
                    DbName = dbName,
                    DbNumber = dbNumber,
                    SheetName = sheetName,
                    Name = name,
                    DataType = dt,
                    OffsetRaw = offsetRaw,
                    FullAddress = addr,
                    ByteLength = byteLen,
                    Warning = warn
                };
            }

            // others expect byte offset
            if (!int.TryParse(off, NumberStyles.Integer, CultureInfo.InvariantCulture, out int byteOffset))
            {
                warn = Append(warn, $"Invalid byte offset: '{offsetRaw}'");
                byteOffset = 0;
            }

            switch (dt)
            {
                case "BYTE":
                case "CHAR":
                    addr = $"DB{dbNumber}.DBB{byteOffset}";
                    byteLen = 1;
                    break;

                case "WORD":
                case "UINT":
                case "INT":
                    addr = $"DB{dbNumber}.DBW{byteOffset}";
                    byteLen = 2;
                    break;

                case "DWORD":
                case "UDINT":
                case "DINT":
                case "REAL":
                case "TIME":
                    addr = $"DB{dbNumber}.DBD{byteOffset}";
                    byteLen = 4;
                    break;

                case "LREAL":
                    addr = $"DB{dbNumber}.DBD{byteOffset}";
                    byteLen = 8;
                    warn = Append(warn, "LREAL is 8 bytes; read/write 8 bytes from this offset.");
                    break;

                default:
                    addr = $"DB{dbNumber}.DBB{byteOffset}";
                    byteLen = 0;
                    warn = Append(warn, $"Unknown datatype '{dataType}', defaulted to DBB.");
                    break;
            }

            return new PlcTagInfo
            {
                DbName = dbName,
                DbNumber = dbNumber,
                SheetName = sheetName,
                Name = name,
                DataType = dt,
                OffsetRaw = offsetRaw,
                FullAddress = addr,
                ByteLength = byteLen,
                Warning = warn
            };
        }

        private static string NormalizeType(string s)
        {
            s = (s ?? "").Trim().ToUpperInvariant();

            if (s == "FLOAT") return "REAL";
            if (s == "DOUBLE") return "LREAL";
            if (s == "BOOLEAN") return "BOOL";

            if (s == "SHORT") return "INT";
            if (s == "USHORT") return "UINT";
            if (s == "LONG") return "DINT";
            if (s == "ULONG") return "UDINT";

            return s;
        }

        private static string Append(string? a, string b)
            => string.IsNullOrWhiteSpace(a) ? b : a + " | " + b;

        // Trial Change
    }
}
