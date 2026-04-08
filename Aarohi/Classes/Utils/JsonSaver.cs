using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aarohi.Classes.Utils
{
    public static class JsonPayloadStore
    {
        private static readonly JsonSerializerOptions _opts = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // -------- Core Save / Load (Dictionary) --------
        public static void Save(string filePath, IDictionary<string, object?> payload)
        {
            if (filePath is null) throw new ArgumentNullException(nameof(filePath));
            var json = JsonSerializer.Serialize(payload, _opts);
            File.WriteAllText(filePath, json);
        }

        public static Dictionary<string, object?> Load(string filePath)
        {
            if (filePath is null) throw new ArgumentNullException(nameof(filePath));
            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            return (Dictionary<string, object?>)FromJsonElement(doc.RootElement)!;
        }

        // -------- Core Save / Load (JsonObject) --------
        public static void Save(string filePath, JsonObject payload)
        {
            if (filePath is null) throw new ArgumentNullException(nameof(filePath));
            File.WriteAllText(filePath, payload.ToJsonString(_opts));
        }

        public static JsonObject LoadAsNode(string filePath)
        {
            if (filePath is null) throw new ArgumentNullException(nameof(filePath));
            var json = File.ReadAllText(filePath);
            return JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidDataException("Root is not a JSON object.");
        }

        // -------- Dialog-based helpers --------
        public static bool SaveWithDialog(IDictionary<string, object?> payload, IWin32Window owner, string defaultFile = "payload.json")
        {
            using var sfd = new SaveFileDialog
            {
                Title = "Save Payload",
                Filter = "JSON (*.json)|*.json",
                FileName = defaultFile
            };
            if (sfd.ShowDialog(owner) != DialogResult.OK) return false;
            Save(sfd.FileName, payload);
            return true;
        }

        public static bool LoadWithDialog(IWin32Window owner, out Dictionary<string, object?>? payload)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Open Payload",
                Filter = "JSON (*.json)|*.json"
            };
            if (ofd.ShowDialog(owner) != DialogResult.OK)
            {
                payload = null;
                return false;
            }
            payload = Load(ofd.FileName);
            return true;
        }

        // -------- Optional backup --------
        public static string SaveBackup(string directory, IDictionary<string, object?> payload, string baseName = "design")
        {
            Directory.CreateDirectory(directory);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var path = Path.Combine(directory, $"{baseName}_{stamp}.json");
            Save(path, payload);
            return path;
        }

        // -------- Private helper: convert JsonElement to object --------
        private static object? FromJsonElement(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object?>();
                    foreach (var p in e.EnumerateObject())
                        dict[p.Name] = FromJsonElement(p.Value);
                    return dict;

                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var item in e.EnumerateArray())
                        list.Add(FromJsonElement(item));
                    return list;

                case JsonValueKind.String:
                    return e.GetString();

                case JsonValueKind.Number:
                    if (e.TryGetInt64(out var l))
                        return (l >= int.MinValue && l <= int.MaxValue) ? (int)l : l;
                    if (e.TryGetDouble(out var d))
                        return d;
                    return e.GetRawText();

                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined: return null;
                default: return null;
            }
        }
    }

}
