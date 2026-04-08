using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Classes.Healper
{
    public static class Genral
    {
        public static string FolderBuilder(string folderPath, out bool created)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("Folder path cannot be null or empty.");

            string fullPath = Path.GetFullPath(folderPath);

            if (Directory.Exists(fullPath))
            {
                created = false;
                return fullPath;
            }

            Directory.CreateDirectory(fullPath);
            created = true;
            return fullPath;
        }
    }

    public static class DirectoryFileLister
    {
        /// <summary>
        /// Returns list of files from a directory, optionally filtered by extensions.
        /// - If extensions are not provided => returns all files.
        /// - Extensions can be: "json", ".json", "*.json"
        /// </summary>
        public static List<string> GetFiles(
            string directoryPath,
            bool returnFullPath = false,
            bool includeSubDirectories = false,
            params string[]? extensions
        )
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                return new List<string>();

            if (!Directory.Exists(directoryPath))
                return new List<string>();

            var option = includeSubDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            // Get all files first
            var files = Directory.EnumerateFiles(directoryPath, "*", option);

            // If extensions provided => filter
            if (extensions != null && extensions.Length > 0)
            {
                var extSet = new HashSet<string>(
                    extensions
                        .Where(e => !string.IsNullOrWhiteSpace(e))
                        .Select(NormalizeExtension),
                    StringComparer.OrdinalIgnoreCase
                );

                files = files.Where(f => extSet.Contains(Path.GetExtension(f)));
            }

            // Return full path or file name only
            return returnFullPath
                ? files.ToList()
                : files.Select(Path.GetFileName).ToList();
        }

        private static string NormalizeExtension(string ext)
        {
            ext = ext.Trim();

            // "*.json" -> ".json"
            if (ext.StartsWith("*.")) ext = ext.Substring(1);

            // "json" -> ".json"
            if (!ext.StartsWith(".")) ext = "." + ext;

            return ext;
        }
    }

}
