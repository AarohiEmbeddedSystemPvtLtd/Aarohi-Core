using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aarohi.Classes;
using static Aarohi.Classes.Dbhand;

namespace Aarohi.Classes.ExcelImportExport
{
    public class ExcelMetadataImportExportService
    {
        private static ExcelMetadataOptions _options = new ExcelMetadataOptions();
        private static readonly object _operationLock = new object();

        // Public property
        public static ExcelMetadataOptions Options
        {
            get { return _options; }
            set { _options = value ?? new ExcelMetadataOptions(); }
        }

        private static ExcelMetadataOptions NormalizeOptions(ExcelMetadataOptions options)
        {
            return options ?? new ExcelMetadataOptions();
        }

        // export async without options
        public static Task ExportAsync(string filePath, CancellationToken token = default)
        {
            return ExportAsync(filePath, new ExcelMetadataOptions(), token);
        }

        // export async with options
        public static Task ExportAsync(string filePath,ExcelMetadataOptions options,CancellationToken token = default)
        {
            return Task.Run(() =>
            {
                lock (_operationLock)
                {
                    Export(filePath, options, token);
                }
            }, token);
        }
        // export selected table async without options
        public static Task ExportSingleTableAsync(string filePath,string selectedTableName,CancellationToken token = default)
        {
            return ExportSingleTableAsync(filePath, selectedTableName, new ExcelMetadataOptions(), token);
        }

        // export selected table async with options
        public static Task ExportSingleTableAsync(string filePath,string selectedTableName,ExcelMetadataOptions options,CancellationToken token = default)
        {
            return Task.Run(() =>
            {
                lock (_operationLock)
                {
                    ExportSingleTable(filePath, selectedTableName, options, token);
                }
            }, token);
        }

        // export selected table without options
        public static void ExportSingleTable(string filePath,string selectedTableName, CancellationToken token = default)
        {
            ExportSingleTable(filePath, selectedTableName, new ExcelMetadataOptions(), token);
        }

        // export selected table with options
        public static void ExportSingleTable(string filePath,string selectedTableName,ExcelMetadataOptions options,CancellationToken token = default)
        {
            options = NormalizeOptions(options);

            ExcelMetadataOptions previousOptions = Options;

            try
            {
                Options = options;

                ValidateExportFilePath(filePath);

                if (string.IsNullOrWhiteSpace(selectedTableName))
                    throw new ArgumentException("Selected table name cannot be empty.", nameof(selectedTableName));

                token.ThrowIfCancellationRequested();

                GenerateExcelExportFileForSingleTable(filePath, selectedTableName, token);
            }
            finally
            {
                Options = previousOptions;
            }
        }

        //import async without options
        public static Task ImportAsync(string filePath, CancellationToken token = default)
        {
            return ImportAsync(filePath, new ExcelMetadataOptions(), token);
        }


        // import async with options
        public static Task ImportAsync(string filePath,ExcelMetadataOptions options,CancellationToken token = default)
        {
            return Task.Run(() =>
            {
                lock (_operationLock)
                {
                    Import(filePath, options, token);
                }
            }, token);
        }
        // import selected table async without options
        public static Task ImportSingleTableAsync(string filePath,string selectedTableName,CancellationToken token = default)
        {
            return ImportSingleTableAsync(filePath, selectedTableName, new ExcelMetadataOptions(), token);
        }

        // import selected table async with options
        public static Task ImportSingleTableAsync(string filePath,string selectedTableName,ExcelMetadataOptions options,CancellationToken token = default)
        {
            return Task.Run(() =>
            {
                lock (_operationLock)
                {
                    ImportSingleTable(filePath, selectedTableName, options, token);
                }
            }, token);
        }

        // import selected table without options
        public static void ImportSingleTable(string filePath,string selectedTableName,CancellationToken token = default)
        {
            ImportSingleTable(filePath, selectedTableName, new ExcelMetadataOptions(), token);
        }

        // import selected table with options
        public static void ImportSingleTable(string filePath,string selectedTableName,ExcelMetadataOptions options,CancellationToken token = default)
        {
            options = NormalizeOptions(options);

            ExcelMetadataOptions previousOptions = Options;

            try
            {
                Options = options;

                ValidateImportFilePath(filePath);

                if (string.IsNullOrWhiteSpace(selectedTableName))
                    throw new ArgumentException("Selected table name cannot be empty.", nameof(selectedTableName));

                token.ThrowIfCancellationRequested();

                ImportExcelFileForSingleTable(filePath, selectedTableName, token);
            }
            finally
            {
                Options = previousOptions;
            }
        }
        // Export without options
        public static void Export(string filePath, CancellationToken token = default)
        {
            Export(filePath, new ExcelMetadataOptions(), token);
        }

        // Export with options
        public static void Export(string filePath,ExcelMetadataOptions options,CancellationToken token = default)
        {
            options = NormalizeOptions(options);

            ExcelMetadataOptions previousOptions = Options;

            try
            {
                Options = options;

                ValidateExportFilePath(filePath);
                token.ThrowIfCancellationRequested();

                GenerateExcelExportFile(filePath, token);
            }
            finally
            {
                Options = previousOptions;
            }
        }

        //import without options
        public static void Import(string filePath, CancellationToken token = default)
        {
            Import(filePath, new ExcelMetadataOptions(), token);
        }

        // import with options
        public static void Import(string filePath,ExcelMetadataOptions options,CancellationToken token = default)
        {
            options = NormalizeOptions(options);

            ExcelMetadataOptions previousOptions = Options;

            try
            {
                Options = options;

                ValidateImportFilePath(filePath);
                token.ThrowIfCancellationRequested();

                ImportExcelFile(filePath, token);
            }
            finally
            {
                Options = previousOptions;
            }
        }
        public static void ValidateExportFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Export file path cannot be empty.", nameof(filePath));

            string fullPath = Path.GetFullPath(filePath);
            string directory = Path.GetDirectoryName(fullPath) ?? "";

            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("Invalid export directory.");

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string extension = Path.GetExtension(fullPath);

            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Export file must be .xlsx file.");
        }

        public static void ValidateImportFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Import file path cannot be empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Import Excel file not found.", filePath);

            string extension = Path.GetExtension(filePath);

            bool isValidExcel =
                string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase);

            if (!isValidExcel)
                throw new InvalidOperationException("Invalid Excel file. Only .xlsx and .xlsm files are allowed.");
        }

        #region Export

        public static void GenerateExcelExportFile(string fileName, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            List<TableInfo> infos = Dbhand.GetTables().ToList();

            if (infos.Count == 0)
                throw new InvalidOperationException("No tables found.");

            using (XLWorkbook workbook = new XLWorkbook())
            {
                IXLWorksheet wsValidationLists = workbook.Worksheets.Add(_options.ValidationSheetName);

                wsValidationLists.Visibility = XLWorksheetVisibility.VeryHidden;

                int validationListRow = 1;

                List<string> allowedDataTypes = GetAllowedDataTypesFromDynamicClass(infos);

                Dictionary<string, string[]> parameterUnitMap = GetParameterUnitMapFromParameterMapping();

                DataTable dtTableMeta = CreateTableMetaExportTable();// This table will hold metadata about tables and will be used during import to maintain identity and mapping

                HashSet<string> usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                HashSet<string> usedExcelTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (TableInfo info in infos)
                {
                    token.ThrowIfCancellationRequested();

                    using (DynamicClass dc = new DynamicClass(info.Schema, info.Name))
                    {
                        string uniqueId = Guid.NewGuid().ToString("D");// create new GUID for each table to maintain identity during import
                        string fullTableName = $"{info.Schema}.{info.Name}";

                        DataRow rowMetaTable = dtTableMeta.NewRow(); //create new row for table metadata

                        SetExportValue(rowMetaTable, ExcelTableFields.UniqueId, uniqueId);
                        SetExportValue(rowMetaTable, ExcelTableFields.Schema, info.Schema);
                        SetExportValue(rowMetaTable, ExcelTableFields.TableName, info.Name);
                        SetExportValue(rowMetaTable, ExcelTableFields.FullTableName, fullTableName);
                        SetExportValue(rowMetaTable, ExcelTableFields.DisplayName, SafeString(dc.GetTableDisplayName()));

                        dtTableMeta.Rows.Add(rowMetaTable);

                        DataTable dtColumnTable = CreateColumnExportTable(info.Name);

                        List<DynamicClass.ColumnInfo> columns = dc.GetColumns() ?? new List<DynamicClass.ColumnInfo>();

                        foreach (DynamicClass.ColumnInfo column in columns)
                        {
                            token.ThrowIfCancellationRequested();

                            DataRow rowColumn = dtColumnTable.NewRow();

                            FillColumnExportRow(rowColumn, dc, column);

                            dtColumnTable.Rows.Add(rowColumn);
                        }

                        string sheetName = MakeSafeWorksheetName(fullTableName, usedSheetNames);

                        string excelTableName = MakeSafeExcelTableName($"Columns_{sheetName}", usedExcelTableNames);

                        IXLWorksheet wsColumns = workbook.Worksheets.Add(sheetName);

                        SetWorksheetGuidIdentity(wsColumns, uniqueId);

                        const int columnHeaderRow = 3;
                        const int columnFirstDataRow = 4;
                        int columnLastDataRow = columnHeaderRow + dtColumnTable.Rows.Count;

                        wsColumns.Cell(columnHeaderRow, 1).InsertTable(dtColumnTable, excelTableName, true);
                        FormatExcelSheet(wsColumns, columnHeaderRow);

                        ApplyColumnValidations(workbook, wsColumns, dtColumnTable, allowedDataTypes, parameterUnitMap, wsValidationLists, ref validationListRow,
                            columnHeaderRow, columnFirstDataRow, columnLastDataRow);
                    }
                }

                string tableMetaName = MakeSafeExcelTableName("Table_Metadata", usedExcelTableNames);

                IXLWorksheet wsTables = workbook.Worksheets.Add(ExcelTableFields.SheetName, 1);

                wsTables.Cell(1, 1).InsertTable(dtTableMeta, tableMetaName, true);

                FormatExcelSheet(wsTables);

                AddTableSheetHyperlinks(workbook);
                AddBackToTablesButtons(workbook);

                // Main protection: all sheets + workbook structure.
                ApplyFinalWorkbookProtection(workbook);

                workbook.SaveAs(fileName, new SaveOptions
                {
                    ValidatePackage = false,
                    EvaluateFormulasBeforeSaving = false,
                    GenerateCalculationChain = false,
                    ConsolidateConditionalFormatRanges = false,
                    ConsolidateDataValidationRanges = false
                });
            }
        }
        public static void GenerateExcelExportFileForSingleTable(string fileName,string selectedTableName,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            List<TableInfo> allTables = Dbhand.GetTables().ToList();

            if (allTables.Count == 0)
                throw new InvalidOperationException("No tables found.");

            TableInfo selectedInfo = GetSelectedTableInfo(selectedTableName, allTables);

            using (XLWorkbook workbook = new XLWorkbook())
            {
                IXLWorksheet wsValidationLists = workbook.Worksheets.Add(_options.ValidationSheetName);
                wsValidationLists.Visibility = XLWorksheetVisibility.VeryHidden;

                int validationListRow = 1;

                // Keep all DB data types in dropdown, same as full export.
                List<string> allowedDataTypes = GetAllowedDataTypesFromDynamicClass(allTables);

                Dictionary<string, string[]> parameterUnitMap = GetParameterUnitMapFromParameterMapping();

                DataTable dtTableMeta = CreateTableMetaExportTable();

                HashSet<string> usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> usedExcelTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                using (DynamicClass dc = new DynamicClass(selectedInfo.Schema, selectedInfo.Name))
                {
                    string uniqueId = Guid.NewGuid().ToString("D");
                    string fullTableName = $"{selectedInfo.Schema}.{selectedInfo.Name}";

                    DataRow rowMetaTable = dtTableMeta.NewRow();

                    SetExportValue(rowMetaTable, ExcelTableFields.UniqueId, uniqueId);
                    SetExportValue(rowMetaTable, ExcelTableFields.Schema, selectedInfo.Schema);
                    SetExportValue(rowMetaTable, ExcelTableFields.TableName, selectedInfo.Name);
                    SetExportValue(rowMetaTable, ExcelTableFields.FullTableName, fullTableName);
                    SetExportValue(rowMetaTable, ExcelTableFields.DisplayName, SafeString(dc.GetTableDisplayName()));

                    dtTableMeta.Rows.Add(rowMetaTable);

                    DataTable dtColumnTable = CreateColumnExportTable(selectedInfo.Name);

                    List<DynamicClass.ColumnInfo> columns =dc.GetColumns() ?? new List<DynamicClass.ColumnInfo>();

                    foreach (DynamicClass.ColumnInfo column in columns)
                    {
                        token.ThrowIfCancellationRequested();

                        DataRow rowColumn = dtColumnTable.NewRow();

                        FillColumnExportRow(rowColumn, dc, column);

                        dtColumnTable.Rows.Add(rowColumn);
                    }

                    string sheetName = MakeSafeWorksheetName(fullTableName, usedSheetNames);

                    string excelTableName = MakeSafeExcelTableName($"Columns_{sheetName}",usedExcelTableNames);

                    IXLWorksheet wsColumns = workbook.Worksheets.Add(sheetName);

                    SetWorksheetGuidIdentity(wsColumns, uniqueId);

                    const int columnHeaderRow = 3;
                    const int columnFirstDataRow = 4;
                    int columnLastDataRow = columnHeaderRow + dtColumnTable.Rows.Count;

                    wsColumns.Cell(columnHeaderRow, 1).InsertTable(dtColumnTable, excelTableName, true);
                    FormatExcelSheet(wsColumns, columnHeaderRow);

                    ApplyColumnValidations(workbook,wsColumns,dtColumnTable,allowedDataTypes,parameterUnitMap,wsValidationLists,
                        ref validationListRow,columnHeaderRow,columnFirstDataRow,columnLastDataRow);
                }

                string tableMetaName = MakeSafeExcelTableName("Table_Metadata",usedExcelTableNames);

                IXLWorksheet wsTables = workbook.Worksheets.Add(ExcelTableFields.SheetName, 1);

                wsTables.Cell(1, 1).InsertTable(dtTableMeta, tableMetaName, true);

                FormatExcelSheet(wsTables);

                AddTableSheetHyperlinks(workbook);
                AddBackToTablesButtons(workbook);

                ApplyFinalWorkbookProtection(workbook);

                workbook.SaveAs(fileName, new SaveOptions
                {
                    ValidatePackage = false,
                    EvaluateFormulasBeforeSaving = false,
                    GenerateCalculationChain = false,
                    ConsolidateConditionalFormatRanges = false,
                    ConsolidateDataValidationRanges = false
                });
            }
        }
        public static DataTable CreateTableMetaExportTable()
        {
            DataTable dt = new DataTable(ExcelTableFields.SheetName);

            foreach (string columnName in ExcelTableFields.ExportOrder)
                dt.Columns.Add(columnName);

            return dt;
        }

        public static DataTable CreateColumnExportTable(string tableName)
        {
            DataTable dt = new DataTable(tableName);

            foreach (string columnName in _options.GetAllExportColumns())
            {
                if (!dt.Columns.Contains(columnName))
                    dt.Columns.Add(columnName);
            }

            return dt;
        }

        public static void SetExportValue(DataRow row, string columnName, object value)
        {
            if (row == null || row.Table == null)
                return;

            if (!row.Table.Columns.Contains(columnName))
                return;

            row[columnName] = value == null || value == DBNull.Value ? "" : value;
        }

        public static void FillColumnExportRow(DataRow row, DynamicClass dc, DynamicClass.ColumnInfo column)
        {
            string columnName = column.Name ?? "";

            SetExportValue(row, ExcelColumnFields.Name, columnName);
            SetExportValue(row, ExcelColumnFields.DataType, SafeString(column.DataType));
            SetExportValue(row, ExcelColumnFields.Precision, Convert.ToString(column.Precision) ?? "");
            SetExportValue(row, ExcelColumnFields.Scale, Convert.ToString(column.Scale) ?? "");
            SetExportValue(row, ExcelColumnFields.Options, OptionsToExcelText(column.Options));

            SetExportValue(row, ExcelColumnFields.DefaultValue, column.DefaultValue == null || column.DefaultValue == DBNull.Value ? ""
                    : Convert.ToString(column.DefaultValue)?.Trim() ?? "");

            SetExportValue(row, ExcelColumnFields.DisplayName, SafeString(column.DisplayName));
            SetExportValue(row, ExcelColumnFields.Description, SafeString(column.Description));

            SetExportValue(row, ExcelColumnFields.Unit,
                GetValueFromColumnOrExtendedProperty(dc, columnName, column.Unit, ExcelColumnFields.Unit));

            SetExportValue(row, ExcelColumnFields.DefaultUnit,
                GetValueFromColumnOrExtendedProperty(dc, columnName, column.DefaultUnit, ExcelColumnFields.DefaultUnit));

            SetExportValue(row, ExcelColumnFields.InputUnit,
                GetValueFromColumnOrExtendedProperty(dc, columnName, column.InputUnit, ExcelColumnFields.InputUnit));

            SetExportValue(row, ExcelColumnFields.LastUsedUnit,
                GetValueFromColumnOrExtendedProperty(dc, columnName, column.LastUsedUnit, ExcelColumnFields.LastUsedUnit));

            SetExportValue(row, ExcelColumnFields.ShowUnit,
                GetValueFromColumnOrExtendedProperty(dc, columnName, column.ShowUnit, ExcelColumnFields.ShowUnit));

            SetExportValue(row, ExcelColumnFields.ReportUnit,
                GetValueFromColumnOrExtendedProperty(dc, columnName, column.ReportUnit, ExcelColumnFields.ReportUnit));

            SetExportValue(row, ExcelColumnFields.DatagridShow, GetNullableBoolText(column.DatagridShow));
            SetExportValue(row, ExcelColumnFields.HideInCrudForm, GetNullableBoolText(column.HideInCrudForm));

            SetExportValue(row, ExcelColumnFields.Format,
                GetValueFromColumnOrExtendedProperty(dc, columnName, column.Format, ExcelColumnFields.Format));

            SetExportValue(row, ExcelColumnFields.Parameter,
                GetValueFromColumnOrExtendedProperty(dc, columnName, column.Parameter, ExcelColumnFields.Parameter));

            SetExportValue(row, ExcelColumnFields.Order, Convert.ToString(column.Order) ?? "");
            SetExportValue(row, ExcelColumnFields.Visible, GetNullableBoolText(column.Visible));

            foreach (string propertyName in _options.ExtraPropertyColumns)
            {
                SetExportValue(row, propertyName, GetColumnPropertySafeForExport(dc, columnName, propertyName));
            }
        }
        public static string GetValueFromColumnOrExtendedProperty(DynamicClass dc, string columnName, string columnValue, string propertyName)
        {
            if (!string.IsNullOrWhiteSpace(columnValue))
                return columnValue.Trim();

            return GetColumnPropertySafeForExport(dc, columnName, propertyName);
        }
        public static string GetColumnPropertySafeForExport(DynamicClass dc, string columnName, string propertyName)
        {
            try
            {
                if (dc == null || string.IsNullOrWhiteSpace(columnName) || string.IsNullOrWhiteSpace(propertyName))
                    return "";

                object value = dc.GetColumnProperty(propertyName, columnName);

                return value == null || value == DBNull.Value ? "" : Convert.ToString(value)?.Trim() ?? "";
            }
            catch
            {
                return "";
            }
        }
        public static string OptionsToExcelText(string[] options)
        {
            if (options == null || options.Length == 0)
                return "";

            return string.Join(", ", options.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
        }

        public static string GetNullableBoolText(bool? value)
        {
            return value.HasValue ? value.Value.ToString() : "";
        }

        private static string SafeString(string value)
        {
            return value == null ? "" : value.Trim();
        }

        #endregion

        #region Import

        public static void ImportExcelFile(string filePath, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            using (XLWorkbook workbook = new XLWorkbook(filePath))
            {
                List<TableInfo> databaseTables = Dbhand.GetTables().ToList();

                if (databaseTables.Count == 0)
                    throw new InvalidOperationException("No database tables found.");

                Dictionary<string, TableInfo> guidTableMap = BuildGuidTableMapFromTablesSheet(workbook, databaseTables);

                List<IXLWorksheet> sheets = workbook.Worksheets
                    .Where(ws =>
                        !string.Equals(ws.Name, ExcelTableFields.SheetName, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(ws.Name, _options.ValidationSheetName, StringComparison.OrdinalIgnoreCase))
                    .Where(ws => ws.Visibility == XLWorksheetVisibility.Visible)
                    .ToList();

                foreach (IXLWorksheet ws in sheets)
                {
                    token.ThrowIfCancellationRequested();

                    TableInfo matchedTable = GetMatchingTableInfoByGuid(ws, guidTableMap);

                    using (DynamicClass dc = new DynamicClass(matchedTable.Schema, matchedTable.Name))
                    {
                        DataTable excelTable = ImportWorksheet(ws);

                        if (excelTable.Rows.Count == 0)
                            continue;

                        SaveExtendedProperties(excelTable, dc, token);
                    }
                }
            }
        }
        public static void ImportExcelFileForSingleTable(string filePath,string selectedTableName,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            using (XLWorkbook workbook = new XLWorkbook(filePath))
            {
                List<TableInfo> databaseTables = Dbhand.GetTables().ToList();

                if (databaseTables.Count == 0)
                    throw new InvalidOperationException("No database tables found.");

                TableInfo selectedDbTable = GetSelectedTableInfo(selectedTableName, databaseTables);

                Dictionary<string, TableInfo> guidTableMap =BuildGuidTableMapFromTablesSheet(workbook, databaseTables);

                List<IXLWorksheet> sheets = workbook.Worksheets
                    .Where(ws =>
                        !string.Equals(ws.Name, ExcelTableFields.SheetName, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(ws.Name, _options.ValidationSheetName, StringComparison.OrdinalIgnoreCase))
                    .Where(ws => ws.Visibility == XLWorksheetVisibility.Visible)
                    .ToList();

                bool selectedTableFoundInExcel = false;

                foreach (IXLWorksheet ws in sheets)
                {
                    token.ThrowIfCancellationRequested();

                    TableInfo matchedTable = GetMatchingTableInfoByGuid(ws, guidTableMap);

                    bool isSelectedTable =
                        string.Equals(matchedTable.Schema, selectedDbTable.Schema, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(matchedTable.Name, selectedDbTable.Name, StringComparison.OrdinalIgnoreCase);

                    if (!isSelectedTable)
                        continue;

                    selectedTableFoundInExcel = true;

                    using (DynamicClass dc = new DynamicClass(matchedTable.Schema, matchedTable.Name))
                    {
                        DataTable excelTable = ImportWorksheet(ws);

                        if (excelTable.Rows.Count == 0)
                            continue;

                        SaveExtendedProperties(excelTable, dc, token);
                    }
                }

                if (!selectedTableFoundInExcel)
                {
                    throw new InvalidOperationException(
                        $"Selected table '{selectedDbTable.Schema}.{selectedDbTable.Name}' was not found in this Excel file.");
                }
            }
        }
        public static Dictionary<string, TableInfo> BuildGuidTableMapFromTablesSheet(XLWorkbook workbook, List<TableInfo> databaseTables)
        {
            Dictionary<string, TableInfo> map = new Dictionary<string, TableInfo>(StringComparer.OrdinalIgnoreCase);

            if (!workbook.Worksheets.Contains(ExcelTableFields.SheetName))
                throw new Exception("Tables sheet not found. Cannot import using Unique_ID.");

            IXLWorksheet wsTables = workbook.Worksheet(ExcelTableFields.SheetName);

            int headerRow = FindTablesHeaderRow(wsTables);

            if (headerRow <= 0)
                throw new Exception("Header row not found in Tables sheet.");

            Dictionary<string, int> colMap = GetHeaderMap(wsTables, headerRow);

            if (!colMap.TryGetValue(ExcelTableFields.UniqueId, out int uniqueIdCol))
                throw new Exception("Unique_ID column not found in Tables sheet.");

            if (!colMap.TryGetValue(ExcelTableFields.Schema, out int schemaCol))
                throw new Exception("Schema column not found in Tables sheet.");

            if (!colMap.TryGetValue(ExcelTableFields.TableName, out int tableNameCol))
                throw new Exception("Name column not found in Tables sheet.");

            int lastRow = wsTables.LastRowUsed()?.RowNumber() ?? headerRow;

            for (int row = headerRow + 1; row <= lastRow; row++)
            {
                string uniqueId = wsTables.Cell(row, uniqueIdCol).GetString().Trim();
                string schema = wsTables.Cell(row, schemaCol).GetString().Trim();
                string tableName = wsTables.Cell(row, tableNameCol).GetString().Trim();

                if (string.IsNullOrWhiteSpace(uniqueId) || string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(tableName))
                    continue;

                if (!Guid.TryParse(uniqueId, out _))
                    continue;

                TableInfo matchedDbTable = databaseTables.FirstOrDefault(t =>
                    string.Equals(t.Schema, schema, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase));

                if (IsEmptyTableInfo(matchedDbTable))
                    continue;

                if (!map.ContainsKey(uniqueId))
                    map.Add(uniqueId, matchedDbTable);
            }

            if (map.Count == 0)
                throw new Exception("No valid Unique_ID table mapping found in Tables sheet.");

            return map;
        }

        public static TableInfo GetMatchingTableInfoByGuid(IXLWorksheet worksheet, Dictionary<string, TableInfo> guidTableMap)
        {
            string worksheetName = worksheet.Name.Trim();

            string uniqueId = GetWorksheetUniqueId(worksheet);

            if (string.IsNullOrWhiteSpace(uniqueId))
            {
                throw new Exception(
                    $"Unique_ID not found in worksheet '{worksheetName}'. " +
                    $"Expected GUID in hidden cell {_options.SheetGuidCellAddress}.");
            }

            if (!guidTableMap.TryGetValue(uniqueId, out TableInfo matchedTable))
            {
                throw new Exception(
                    $"No table mapping found for worksheet '{worksheetName}'. " +
                    $"Unique_ID: {uniqueId}");
            }

            if (IsEmptyTableInfo(matchedTable))
            {
                throw new Exception(
                    $"Invalid table mapping for worksheet '{worksheetName}'. " +
                    $"Unique_ID: {uniqueId}");
            }

            return matchedTable;
        }

        public static string GetWorksheetUniqueId(IXLWorksheet ws)
        {
            if (ws == null)
                return "";

            string uniqueId = ws.Cell(_options.SheetGuidCellAddress).GetString().Trim();

            if (!Guid.TryParse(uniqueId, out _))
                return "";

            return uniqueId;
        }

        public static DataTable ImportWorksheet(IXLWorksheet ws)
        {
            DataTable dt = new DataTable();

            int headerRowNumber = FindHeaderRow(ws);

            if (headerRowNumber <= 0)
                throw new Exception($"Header row not found in worksheet: {ws.Name}");

            IXLRow headerRow = ws.Row(headerRowNumber);

            int lastColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

            if (lastColumn <= 0)
                throw new Exception($"No columns found in worksheet: {ws.Name}");

            Dictionary<int, string> excelColumnMap = new Dictionary<int, string>();

            for (int col = 1; col <= lastColumn; col++)
            {
                string columnName = ws.Cell(headerRowNumber, col).GetString().Trim();

                if (string.IsNullOrWhiteSpace(columnName))
                    continue;

                columnName = MakeUniqueDataTableColumnName(dt, columnName);

                dt.Columns.Add(columnName);
                excelColumnMap[col] = columnName;
            }

            if (!dt.Columns.Contains(ExcelColumnFields.Name))
                throw new Exception($"Required column 'Name' not found in worksheet: {ws.Name}");

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRowNumber;

            for (int rowNo = headerRowNumber + 1; rowNo <= lastRow; rowNo++)
            {
                IXLRow row = ws.Row(rowNo);

                bool isEmptyRow = excelColumnMap.Keys.All(col => string.IsNullOrWhiteSpace(row.Cell(col).GetString()));

                if (isEmptyRow)
                    continue;

                DataRow dr = dt.NewRow();

                foreach (KeyValuePair<int, string> item in excelColumnMap)
                {
                    int excelCol = item.Key;
                    string dtColumnName = item.Value;

                    dr[dtColumnName] = row.Cell(excelCol).GetString().Trim();
                }

                string columnNameValue =
                    dr[ExcelColumnFields.Name]?.ToString()?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(columnNameValue))
                    continue;

                dt.Rows.Add(dr);
            }

            return dt;
        }

        public static void SaveExtendedProperties(DataTable excelTable, DynamicClass dc, CancellationToken token)
        {
            if (excelTable == null || excelTable.Rows.Count == 0)
                return;

            if (!excelTable.Columns.Contains(ExcelColumnFields.Name))
                throw new Exception("Excel table does not contain required column 'Name'.");

            foreach (DataRow row in excelTable.Rows)
            {
                token.ThrowIfCancellationRequested();

                string columnName = row[ExcelColumnFields.Name]?.ToString()?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(columnName))
                    continue;

                columnName = columnName.Replace(" ", "").Trim();

                foreach (DataColumn column in excelTable.Columns)
                {
                    token.ThrowIfCancellationRequested();

                    string propertyName = column.ColumnName?.Trim() ?? "";

                    if (_options.ShouldSkipImportProperty(propertyName))
                        continue;

                    object value = row[column];

                    if (value == null || value == DBNull.Value)
                        continue;

                    string propertyValue = value.ToString()?.Trim() ?? "";

                    if (string.IsNullOrWhiteSpace(propertyValue))
                        continue;

                    try
                    {
                        dc.SetColumnProperty(propertyName, columnName, propertyValue);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Import property failed. Column={columnName}, Property={propertyName}, Error={ex.Message}");
                    }
                }
            }
        }

        public static string MakeUniqueDataTableColumnName(DataTable dt, string columnName)
        {
            string finalName = columnName;
            int counter = 1;

            while (dt.Columns.Contains(finalName))
            {
                finalName = $"{columnName}_{counter}";
                counter++;
            }

            return finalName;
        }

        public static bool IsEmptyTableInfo(TableInfo tableInfo)
        {
            return ReferenceEquals(tableInfo, null) || string.IsNullOrWhiteSpace(tableInfo.Name);
        }

        #endregion

        #region Validation Lists

        public static void ApplyColumnValidations(XLWorkbook workbook, IXLWorksheet ws, DataTable dt, IReadOnlyList<string> allowedDataTypes,
            IReadOnlyDictionary<string, string[]> parameterUnitMap, IXLWorksheet wsValidationLists, ref int validationListRow,
            int headerRow, int firstDataRow, int lastDataRow)
        {
            if (lastDataRow < firstDataRow)
                return;

            Dictionary<string, int> colMap = GetExcelColumnMap(ws, headerRow);

            ApplyDataTypeDropDown(workbook, ws, colMap, allowedDataTypes, wsValidationLists, ref validationListRow, firstDataRow, lastDataRow);

            foreach (var rule in ExcelColumnFields.WholeNumberColumns)
            {
                ApplyWholeNumberValidation(ws, colMap, rule.ColumnName, rule.Min, rule.Max, firstDataRow, lastDataRow);
            }

            foreach (string columnName in _options.GetAllBooleanColumns())
            {
                ApplyBooleanDropDown(ws, colMap, columnName, firstDataRow, lastDataRow);
            }

            foreach (string columnName in ExcelColumnFields.TextColumns)
            {
                ApplyTextOnlyValidation(ws, colMap, columnName, firstDataRow, lastDataRow);
            }

            ApplyParameterAndUnitDropDowns(workbook, ws, colMap, parameterUnitMap, wsValidationLists, ref validationListRow, firstDataRow, lastDataRow);

            ApplyDefaultValueValidationByDataType(workbook, ws, dt, colMap, wsValidationLists, ref validationListRow, firstDataRow, lastDataRow);
        }

        public static void ApplyDataTypeDropDown(XLWorkbook workbook, IXLWorksheet ws, Dictionary<string, int> colMap, IReadOnlyList<string> allowedDataTypes,
            IXLWorksheet wsValidationLists, ref int validationListRow, int firstDataRow, int lastDataRow)
        {
            if (!colMap.TryGetValue(ExcelColumnFields.DataType, out int dataTypeCol))
                return;

            if (allowedDataTypes == null || allowedDataTypes.Count == 0)
                return;

            EnsureSimpleNamedList(workbook, wsValidationLists, "Allowed_Data_Types", allowedDataTypes, ref validationListRow);

            var range = ws.Range(firstDataRow, dataTypeCol, lastDataRow, dataTypeCol);

            var validation = range.CreateDataValidation();
            validation.IgnoreBlanks = true;
            validation.InCellDropdown = true;
            validation.ShowErrorMessage = true;
            validation.ErrorStyle = XLErrorStyle.Stop;
            validation.ErrorTitle = "Invalid Data Type";
            validation.ErrorMessage = "Please select data type from dropdown only.";
            validation.List("=Allowed_Data_Types");
        }

        public static void EnsureSimpleNamedList(XLWorkbook workbook, IXLWorksheet wsValidationLists, string rangeName, IReadOnlyList<string> values,
            ref int validationListRow)
        {
            bool alreadyCreated = workbook.NamedRanges.Any(x => x.Name.Equals(rangeName, StringComparison.OrdinalIgnoreCase));

            if (alreadyCreated)
                return;

            int startRow = validationListRow;
            int col = 1;

            foreach (string value in values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            {
                wsValidationLists.Cell(validationListRow, col).Value = value;
                validationListRow++;
            }

            int endRow = validationListRow - 1;

            if (endRow >= startRow)
            {
                workbook.NamedRanges.Add(rangeName, wsValidationLists.Range(startRow, col, endRow, col));
            }
        }

        public static void ApplyWholeNumberValidation(IXLWorksheet ws, Dictionary<string, int> colMap, string columnName, int minValue, int maxValue,
            int firstDataRow, int lastDataRow)
        {
            if (!colMap.TryGetValue(columnName, out int col))
                return;

            if (lastDataRow < firstDataRow)
                return;

            var range = ws.Range(firstDataRow, col, lastDataRow, col);

            ws.Column(col).Style.NumberFormat.Format = "0";

            var validation = range.CreateDataValidation();
            validation.IgnoreBlanks = true;
            validation.ShowErrorMessage = true;
            validation.ErrorStyle = XLErrorStyle.Stop;
            validation.ErrorTitle = "Invalid Number";
            validation.ErrorMessage = $"{columnName} accepts only whole numbers between {minValue} and {maxValue}.";

            validation.WholeNumber.Between(minValue, maxValue);
        }

        public static void ApplyBooleanDropDown(IXLWorksheet ws, Dictionary<string, int> colMap, string columnName, int firstDataRow, int lastDataRow)
        {
            if (!colMap.TryGetValue(columnName, out int col))
                return;

            if (lastDataRow < firstDataRow)
                return;

            var range = ws.Range(firstDataRow, col, lastDataRow, col);

            var validation = range.CreateDataValidation();
            validation.IgnoreBlanks = true;
            validation.InCellDropdown = true;
            validation.ShowErrorMessage = true;
            validation.ErrorStyle = XLErrorStyle.Stop;
            validation.ErrorTitle = "Invalid Value";
            validation.ErrorMessage = $"{columnName} accepts only TRUE or FALSE.";
            validation.List("\"TRUE,FALSE\"");
        }

        public static void ApplyTextOnlyValidation(IXLWorksheet ws, Dictionary<string, int> colMap, string columnName, int firstDataRow, int lastDataRow)
        {
            if (!colMap.TryGetValue(columnName, out int col))
                return;

            if (lastDataRow < firstDataRow)
                return;

            var range = ws.Range(firstDataRow, col, lastDataRow, col);

            string firstCellAddress =
                ws.Cell(firstDataRow, col).Address.ToString().Replace("$", "");

            var validation = range.CreateDataValidation();
            validation.IgnoreBlanks = true;
            validation.ShowErrorMessage = true;
            validation.ErrorStyle = XLErrorStyle.Stop;
            validation.ErrorTitle = "Invalid Text";
            validation.ErrorMessage = $"{columnName} accepts text only. Digits are not allowed.";

            string formula =
                $"=OR({firstCellAddress}=\"\",AND(ISTEXT({firstCellAddress})," +
                $"SUMPRODUCT(--ISNUMBER(FIND(MID({firstCellAddress},ROW(INDIRECT(\"1:\"&LEN({firstCellAddress}))),1),\"0123456789\")))=0))";

            validation.Custom(formula);
        }

        public static void ApplyParameterAndUnitDropDowns(XLWorkbook workbook, IXLWorksheet ws, Dictionary<string, int> colMap,
            IReadOnlyDictionary<string, string[]> parameterUnitMap,
            IXLWorksheet wsValidationLists, ref int validationListRow, int firstDataRow, int lastDataRow)
        {
            if (parameterUnitMap == null || parameterUnitMap.Count == 0)
                return;

            if (!colMap.TryGetValue(ExcelColumnFields.Parameter, out _))
                return;

            EnsureParameterUnitValidationLists(workbook, wsValidationLists, parameterUnitMap, ref validationListRow);

            ApplyParameterDropDown(ws, colMap, firstDataRow, lastDataRow);
            ApplyUnitTextFormulaByParameter(ws, colMap, firstDataRow, lastDataRow);
            ApplyUnitDropDownByParameter(ws, colMap, firstDataRow, lastDataRow);
        }

        public static void EnsureParameterUnitValidationLists(XLWorkbook workbook, IXLWorksheet wsValidationLists, IReadOnlyDictionary<string,
            string[]> parameterUnitMap, ref int validationListRow)
        {
            const string parameterListName = "Parameter_List";
            const string parameterUnitMapName = "Parameter_Unit_Map";
            const string parameterUnitTextMapName = "Parameter_Unit_Text_Map";

            bool alreadyCreated = workbook.NamedRanges.Any(x =>
                x.Name.Equals(parameterListName, StringComparison.OrdinalIgnoreCase));

            if (alreadyCreated)
                return;

            int parameterMapCol = 20;
            int rangeNameMapCol = 21;

            int textMapParameterCol = 23;
            int textMapUnitsCol = 24;

            int unitListCol = 26;

            int mapStartRow = validationListRow;
            int mapRow = mapStartRow;

            foreach (KeyValuePair<string, string[]> item in parameterUnitMap.OrderBy(x => x.Key))
            {
                string parameterName = item.Key?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(parameterName))
                    continue;

                string[] units = item.Value == null
                    ? Array.Empty<string>()
                    : item.Value
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                if (units.Length == 0)
                    continue;

                string unitRangeName = MakeUniqueNamedRangeName(workbook, MakeSafeNamedRangeName("Units_" + parameterName));

                int unitStartRow = validationListRow;

                foreach (string unit in units)
                {
                    wsValidationLists.Cell(validationListRow, unitListCol).Value = unit;
                    validationListRow++;
                }

                int unitEndRow = validationListRow - 1;

                workbook.NamedRanges.Add(unitRangeName, wsValidationLists.Range(unitStartRow, unitListCol, unitEndRow, unitListCol));

                wsValidationLists.Cell(mapRow, parameterMapCol).Value = parameterName;
                wsValidationLists.Cell(mapRow, rangeNameMapCol).Value = unitRangeName;

                wsValidationLists.Cell(mapRow, textMapParameterCol).Value = parameterName;
                wsValidationLists.Cell(mapRow, textMapUnitsCol).Value = string.Join(",", units);

                mapRow++;
            }

            int mapEndRow = mapRow - 1;

            if (mapEndRow < mapStartRow)
                return;

            workbook.NamedRanges.Add(parameterListName, wsValidationLists.Range(mapStartRow, parameterMapCol, mapEndRow, parameterMapCol));

            workbook.NamedRanges.Add(parameterUnitMapName, wsValidationLists.Range(mapStartRow, parameterMapCol, mapEndRow, rangeNameMapCol));

            workbook.NamedRanges.Add(parameterUnitTextMapName, wsValidationLists.Range(mapStartRow, textMapParameterCol, mapEndRow, textMapUnitsCol));
        }

        public static void ApplyParameterDropDown(IXLWorksheet ws, Dictionary<string, int> colMap, int firstDataRow, int lastDataRow)
        {
            if (!colMap.TryGetValue(ExcelColumnFields.Parameter, out int parameterCol))
                return;

            if (lastDataRow < firstDataRow)
                return;

            var range = ws.Range(firstDataRow, parameterCol, lastDataRow, parameterCol);

            var validation = range.CreateDataValidation();
            validation.IgnoreBlanks = true;
            validation.InCellDropdown = true;
            validation.ShowErrorMessage = true;
            validation.ErrorStyle = XLErrorStyle.Stop;
            validation.ErrorTitle = "Invalid Parameter";
            validation.ErrorMessage = "Please select a valid parameter from dropdown only.";
            validation.List("=Parameter_List");
        }

        public static void ApplyUnitTextFormulaByParameter(IXLWorksheet ws, Dictionary<string, int> colMap, int firstDataRow, int lastDataRow)
        {
            if (!colMap.TryGetValue(ExcelColumnFields.Parameter, out int parameterCol))
                return;

            if (!colMap.TryGetValue(ExcelColumnFields.Unit, out int unitCol))
                return;

            if (lastDataRow < firstDataRow)
                return;

            string parameterColumnLetter = ws.Column(parameterCol).ColumnLetter();

            for (int row = firstDataRow; row <= lastDataRow; row++)
            {
                string parameterCellAddress = $"${parameterColumnLetter}{row}";

                ws.Cell(row, unitCol).FormulaA1 =
                    $"IFERROR(VLOOKUP({parameterCellAddress},Parameter_Unit_Text_Map,2,FALSE),\"\")";
            }
        }

        public static void ApplyUnitDropDownByParameter(IXLWorksheet ws, Dictionary<string, int> colMap, int firstDataRow, int lastDataRow)
        {
            if (!colMap.TryGetValue(ExcelColumnFields.Parameter, out int parameterCol))
                return;

            if (lastDataRow < firstDataRow)
                return;

            string parameterColumnLetter = ws.Column(parameterCol).ColumnLetter();
            string parameterCellAddress = $"${parameterColumnLetter}{firstDataRow}";

            string formula = $"=INDIRECT(VLOOKUP({parameterCellAddress},Parameter_Unit_Map,2,FALSE))";

            foreach (string unitColumnName in ExcelColumnFields.UnitColumns)
            {
                if (!colMap.TryGetValue(unitColumnName, out int unitCol))
                    continue;

                var range = ws.Range(firstDataRow, unitCol, lastDataRow, unitCol);

                var validation = range.CreateDataValidation();
                validation.IgnoreBlanks = true;
                validation.InCellDropdown = true;
                validation.ShowErrorMessage = true;
                validation.ErrorStyle = XLErrorStyle.Stop;
                validation.ErrorTitle = "Invalid Unit";
                validation.ErrorMessage = "Please select unit based on selected parameter only.";
                validation.List(formula);
            }
        }

        public static void ApplyDefaultValueValidationByDataType(XLWorkbook workbook, IXLWorksheet ws, DataTable dt, Dictionary<string, int> colMap,
            IXLWorksheet wsValidationLists, ref int validationListRow, int firstDataRow, int lastDataRow)
        {
            if (!colMap.TryGetValue(ExcelColumnFields.DefaultValue, out int defaultValueCol))
                return;

            if (!colMap.TryGetValue(ExcelColumnFields.DataType, out int dataTypeCol))
                return;

            if (dt == null || dt.Rows.Count == 0)
                return;

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                int row = firstDataRow + i;

                if (row > lastDataRow)
                    break;

                string dataType = ws.Cell(row, dataTypeCol).GetString().Trim();
                IXLCell targetCell = ws.Cell(row, defaultValueCol);

                if (IsNumericSqlType(dataType))
                {
                    ApplySingleCellDecimalValidation(targetCell);
                    continue;
                }

                if (IsStringSqlType(dataType))
                {
                    ApplySingleCellTextValidation(targetCell);
                    continue;
                }

                if (IsBooleanSqlType(dataType))
                {
                    ApplySingleCellBooleanValidation(targetCell);
                    continue;
                }
            }
        }

        public static void ApplySingleCellDecimalValidation(IXLCell cell)
        {
            var validation = cell.CreateDataValidation();
            validation.IgnoreBlanks = true;
            validation.ShowErrorMessage = true;
            validation.ErrorStyle = XLErrorStyle.Stop;
            validation.ErrorTitle = "Invalid Number";
            validation.ErrorMessage = "Only numeric value is allowed.";
            validation.Decimal.Between(-999999999999, 999999999999);
        }

        public static void ApplySingleCellBooleanValidation(IXLCell cell)
        {
            var validation = cell.CreateDataValidation();
            validation.IgnoreBlanks = true;
            validation.InCellDropdown = true;
            validation.ShowErrorMessage = true;
            validation.ErrorStyle = XLErrorStyle.Stop;
            validation.ErrorTitle = "Invalid Boolean";
            validation.ErrorMessage = "Only TRUE or FALSE is allowed.";
            validation.List("\"TRUE,FALSE\"");
        }

        public static void ApplySingleCellTextValidation(IXLCell cell)
        {
            string address = cell.Address.ToString().Replace("$", "");

            var validation = cell.CreateDataValidation();
            validation.IgnoreBlanks = true;
            validation.ShowErrorMessage = true;
            validation.ErrorStyle = XLErrorStyle.Stop;
            validation.ErrorTitle = "Invalid Text";
            validation.ErrorMessage = "Only text is allowed. Digits are not allowed.";

            string formula =
                $"=OR({address}=\"\",AND(ISTEXT({address})," +
                $"SUMPRODUCT(--ISNUMBER(FIND(MID({address},ROW(INDIRECT(\"1:\"&LEN({address}))),1),\"0123456789\")))=0))";

            validation.Custom(formula);
        }

        #endregion

        #region Protection
        public static void ApplyFinalWorkbookProtection(XLWorkbook workbook)
        {
            if (workbook == null)
                return;

            string password = _options.ProtectionPassword;

            if (string.IsNullOrWhiteSpace(password))
                password = "AarohiUnitLock";

            if (_options.ProtectWorksheet)
                ProtectAllWorksheets(workbook, password);

            if (_options.ProtectWorkbookStructure)
                ProtectWorkbookStructure(workbook, password);
        }
        public static void ProtectAllWorksheets(XLWorkbook workbook, string password)
        {
            foreach (IXLWorksheet ws in workbook.Worksheets)
            {
                ws.Style.Protection.Locked = true;

                var usedRange = ws.RangeUsed();

                if (usedRange != null)
                    usedRange.Style.Protection.Locked = true;

                ws.Protect(password).AllowElement(XLSheetProtectionElements.SelectLockedCells)
                    .AllowElement(XLSheetProtectionElements.SelectUnlockedCells);
            }
        }

        public static void ProtectWorkbookStructure(XLWorkbook workbook, string password)
        {
            workbook.Protect(password);
        }

        #endregion

        #region Common Helpers

        public static Dictionary<string, int> GetExcelColumnMap(IXLWorksheet ws, int headerRowNumber = 1)
        {
            Dictionary<string, int> map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var usedRange = ws.RangeUsed();

            if (usedRange == null)
                return map;

            int lastColumn = usedRange.LastColumn().ColumnNumber();

            for (int col = 1; col <= lastColumn; col++)
            {
                string header = ws.Cell(headerRowNumber, col).GetString().Trim();

                if (!string.IsNullOrWhiteSpace(header) && !map.ContainsKey(header))
                    map.Add(header, col);
            }

            return map;
        }

        public static void FormatExcelSheet(IXLWorksheet ws, int headerRowNumber = 1)
        {
            var usedRange = ws.RangeUsed();

            if (usedRange == null)
                return;

            usedRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            var headerCells = ws.Row(headerRowNumber).CellsUsed();

            foreach (IXLCell cell in headerCells)
            {
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#000000");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }

            usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            ws.SheetView.FreezeRows(headerRowNumber);
            ws.SheetView.FreezeColumns(1);

            ws.ColumnsUsed().AdjustToContents(8, 45);
        }
        public static string MakeSafeWorksheetName(string name, HashSet<string> usedNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "Sheet";

            char[] invalidChars = { ':', '\\', '/', '?', '*', '[', ']' };

            foreach (char c in invalidChars)
                name = name.Replace(c, '_');

            name = name.Trim();

            if (name.Length > 31)
                name = name.Substring(0, 31);

            string baseName = name;
            string finalName = baseName;
            int counter = 1;

            while (usedNames.Contains(finalName))
            {
                string suffix = "_" + counter;
                int allowedLength = 31 - suffix.Length;

                string shortBase = baseName.Length > allowedLength? baseName.Substring(0, allowedLength): baseName;

                finalName = shortBase + suffix;
                counter++;
            }

            usedNames.Add(finalName);

            return finalName;
        }
        public static string MakeSafeWorksheetBaseName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Sheet";

            char[] invalidChars = { ':', '\\', '/', '?', '*', '[', ']' };

            foreach (char ch in invalidChars)
                name = name.Replace(ch, '_');

            name = name.Trim();

            if (name.Length > 31)
                name = name.Substring(0, 31);

            return name;
        }

        public static string MakeSafeExcelTableName(string name, HashSet<string> usedNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "ExportTable";

            string safe = new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

            safe = safe.Trim('_');

            if (string.IsNullOrWhiteSpace(safe))
                safe = "ExportTable";

            if (char.IsDigit(safe[0]))
                safe = "T_" + safe;

            if (safe.Length > 200)
                safe = safe.Substring(0, 200);

            string baseName = safe;
            string finalName = baseName;
            int counter = 1;

            while (usedNames.Contains(finalName))
            {
                finalName = $"{baseName}_{counter}";
                counter++;
            }

            usedNames.Add(finalName);

            return finalName;
        }

        public static string MakeSafeNamedRangeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "ValidationList";

            string safe = new string(
                name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());

            if (char.IsDigit(safe[0]))
                safe = "N_" + safe;

            if (safe.Length > 200)
                safe = safe.Substring(0, 200);

            return safe;
        }

        public static string MakeUniqueNamedRangeName(XLWorkbook workbook,string baseName)
        {
            string finalName = baseName;
            int counter = 1;

            while (workbook.NamedRanges.Any(x =>x.Name.Equals(finalName, StringComparison.OrdinalIgnoreCase)))
            {
                finalName = $"{baseName}_{counter}";
                counter++;
            }

            return finalName;
        }

        public static void SetWorksheetGuidIdentity(IXLWorksheet ws,string uniqueId)
        {
            if (ws == null)
                return;

            if (string.IsNullOrWhiteSpace(uniqueId))
                return;

            ws.Cell(_options.SheetGuidCellAddress).Value = uniqueId;
            ws.Cell(_options.SheetGuidCellAddress).Style.Protection.Locked = true;

            ws.Column("XFD").Hide();

            ws.PageSetup.Footer.Left.Clear(XLHFOccurrence.AllPages);
            ws.PageSetup.Footer.Center.Clear(XLHFOccurrence.AllPages);
            ws.PageSetup.Footer.Right.Clear(XLHFOccurrence.AllPages);

            ws.PageSetup.Footer.Center.AddText(_options.SheetGuidFooterPrefix + uniqueId,XLHFOccurrence.AllPages);
        }
        public static int FindHeaderRow(IXLWorksheet ws)
        {
            foreach (IXLRow row in ws.RowsUsed())
            {
                bool hasName = false;
                bool hasDataType = false;

                foreach (IXLCell cell in row.CellsUsed())
                {
                    string value = cell.GetString().Trim();

                    if (string.Equals(value, ExcelColumnFields.Name, StringComparison.OrdinalIgnoreCase))
                        hasName = true;

                    if (string.Equals(value, ExcelColumnFields.DataType, StringComparison.OrdinalIgnoreCase))
                        hasDataType = true;
                }

                if (hasName && hasDataType)
                    return row.RowNumber();
            }

            return 0;
        }

        public static int FindTablesHeaderRow(IXLWorksheet ws)
        {
            foreach (IXLRow row in ws.RowsUsed())
            {
              bool hasUniqueId = row.CellsUsed().Any(c =>string.Equals(c.GetString().Trim(), ExcelTableFields.UniqueId,StringComparison.OrdinalIgnoreCase));

              bool hasSchema = row.CellsUsed().Any(c =>string.Equals(c.GetString().Trim(),ExcelTableFields.Schema,StringComparison.OrdinalIgnoreCase));

              bool hasName = row.CellsUsed().Any(c =>string.Equals(c.GetString().Trim(),ExcelTableFields.TableName,StringComparison.OrdinalIgnoreCase));

                if (hasUniqueId && hasSchema && hasName)
                    return row.RowNumber();
            }

            return 0;
        }

        public static Dictionary<string, int> GetHeaderMap(IXLWorksheet ws, int headerRow)
        {
            Dictionary<string, int> map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int lastColumn = ws.Row(headerRow).LastCellUsed()?.Address.ColumnNumber ?? 0;

            for (int col = 1; col <= lastColumn; col++)
            {
                string header = ws.Cell(headerRow, col).GetString().Trim();

                if (!string.IsNullOrWhiteSpace(header) && !map.ContainsKey(header))
                    map.Add(header, col);
            }

            return map;
        }

        public static bool IsNumericSqlType(string dataType)
        {
            if (string.IsNullOrWhiteSpace(dataType))
                return false;

            dataType = dataType.Trim().ToLowerInvariant();

            return dataType == "int"|| dataType == "bigint"|| dataType == "smallint"|| dataType == "tinyint"|| dataType == "decimal"
                || dataType == "numeric"|| dataType == "float"|| dataType == "real"|| dataType == "money"|| dataType == "smallmoney";
        }

        public static bool IsStringSqlType(string dataType)
        {
            if (string.IsNullOrWhiteSpace(dataType))
                return false;

            dataType = dataType.Trim().ToLowerInvariant();

            return dataType == "varchar"|| dataType == "nvarchar"|| dataType == "char"|| dataType == "nchar"|| dataType == "text"
                   || dataType == "ntext"|| dataType == "string";
        }

        public static bool IsBooleanSqlType(string dataType)
        {
            if (string.IsNullOrWhiteSpace(dataType))
                return false;

            dataType = dataType.Trim().ToLowerInvariant();

            return dataType == "bit"|| dataType == "bool"|| dataType == "boolean";
        }
        public static TableInfo GetSelectedTableInfo(string selectedTableName,List<TableInfo> allTables)
        {
            if (string.IsNullOrWhiteSpace(selectedTableName))
                throw new ArgumentException("Selected table name cannot be empty.", nameof(selectedTableName));

            if (allTables == null || allTables.Count == 0)
                throw new InvalidOperationException("No database tables found.");

            ParsedTableName parsed = ParseSelectedTableName(selectedTableName);

            List<TableInfo> matches;

            if (!string.IsNullOrWhiteSpace(parsed.Schema))
            {
                matches = allTables.Where(t =>string.Equals(t.Schema, parsed.Schema, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(t.Name, parsed.TableName, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else
            {
                matches = allTables.Where(t =>string.Equals(t.Name, parsed.TableName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (matches.Count == 0)
            {
                throw new InvalidOperationException($"Selected table not found in database: {selectedTableName}");
            }

            if (matches.Count > 1)
            {
                string foundTables = string.Join(", ", matches.Select(x => $"{x.Schema}.{x.Name}"));

                throw new InvalidOperationException($"Multiple tables found with name '{parsed.TableName}'. " +
                    $"Please pass schema also. Found: {foundTables}");
            }

            return matches[0];
        }

        public static ParsedTableName ParseSelectedTableName(string selectedTableName)
        {
            string text = selectedTableName?.Trim() ?? "";

            text = text.Replace("[", "").Replace("]", "").Trim();

            // Supports text like:
            // database | HeadFlow_Input_Master | Value_2
            // In this case table name is second part.
            if (text.Contains("|"))
            {
                string[] parts = text.Split('|').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

                if (parts.Length >= 2)
                    text = parts[1];
                else if (parts.Length == 1)
                    text = parts[0];
            }

            string schema = "";
            string tableName = text;

            string[] nameParts = text.Split('.').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

            if (nameParts.Length >= 2)
            {
                schema = nameParts[nameParts.Length - 2];
                tableName = nameParts[nameParts.Length - 1];
            }

            return new ParsedTableName
            {
                Schema = schema,
                TableName = tableName
            };
        }

        public sealed class ParsedTableName
        {
            public string Schema { get; set; } = "";
            public string TableName { get; set; } = "";
        }

        #endregion

        #region Parameter Mapping
        public static Dictionary<string, string[]> GetParameterUnitMapFromParameterMapping()
        {
            Dictionary<string, string[]> map =new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            DataTable dt = new DataTable();

            try
            {
                using (DynamicClass dc = new DynamicClass("dbo", "PerameterMapping"))
                {
                    dt = GetAllRowsFromDynamicClass(dc);
                }
            }
            catch
            {
                try
                {
                    using (DynamicClass dc = new DynamicClass("dbo", "ParameterMapping"))
                    {
                        dt = GetAllRowsFromDynamicClass(dc);
                    }
                }
                catch
                {
                    return map;
                }
            }

            foreach (DataRow row in dt.Rows)
            {
                string parameter = GetRowString(row, "Perameter");

                if (string.IsNullOrWhiteSpace(parameter))
                    parameter = GetRowString(row, "Parameter");

                string unitsText = GetRowString(row, "Units");

                if (string.IsNullOrWhiteSpace(parameter) ||
                    string.IsNullOrWhiteSpace(unitsText))
                    continue;

                string[] units = unitsText
                    .Split(',')
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (units.Length == 0)
                    continue;

                map[parameter] = units;
            }

            return map;
        }

        public static DataTable GetAllRowsFromDynamicClass(DynamicClass dc)
        {
            if (dc == null)
                return new DataTable();

            return dc.AutoSelectWithJoins(whereSql: null, parameters: null, leftJoin: true, orderBy: null, includeRefKeyColumns: true,
                       defaultRefSchema: null) ?? new DataTable();
        }

        public static string GetRowString(DataRow row, string columnName)
        {
            if (row == null)
                return "";

            if (row.Table == null)
                return "";

            if (string.IsNullOrWhiteSpace(columnName))
                return "";

            if (!row.Table.Columns.Contains(columnName))
                return "";

            object value = row[columnName];

            return value == null || value == DBNull.Value ? "" : Convert.ToString(value)?.Trim() ?? "";
        }

        #endregion

        #region Data Type Helpers

        public static List<string> GetAllowedDataTypesFromDynamicClass(IEnumerable<TableInfo> infos)
        {
            SortedSet<string> dataTypes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (TableInfo info in infos)
            {
                try
                {
                    using (DynamicClass dc = new DynamicClass(info.Schema, info.Name))
                    {
                        List<DynamicClass.ColumnInfo> columns = dc.GetColumns() ?? new List<DynamicClass.ColumnInfo>();

                        foreach (DynamicClass.ColumnInfo column in columns)
                        {
                            if (!string.IsNullOrWhiteSpace(column.DataType))
                                dataTypes.Add(column.DataType.Trim());
                        }
                    }
                }
                catch
                {
                    // Ignore failed table metadata read.
                }
            }

            return dataTypes.ToList();
        }

        #endregion

        #region Hyperlinks

        public static void AddTableSheetHyperlinks(XLWorkbook workbook)
        {
            if (!workbook.Worksheets.Contains(ExcelTableFields.SheetName))
                return;

            IXLWorksheet tableSheet = workbook.Worksheet(ExcelTableFields.SheetName);

            IXLRow headerRow = tableSheet.FirstRowUsed();

            if (headerRow == null)
                return;

            IXLCell fullTableHeaderCell = headerRow.CellsUsed().FirstOrDefault(c => string.Equals(
                        c.GetString().Trim(), ExcelTableFields.FullTableName, StringComparison.OrdinalIgnoreCase));

            if (fullTableHeaderCell == null)
                return;

            int fullTableNameCol = fullTableHeaderCell.Address.ColumnNumber;
            int headerRowNo = headerRow.RowNumber();

            IXLRow lastRow = tableSheet.LastRowUsed();

            if (lastRow == null)
                return;

            for (int row = headerRowNo + 1; row <= lastRow.RowNumber(); row++)
            {
                IXLCell linkCell = tableSheet.Cell(row, fullTableNameCol);
                string fullTableName = linkCell.GetString().Trim();

                if (string.IsNullOrWhiteSpace(fullTableName))
                    continue;

                string safeName = MakeSafeWorksheetBaseName(fullTableName);

                IXLWorksheet targetSheet = workbook.Worksheets.FirstOrDefault(ws =>
                        string.Equals(ws.Name, fullTableName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ws.Name, safeName, StringComparison.OrdinalIgnoreCase) ||
                        safeName.StartsWith(ws.Name, StringComparison.OrdinalIgnoreCase) ||
                        ws.Name.StartsWith(safeName, StringComparison.OrdinalIgnoreCase));

                if (targetSheet == null)
                    continue;

                linkCell.SetHyperlink(new XLHyperlink(targetSheet.Cell("A1")));

                linkCell.Style.Font.FontColor = XLColor.Blue;
                linkCell.Style.Font.Underline = XLFontUnderlineValues.Single;
                linkCell.Style.Font.Bold = true;
            }
        }
        private static void AddBackToTablesButtons(XLWorkbook workbook)
        {
            const string buttonText = "← Back to Tables";

            if (!workbook.Worksheets.Contains(ExcelTableFields.SheetName))
                return;

            IXLWorksheet tablesSheet = workbook.Worksheet(ExcelTableFields.SheetName);

            foreach (IXLWorksheet ws in workbook.Worksheets)
            {
                if (string.Equals(ws.Name, ExcelTableFields.SheetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (ws.Visibility != XLWorksheetVisibility.Visible)
                    continue;

                IXLRange buttonRange = ws.Range("A1:D1");
                buttonRange.Merge();

                IXLCell buttonCell = ws.Cell("A1");
                buttonCell.Value = buttonText;
                buttonCell.SetHyperlink(new XLHyperlink(tablesSheet.Cell("A1")));

                buttonRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
                buttonRange.Style.Font.FontColor = XLColor.White;
                buttonRange.Style.Font.Bold = true;
                buttonRange.Style.Font.FontSize = 12;
                buttonRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                buttonRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                buttonRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                ws.Row(1).Height = 24;
                ws.Row(2).Height = 6;
            }
        }
        #endregion
    }
}
