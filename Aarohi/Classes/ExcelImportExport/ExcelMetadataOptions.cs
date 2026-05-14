using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Classes.ExcelImportExport
{
    public class ExcelMetadataOptions
    {
        public string ValidationSheetName { get; set; } = "__ValidationLists";
        public string SheetGuidCellAddress { get; set; } = "XFD1";
        public string SheetGuidFooterPrefix { get; set; } = "Aarohi_Table_Unique_ID: ";

        // Protection settings
        public bool ProtectWorksheet { get; set; } = false;
        public bool ProtectWorkbookStructure { get; set; } = true;

        // Same password will be used for worksheet + workbook structure protection
        public string ProtectionPassword { get; set; } = "Dev@Aarohi";

        // Performance settings
        public bool EnableDataValidation { get; set; } = true;
        public bool EnableAutoFitColumns { get; set; } = false;
        public bool EnableHyperlinks { get; set; } = true;
        public bool EnableBackButtons { get; set; } = true;
        public bool EnableProtection { get; set; } = true;
        public bool ImportOnlyChangedValues { get; set; } = true;

        public List<string> ExtraPropertyColumns { get; set; } = new List<string>();
        public List<string> ExtraBooleanColumns { get; set; } = new List<string>();

        // Columns that should remain locked in exported Excel
        public List<string> LockedColumns { get; set; } = new List<string>
        {
            ExcelColumnFields.Name,
            ExcelColumnFields.DataType,
            ExcelColumnFields.Precision,
            ExcelColumnFields.Scale,
            ExcelColumnFields.Unit
        };
        // Columns that should not be imported back as extended properties
        public List<string> SkipImportColumns { get; set; } = new List<string>
        {
            ExcelColumnFields.Name,
            ExcelColumnFields.DataType,
            ExcelColumnFields.Precision,
            ExcelColumnFields.Scale,
            ExcelColumnFields.Options,
            ExcelColumnFields.DefaultValue
        };

        // Main export columns + extra project columns
        public string[] GetAllExportColumns()
        {
            return ExcelColumnFields.ExportOrder
                .Concat(ExtraPropertyColumns ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        // Default boolean columns + extra project boolean columns
        public string[] GetAllBooleanColumns()
        {
            return ExcelColumnFields.DefaultBooleanColumns
                .Concat(ExtraBooleanColumns ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        public string[] GetLockedColumns()
        {
            return (LockedColumns ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public string[] GetSkipImportColumns()
        {
            return (SkipImportColumns ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public bool ShouldSkipImportProperty(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return true;

            return GetSkipImportColumns()
                .Any(x => string.Equals(x, propertyName, StringComparison.OrdinalIgnoreCase));
        }

        public void AddExtraPropertyColumns(params string[] columnNames)
        {
            AddUniqueItems(ExtraPropertyColumns, columnNames);
        }

        public void AddExtraBooleanColumns(params string[] columnNames)
        {
            AddUniqueItems(ExtraBooleanColumns, columnNames);
        }
        private static void AddUniqueItems(List<string> targetList, IEnumerable<string> values)
        {
            if (targetList == null || values == null)
                return;

            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                string cleanValue = value.Trim();

                bool alreadyExists = targetList.Any(x =>
                    string.Equals(x, cleanValue, StringComparison.OrdinalIgnoreCase));

                if (!alreadyExists)
                    targetList.Add(cleanValue);
            }
        }
    }
}
