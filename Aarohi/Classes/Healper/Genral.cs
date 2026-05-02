using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Aarohi.Classes.Healper
{
    public static class Genral
    {
        public static string FolderBuilder(string folderPath, out bool created)
        {
            return SafeFolderPath(folderPath, out created, "DefaultFolder");
        }

        public static string FolderBuilder(string folderPath, out bool created, string name)
        {
            return SafeFolderPath(folderPath, out created, name);
        }

        public static string SafeFolderPath(string defaultPath, out bool created, string name)
        {
            created = false;

            string registryPath = LoadPathFromRegistry(name);

            if (TryCreateOrValidateFolder(registryPath, out string validRegistryPath, out created))
                return validRegistryPath;

            if (TryCreateOrValidateFolder(defaultPath, out string validDefaultPath, out created))
            {
                SavePathToRegistry(name, validDefaultPath);
                return validDefaultPath;
            }

            MessageBox.Show(
                $"Application could not open/create this folder.\n\n" +
                $"Path Name: {name}\n\n" +
                $"Default Path:\n{defaultPath}\n\n" +
                $"Please select folder manually.",
                "Folder Path Required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            string selectedPath = PickFolder(name, defaultPath);

            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                Directory.CreateDirectory(selectedPath);
                SavePathToRegistry(name, selectedPath);
                created = false;
                return selectedPath;
            }

            string fallback = GetFallbackFolder(name);
            Directory.CreateDirectory(fallback);
            SavePathToRegistry(name, fallback);
            created = true;

            return fallback;
        }

        public static string SafeFilePath(
            string defaultFilePath,
            string name,
            string title = "Select File",
            string filter = "All Files (*.*)|*.*")
        {
            string registryPath = LoadPathFromRegistry(name);

            if (IsValidFile(registryPath))
                return registryPath;

            if (IsValidFile(defaultFilePath))
            {
                SavePathToRegistry(name, defaultFilePath);
                return defaultFilePath;
            }

            MessageBox.Show(
                $"Application could not find this file.\n\n" +
                $"Path Name: {name}\n\n" +
                $"Default File:\n{defaultFilePath}\n\n" +
                $"Please select file manually.",
                "File Path Required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            string selectedFile = PickFile(name, defaultFilePath, title, filter);

            if (!string.IsNullOrWhiteSpace(selectedFile))
            {
                SavePathToRegistry(name, selectedFile);
                return selectedFile;
            }

            return defaultFilePath;
        }

        private static bool TryCreateOrValidateFolder(
            string path,
            out string validPath,
            out bool created)
        {
            validPath = string.Empty;
            created = false;

            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return false;

                string fullPath = Path.GetFullPath(path);

                if (Directory.Exists(fullPath))
                {
                    validPath = fullPath;
                    return true;
                }

                Directory.CreateDirectory(fullPath);

                if (Directory.Exists(fullPath))
                {
                    validPath = fullPath;
                    created = true;
                    return true;
                }
            }
            catch
            {
                // Path failed.
            }

            return false;
        }

        private static bool IsValidFile(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return false;

                return File.Exists(Path.GetFullPath(path));
            }
            catch
            {
                return false;
            }
        }

        private static string PickFolder(string name, string defaultPath)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = $"Select folder for: {name}";
                dialog.ShowNewFolderButton = true;

                string nearest = GetNearestExistingFolder(defaultPath);

                if (!string.IsNullOrWhiteSpace(nearest) && Directory.Exists(nearest))
                    dialog.SelectedPath = nearest;

                return dialog.ShowDialog() == DialogResult.OK
                    ? dialog.SelectedPath
                    : string.Empty;
            }
        }

        private static string PickFile(
            string name,
            string defaultFilePath,
            string title,
            string filter)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = string.IsNullOrWhiteSpace(title)
                    ? $"Select file for: {name}"
                    : title;

                dialog.Filter = string.IsNullOrWhiteSpace(filter)
                    ? "All Files (*.*)|*.*"
                    : filter;

                dialog.CheckFileExists = true;
                dialog.CheckPathExists = true;

                string nearest = GetNearestExistingFolder(defaultFilePath);

                if (!string.IsNullOrWhiteSpace(nearest) && Directory.Exists(nearest))
                    dialog.InitialDirectory = nearest;

                return dialog.ShowDialog() == DialogResult.OK
                    ? dialog.FileName
                    : string.Empty;
            }
        }

        private static string LoadPathFromRegistry(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return string.Empty;

                return RegistryHelper.LoadEncrypted(
                    RegistryHelper.storeLocs.Paths,
                    name,
                    string.Empty);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void SavePathToRegistry(string name, string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return;

                if (string.IsNullOrWhiteSpace(path))
                    return;

                RegistryHelper.SaveEncrypted(
                    RegistryHelper.storeLocs.Paths,
                    name,
                    path);
            }
            catch
            {
                // Registry failed, but application should not crash.
            }
        }

        private static string GetNearestExistingFolder(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                string currentPath = path;

                if (File.Exists(currentPath))
                    return Path.GetDirectoryName(currentPath);

                if (Path.HasExtension(currentPath))
                    currentPath = Path.GetDirectoryName(currentPath);

                currentPath = Path.GetFullPath(currentPath);

                while (!string.IsNullOrWhiteSpace(currentPath))
                {
                    if (Directory.Exists(currentPath))
                        return currentPath;

                    currentPath = Directory.GetParent(currentPath)?.FullName;
                }
            }
            catch
            {
                // ignored
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private static string GetFallbackFolder(string name)
        {
            string safeName = MakeSafeName(name);

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aarohi",
                "IPTS",
                "RecoveredPaths",
                safeName);
        }

        private static string MakeSafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Unknown";

            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Trim();
        }
    }

    public static class DirectoryFileLister
    {
        /// <summary>
        /// Returns list of files from a directory, optionally filtered by extensions.
        /// If extensions are not provided, returns all files.
        /// Extensions can be: "json", ".json", "*.json".
        /// </summary>
        public static List<string> GetFiles(
            string directoryPath,
            bool returnFullPath = false,
            bool includeSubDirectories = false,
            params string[] extensions)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directoryPath))
                    return new List<string>();

                if (!Directory.Exists(directoryPath))
                    return new List<string>();

                IEnumerable<string> files = includeSubDirectories
                    ? EnumerateFilesSafe(directoryPath)
                    : Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly);

                if (extensions != null && extensions.Length > 0)
                {
                    HashSet<string> extSet = new HashSet<string>(
                        extensions
                            .Where(e => !string.IsNullOrWhiteSpace(e))
                            .Select(NormalizeExtension),
                        StringComparer.OrdinalIgnoreCase);

                    files = files.Where(f => extSet.Contains(Path.GetExtension(f)));
                }

                return returnFullPath
                    ? files.ToList()
                    : files.Select(Path.GetFileName)
                           .Where(x => !string.IsNullOrWhiteSpace(x))
                           .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static IEnumerable<string> EnumerateFilesSafe(string rootPath)
        {
            Queue<string> folders = new Queue<string>();
            folders.Enqueue(rootPath);

            while (folders.Count > 0)
            {
                string currentFolder = folders.Dequeue();

                string[] files = Array.Empty<string>();
                string[] subFolders = Array.Empty<string>();

                try
                {
                    files = Directory.GetFiles(currentFolder);
                }
                catch
                {
                    // ignored
                }

                foreach (string file in files)
                    yield return file;

                try
                {
                    subFolders = Directory.GetDirectories(currentFolder);
                }
                catch
                {
                    // ignored
                }

                foreach (string folder in subFolders)
                    folders.Enqueue(folder);
            }
        }

        private static string NormalizeExtension(string ext)
        {
            ext = ext.Trim();

            if (ext.StartsWith("*."))
                ext = ext.Substring(1);

            if (!ext.StartsWith("."))
                ext = "." + ext;

            return ext;
        }
    }
}