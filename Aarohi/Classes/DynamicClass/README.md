# DynamicClass Documentation

`DynamicClass` is a SQL Server helper for working with database tables dynamically in the Aarohi project. It wraps common SQL operations — table creation, schema changes, metadata, CRUD, bulk ops, querying, and relation filtering — in a single, fluent partial class without needing hand-written SQL.

## Table of Contents

1. [Declaration & File Structure](#1-declaration--file-structure)
2. [Basic Setup](#2-basic-setup)
3. [Core Properties](#3-core-properties)
4. [Soft Used Names](#4-soft-used-names)
5. [CRUD — Insert, Update, Save, Delete](#5-crud--insert-update-save-delete)
6. [Async CRUD](#6-async-crud)
7. [Select & Querying](#7-select--querying)
8. [Chunk Loading](#8-chunk-loading)
9. [ChunkedTable](#9-chunkedtable)
10. [Bulk Operations](#10-bulk-operations)
11. [DDL — Table Management](#11-ddl--table-management)
12. [DDL — Column Management](#12-ddl--column-management)
13. [Column Options (CHECK Constraints)](#13-column-options-check-constraints)
14. [Metadata & Extended Properties](#14-metadata--extended-properties)
15. [Auto-Join](#15-auto-join)
16. [Relation Filters](#16-relation-filters)
17. [Error Handling](#17-error-handling)
18. [Logging](#18-logging)
19. [Quick API Reference](#19-quick-api-reference)

---

## 1. Declaration & File Structure

```csharp
namespace Aarohi.Classes
{
    public sealed partial class DynamicClass : IDisposable
}
```

Located in `Aarohi/Classes/DynamicClass/`. The class is split into partial files by responsibility:

| File | Purpose |
|---|---|
| `DynamicClass.cs` | Core fields, connection factories, constructors, `Dispose` |
| `DynamicClass.Infrastructure.cs` | Shared helpers, inner model classes, `SafeExecute`, SQL type mapping |
| `DynamicClass.Crud.cs` | Sync insert, update, save, delete, bulk insert, bulk upsert |
| `DynamicClass.CrudAsync.cs` | Async insert, update, save, delete, bulk insert, `SelectAsync` |
| `DynamicClass.Query.cs` | `Select`, chunk helpers, `SelectChunksAsync`, display names, formatting |
| `DynamicClass.ChunkedTable.cs` | `SelectChunkedTable` / `SelectChunkedTableAsync` |
| `DynamicClass.SchemaDefinition.cs` | Create/drop/ensure table, add/alter/rename/drop columns, column options |
| `DynamicClass.Metadata.cs` | Extended properties, display names, units, format, order, visibility, `GetColumns` |
| `DynamicClass.NameResolution.cs` | Physical-name validation and SQL placeholder replacement |
| `DynamicClass.AutoJoin.cs` | `AutoSelectWithJoins` — zero-config FK joins |
| `DynamicClass.Relations.cs` | `SelectWithRelationFilters` — join-based filtering |
| `DynamicClass.Logging.cs` | Logger sink injection |

---

## 2. Basic Setup

Set the connection factory **once at application startup**, before using any `DynamicClass` instance:

```csharp
using Aarohi.Classes;
using Microsoft.Data.SqlClient;

DynamicClass.ConnectionFactory = () =>
    new SqlConnection("Server=.;Database=AarohiDb;Trusted_Connection=True;TrustServerCertificate=True;");
```

Then create an instance for a specific table:

```csharp
var machine = new DynamicClass("dbo", "Machine", "MachineId");
```

If `keyColumn` is omitted, `DynamicClass` auto-detects the primary key. If no PK is found, it falls back to a single identity column.

**Per-instance connection** (when one table needs a different database):

```csharp
var machine = new DynamicClass("dbo", "Machine");
machine.InstanceConnectionFactory = () =>
    new SqlConnection("Server=remote;Database=OtherDb;...");
```

---

## 3. Core Properties

| Property | Type | Description |
|---|---|---|
| `Schema` | `string` | SQL schema name, usually `"dbo"` |
| `Table` | `string` | Physical table name |
| `KeyColumn` | `string` | Physical key column name |
| `Values` | `Dictionary<string, object?>` | Column-value pairs for insert/update/save (case-insensitive) |
| `SchemaSpec` | `Dictionary<string, ColumnDef>` | Column definitions for DDL operations |
| `LastErrorMessage` | `string?` | Last captured error message |
| `LastException` | `Exception?` | Last captured exception |
| `ConnectionFactory` | `static Func<SqlConnection>` | Default SQL connection factory (set once at startup) |
| `InstanceConnectionFactory` | `Func<SqlConnection>?` | Per-instance connection factory override |
| `ThrowOnError` | `static bool` | When `true` (default), exceptions are rethrown. When `false`, methods return defaults on failure. |
| `LogInfo` | `static bool` | Enable info-level logging (default `true`) |
| `LogTrace` | `static bool` | Enable trace-level logging (default `false`) |
| `BulkCopyTimeoutSeconds` | `static int` | Default timeout for `SqlBulkCopy` operations in seconds (`600` by default, `0` = infinite) |
| `LogSource` | `string` | Source tag used in log entries (default `"DynamicClass"`) |
| `Soft_Name` | `static string` | Software name tag stamped on columns added via `AddColumn`. Useful for tracking which columns your app created. |

---

## 4. Naming Rules

`DynamicClass` now works only with physical SQL table and column names. SQL placeholder syntax such as `{MachineName}` is still supported, but placeholders must reference real column names.

**Use soft names in SQL fragments via `{PlaceholderName}` syntax:**

```csharp
var rows = dc.Select(
    whereSql: "{Name} = @name",
    parameters: new Dictionary<string, object?> { ["name"] = "Line 1" },
    orderBy: "{Id} DESC");
```

Curly-brace placeholders in `whereSql` and `orderBy` are automatically resolved to their physical column names and safely quoted.

---

## 5. CRUD — Insert, Update, Save, Delete

### Insert

Sets values in `Values` and calls `Insert()`. Returns the new identity value (or provided key, or `null`):

```csharp
var dc = new DynamicClass("dbo", "Machine", "MachineId");

dc.Values["MachineName"] = "Line 1";
dc.Values["Status"] = "Active";

object? newId = dc.Insert();
```

- Empty string values are treated as `NULL`.
- Decimal columns are validated to fit the column's `precision` and `scale`.
- Values is not cleared automatically — call `dc.Values.Clear()` before reusing.

### Update By Key

Only **changed columns** are written (diff against the current DB row is performed first):

```csharp
dc.Values["MachineId"] = 10;
dc.Values["Status"] = "Stopped";

int affectedRows = dc.UpdateByKey();
```

Returns `0` if the row does not exist or values are identical to the current row.

### Save (Upsert)

Automatically decides insert vs update:
- No key in `Values` → **insert**
- Key present but row does not exist → **insert**
- Key present and row exists → **update** (only changed columns)

```csharp
dc.Values["MachineId"] = 10;
dc.Values["MachineName"] = "Line 1";
dc.Values["Status"] = "Active";

object? key = dc.Save(askBeforeOverwrite: false);
```

`askBeforeOverwrite: true` (default) shows a `MessageBox` diff before updating in sync mode. Use `SaveAsync` with a callback for non-UI or async code.

### Delete By Key

```csharp
int deleted = dc.DeleteByKey(10);
```

Throws `ForeignKeyDeleteBlockedException` (caught from SQL error 547) when a child row prevents deletion.

---

## 6. Async CRUD

All sync CRUD methods have async equivalents. Use these for background operations or when you don't want to block the UI thread.

```csharp
// Insert
object? newId = await dc.InsertAsync(ct);

// Update
int affected = await dc.UpdateByKeyAsync(ct);

// Save / Upsert
object? key = await dc.SaveAsync(
    askBeforeOverwrite: true,
    confirmAsync: async (changeSummary, ct) =>
    {
        // changeSummary lists old → new values
        // return true to proceed, false to cancel
        return await ConfirmDialogAsync(changeSummary);
    },
    ct: ct);

// Delete
int deleted = await dc.DeleteByKeyAsync(10, ct);
```

`SaveAsync` accepts an async delegate instead of `MessageBox`, making it UI-framework agnostic.

---

## 7. Select & Querying

### Basic Select

```csharp
DataTable? rows = dc.Select();
```

With optional filtering:

```csharp
DataTable? rows = dc.Select(
    whereSql: "{Status} = @status AND {CreatedOn} >= @from",
    parameters: new Dictionary<string, object?>
    {
        ["status"] = "Active",
        ["from"]   = new DateTime(2024, 1, 1)
    },
    orderBy: "{CreatedOn} DESC",
    top: 100);
```

With display names and formatting applied:

```csharp
DataTable? rows = dc.Select(
    orderBy: "{CreatedOn} DESC",
    DisplayName: true,           // rename columns to their DisplayName extended property
    WantFormatingInDefault: true); // apply Format extended property to column values
```

### Get a Single Row as Dictionary

```csharp
Dictionary<string, object?>? row = dc.GetRowAsDictionary("MachineId", 10);
// Returns null if not found
```

### Get Distinct Column Values

```csharp
string[] statuses = dc.GetColumnValues("Status");
string[] filtered = dc.GetColumnValues("Status", whereSql: "{Active} = @v",
    parameters: new Dictionary<string, object?> { ["v"] = 1 });
```

### Extract Column from Existing DataTable (static)

```csharp
string[] names = DynamicClass.GetColumnValuesFromDataTable(dt, "MachineName");
```

### Async Select

```csharp
DataTable? rows = await dc.SelectAsync(
    whereSql: "{Status} = @s",
    parameters: new Dictionary<string, object?> { ["s"] = "Active" },
    orderBy: "{CreatedOn} DESC",
    ct: cancellationToken);
```

---

## 8. Chunk Loading

For large tables, load data in pages instead of all at once.

### Load a Specific Chunk

```csharp
// First 50 rows
DataTable? chunk1 = dc.Select(chunkSize: 50);

// Second 50 rows
DataTable? chunk2 = dc.Select(chunkSize: 50, chunkNumber: 2);

// Or by offset
DataTable? chunk = dc.Select(chunkSize: 50, chunkOffset: 100);
```

Rules:
- `chunkSize` alone returns chunk 1.
- `top` and `chunkSize` cannot be used together.
- SQL Server paging requires ordering. If none is given, `DynamicClass` defaults to key column, then first column.

### Stream All Chunks (Async Enumerable)

```csharp
await foreach (DataTable chunk in dc.SelectChunksAsync(
    orderBy: "{CreatedOn} DESC",
    chunkSize: 50,
    ct: cancellationToken))
{
    // Handle each chunk — bind first, then append subsequent chunks to the grid
}
```

### With the WPF `ChunkedDataGrid` Control

```csharp
var first = dc.Select(orderBy: "{CreatedOn} DESC", chunkSize: grid.ChunkSize);
grid.BindTable(first ?? new DataTable());

await grid.AppendChunksAsync(dc.SelectChunksAsync(
    orderBy: "{CreatedOn} DESC",
    chunkSize: grid.ChunkSize,
    skipFirstChunk: true,
    ct: cancellationToken), cancellationToken);
```

---

## 9. ChunkedTable

`ChunkedTable` is a `DataTable` subclass that holds data in three views simultaneously and raises events as background chunks arrive. It is the preferred way to bind large tables to a WinForms `DataGridView` with incremental loading.

### Loading

```csharp
CancellationTokenSource cts = new();

ChunkedTable table = await dc.SelectChunkedTableAsync(
    orderBy: "{Id} ASC",
    chunkSize: 150,
    backgroundChunkSize: 1000,   // bigger chunks after the first
    loadRemainingInBackground: true,
    ct: cts.Token);
```

Or synchronously (blocks until the first chunk is loaded, then background-loads the rest):

```csharp
ChunkedTable table = dc.SelectChunkedTable(
    orderBy: "{Id} ASC",
    chunkSize: 150);
```

### Binding to a DataGridView

```csharp
dataGridView1.DataSource = table.FirstChunkData;   // bind immediately — shows first chunk

table.Updated += (sender, e) =>
{
    if (!e.Success)
    {
        lblStatus.Text = "Load failed.";
        return;
    }

    lblStatus.Text = $"Loaded {table.AllRowsData.Rows.Count} rows...";

    if (e.IsFinalChunk || table.IsComplete)
    {
        dataGridView1.DataSource = null;
        dataGridView1.DataSource = table.AllRowsData;   // switch to full data
        lblStatus.Text = $"Complete. {table.AllRowsData.Rows.Count} total rows.";
    }
};
```

### ChunkedTable Data Views

| Property | Contains |
|---|---|
| `FirstChunkData` | Only rows from the initial chunk |
| `AfterFirstChunkData` | Only rows loaded in the background (chunks 2, 3, …) |
| `AllRowsData` | All rows (equivalent to `this`) |

### ChunkedTable Events

| Event | When it fires |
|---|---|
| `OnUpdate` | Just before any update (first chance to cancel or inspect) |
| `Updating` | Immediately before rows are written |
| `Updated` | After rows are written — `e.Success`, `e.RowsAfter`, `e.IsFinalChunk`, `e.IsBackgroundUpdate` |

### ChunkedTable State

| Property | Meaning |
|---|---|
| `IsBackgroundLoading` | `true` while background load is running |
| `IsComplete` | `true` when all rows have been loaded |
| `IsUpdating` | `true` during an active `Update` call |
| `UpdateCount` | Number of times the table has been updated |
| `BackgroundLoadException` | Exception from the background task, if any |

### Cancel Background Load

```csharp
table.CancelBackgroundLoad();
```

Always cancel on form close to avoid orphaned threads:

```csharp
protected override void OnFormClosing(FormClosingEventArgs e)
{
    cts.Cancel();
    table?.CancelBackgroundLoad();
    cts.Dispose();
    base.OnFormClosing(e);
}
```

### Column Metadata in ChunkedTable

When loaded through `SelectChunkedTableAsync`, column display names, units, and parameters from SQL Server extended properties are automatically applied to `DataColumn.Caption` and `DataColumn.ExtendedProperties`.

---

## 10. Bulk Operations

### BulkInsert (DataTable)

Inserts all rows from a `DataTable` using `SqlBulkCopy`:

```csharp
int inserted = dc.BulkInsert(
    dt,
    batchSize: 1000,
    keepIdentity: false,    // if true, preserves identity values from the DataTable
    useTransaction: true,
    autoTrimColumns: true,
    bulkCopyTimeoutSeconds: 300); // 0 = infinite wait
```

`autoTrimColumns: false` throws if the `DataTable` contains columns not in the destination.

### BulkInsert (Dictionary list, Async)

```csharp
int inserted = await dc.BulkInsertAsync(
    rows: listOfDictionaries,
    batchSize: 1000,
    bulkCopyTimeoutSeconds: 300,
    ct: cancellationToken);
```

### BulkInsert (DataTable, Async)

```csharp
int inserted = await dc.BulkInsertAsync(
    dt,
    batchSize: 1000,
    autoTrimColumns: true,
    bulkCopyTimeoutSeconds: 300,
    ct: cancellationToken);
```

### BulkUpsert (DataTable)

Uses a temp table + SQL `MERGE` to insert new rows and update existing ones in one shot:

```csharp
(int Inserted, int Updated) result = dc.BulkUpsert(
    dt,
    keyColumn: "MachineId",     // defaults to dc.KeyColumn if omitted
    batchSize: 2000,
    useTransaction: true,
    autoTrimColumns: true,
    ignoreNullUpdates: false);  // if true, NULL values in the source will NOT overwrite target values

Console.WriteLine($"Inserted: {result.Inserted}, Updated: {result.Updated}");
```

BulkUpsert steps (internally):
1. Creates a `#tmp_upsert` temp table matching the destination column types.
2. Bulk-copies the `DataTable` into the temp table.
3. Executes `MERGE ... WHEN MATCHED THEN UPDATE ... WHEN NOT MATCHED THEN INSERT ...`.
4. Returns counts from `OUTPUT $action`.

---

## 11. DDL — Table Management

Define columns in `SchemaSpec` before calling DDL methods.

### EnsureTable (recommended)

Creates the table if it does not exist. If it already exists and `SchemaSpec` has entries, adds any missing columns:

```csharp
var dc = new DynamicClass("dbo", "Machine", "MachineId");

dc.SchemaSpec["MachineId"] = new DynamicClass.ColumnDef
{
    Name       = "MachineId",
    SqlType    = "int",
    Nullable   = false,
    Identity   = true,
    PrimaryKey = true
};

dc.SchemaSpec["MachineName"] = new DynamicClass.ColumnDef
{
    Name    = "MachineName",
    SqlType = "nvarchar(200)",
    Nullable = false
};

dc.SchemaSpec["Status"] = new DynamicClass.ColumnDef
{
    Name    = "Status",
    SqlType = "nvarchar(50)",
    Nullable = true
};

dc.EnsureTable();  // idempotent — safe to call every startup
```

### CreateTable

Creates the table unconditionally (fails if it already exists):

```csharp
dc.CreateTable(pkName: "MachineId");
```

### DropTable

Drops the table if it exists (idempotent):

```csharp
dc.DropTable();
```

---

## 12. DDL — Column Management

### AddColumn (Simple)

```csharp
dc.AddColumn("Notes", "nvarchar(500)", nullable: true);
dc.AddColumn("SortOrder", "int", nullable: false);
```

If the column already exists, the call is a no-op.

### AddColumn (Rich Options)

Use `ColumnAddOptions` for full control:

```csharp
// varchar with explicit length
dc.AddColumn(new DynamicClass.ColumnAddOptions
{
    Name     = "Code",
    Type     = "varchar",
    Length   = 50,
    Nullable = false,
    DefaultSql = "'UNKNOWN'"
});

// decimal with precision and scale
dc.AddColumn(new DynamicClass.ColumnAddOptions
{
    Name      = "Price",
    Type      = "decimal",
    Precision = 18,
    Scale     = 4,
    Nullable  = true
});

// computed column
dc.AddColumn(new DynamicClass.ColumnAddOptions
{
    Name                  = "FullLabel",
    Type                  = "",            // ignored for computed
    ComputedExpressionSql = "[Code] + ' - ' + [MachineName]",
    Persisted             = true
});

// identity
dc.AddColumn(new DynamicClass.ColumnAddOptions
{
    Name             = "RowNum",
    Type             = "int",
    Nullable         = false,
    Identity         = true,
    IdentitySeed      = 1,
    IdentityIncrement = 1
});
```

`ColumnAddOptions` fields:

| Field | Description |
|---|---|
| `Name` | Column name (required) |
| `Type` | Base SQL type, e.g. `"varchar"`, `"decimal"`, `"int"` (required) |
| `Length` | For `varchar`, `nvarchar`, `char`, `nchar`, `varbinary` — use `-1` for MAX |
| `Precision` | For `decimal`/`numeric` |
| `Scale` | For `decimal`/`numeric`; also used as fractional-seconds scale for `datetime2`/`time` |
| `Nullable` | `true` by default |
| `Identity` | `false` by default |
| `IdentitySeed` | Start value for identity (default `1`) |
| `IdentityIncrement` | Increment for identity (default `1`) |
| `DefaultSql` | SQL expression for `DEFAULT` constraint, e.g. `"GETUTCDATE()"`, `"'Active'"` |
| `DefaultConstraintName` | Custom name for the `DEFAULT` constraint |
| `ComputedExpressionSql` | SQL expression for a computed column |
| `Persisted` | `true` to persist a computed column |

> **Note:** Adding a `NOT NULL` column without a `DefaultSql` to a table that already has rows will throw an error. Either add as `NULL` first and backfill, or provide a `DefaultSql`.

### AlterColumn

Change a column's type or nullability:

```csharp
dc.AlterColumn(new DynamicClass.ColumnAlterOptions
{
    Name      = "MachineName",
    Type      = "nvarchar",
    Length    = 400,
    Nullable  = false
});
```

### RenameColumn

```csharp
dc.RenameColumn("OldColumnName", "NewColumnName");
```

Uses `sp_rename` internally.

### DropColumn

```csharp
dc.DropColumn("ObsoleteColumn");   // no-op if column doesn't exist
```

---

## 13. Column Options (CHECK Constraints)

`DynamicClass` can manage a list of allowed values for a string/numeric column by creating a `CHECK` constraint. This is useful for dropdown options stored and enforced at the DB level.

```csharp
// Set options — replaces any existing CHECK constraint
dc.SetOptions("Status", new[] { "Active", "Stopped", "Maintenance" });

// Add a single option
dc.AddOption("Status", "Calibrating");

// Remove a single option
dc.RemoveOption("Status", "Maintenance");

// Read current options (parsed from the CHECK constraint)
string[] options = dc.GetOptions("Status");
// ["Active", "Stopped", "Calibrating"]

// Drop the constraint entirely
dc.DropOptionsConstraint("Status");
```

Notes:
- Options are stored as a `CK_{Table}_{Column}_OPTIONS` CHECK constraint.
- Numeric column types generate unquoted literals; string types use `N'...'`.
- If the column is nullable, the CHECK allows `NULL` in addition to the listed values.
- `GetColumns()` returns `ColumnInfo.Options` and `ColumnInfo.HasOptions` parsed from these constraints.

---

## 14. Metadata & Extended Properties

All metadata is stored in SQL Server extended properties. This means it survives database backups and is visible in SSMS.

### Display Names

```csharp
dc.SetTableDisplayName("Machine Master");
string tblName = dc.GetTableDisplayName();

dc.SetColumnDisplayName("MachineName", "Machine Name");
string colName = dc.GetColumnDisplayName("MachineName");
dc.RemoveColumnDisplayName("MachineName");
```

### Format (for Select with WantFormatingInDefault)

```csharp
dc.SetColumnFormat("CreatedOn", "dd-MMM-yyyy HH:mm");
dc.SetColumnFormat("Price", "N2");
string? fmt = dc.GetColumnFormat("CreatedOn");
```

### Column Order (displayed left-to-right in Select)

```csharp
dc.SetOrder("MachineId", 0);
dc.SetOrder("MachineName", 1);
dc.SetOrder("Status", 2);
int? order = dc.GetOrder("MachineName");
```

### Show/Hide in DataGrid

```csharp
dc.SetShowInDataGrid("Notes", false);
bool? shown = dc.GetShowInDataGrid("Notes");
```

### Hide in CRUD Form

```csharp
dc.SetHideInCrudForm("MachineId", true);
bool? hidden = dc.GetHideInCrudForm("MachineId");
```

### Units

```csharp
dc.SetColumnDefaultUnit("Voltage", "V");
dc.SetInputUnit("Voltage", "mV");
dc.SetLastUsedUnit("Voltage", "V");

string unit    = dc.GetColumnDefaultUnit("Voltage");
string input   = dc.GetInputUnit("Voltage");
string lastUsed = dc.GetLastUsedUnit("Voltage");
```

### Parameter

```csharp
dc.SetColumnParameter("Voltage", "input_voltage");
string? param = dc.GetColumnParameter("Voltage");
```

### Custom / Arbitrary Properties

Store any name-value pair as an extended property on a column:

```csharp
dc.SetCustomColumnProperty("MachineName", "MyApp_MaxLength", 100);
object? val   = dc.GetCustomColumnProperty("MachineName", "MyApp_MaxLength");
string? str   = dc.GetCustomColumnPropertyString("MachineName", "MyApp_MaxLength");
bool?   flag  = dc.GetCustomColumnPropertyBool("MachineName", "MyApp_IsRequired");
int?    num   = dc.GetCustomColumnPropertyInt("MachineName", "MyApp_MaxLength");
```

### Read All Column Metadata

```csharp
List<DynamicClass.ColumnInfo>? columns = dc.GetColumns();
```

`ColumnInfo` contains:

| Property | Description |
|---|---|
| `Name` | Physical column name |
| `DataType` | SQL type name, e.g. `"nvarchar"` |
| `MaxLength` | Byte length (`-1` for MAX) |
| `Precision` / `Scale` | For decimal/numeric |
| `Nullable` | Whether column allows NULL |
| `Identity` | Whether column is an identity |
| `IsPrimaryKey` / `IsForeignKey` | PK/FK flags |
| `ReferencedTable` / `ReferencedColumn` | FK target |
| `DefaultSql` | Raw DEFAULT definition from DB |
| `DefaultValue` | Parsed .NET default value |
| `CheckDefinition` | Raw CHECK constraint definition |
| `Options` | Parsed string array of allowed values (from CHECK) |
| `HasOptions` | `true` if `Options.Length >= 2` |
| `DisplayName` | Extended property |
| `Format` | Extended property |
| `Order` | Extended property |
| `DatagridShow` | Extended property |
| `HideInCrudForm` | Extended property |
| `Visible` | Extended property |
| `SoftName` | `AddedFromSoftware` extended property |
| `DefaultUnit` / `InputUnit` / `LastUsedUnit` | Unit extended properties |
| `Parameter` | Extended property |

---

## 15. Auto-Join

`AutoSelectWithJoins` automatically detects all foreign keys on the base table and builds the JOIN query for you — no manual SQL needed:

```csharp
DataTable? result = dc.AutoSelectWithJoins(
    whereSql: "{Status} = @status",
    parameters: new Dictionary<string, object?> { ["status"] = "Active" },
    leftJoin: true,             // use LEFT JOIN (default); false = INNER JOIN
    orderBy: "{MachineId} ASC",
    includeRefKeyColumns: false, // exclude the FK reference column from projection
    defaultRefSchema: "dbo");
```

- All base table columns are included (`b.*` style).
- Referenced table columns are included as `[ColumnName]` (without table prefix to keep it clean).
- If a referenced table has a name collision with the base table, the last writer wins — use `SelectWithRelationFilters` for fine-grained control.

---

## 16. Relation Filters

`SelectWithRelationFilters` builds a join-filtered SELECT using explicit relation definitions. Use this when you need to filter the base table by values in related tables.

```csharp
var rows = dc.SelectWithRelationFilters(
    new List<DynamicClass.RelationFilter>
    {
        new()
        {
            Direction    = DynamicClass.ForeignFilterDirection.BaseToReference,
            BaseColumn   = "CustomerId",   // base table column that links to ref table
            RefSchema    = "dbo",
            RefTable     = "Customer",
            RefColumn    = "CustomerId",   // column in ref table that matches BaseColumn
            FilterColumn = "City",         // column in ref table to filter on
            Values       = new List<object?> { "Ahmedabad", "Surat" }
        }
    },
    whereSql: "{Status} = @status",
    parameters: new Dictionary<string, object?> { ["status"] = "Active" },
    orderBy: "{CreatedOn} DESC",
    leftJoin: true,
    displayName: true);
```

`ForeignFilterDirection`:
- `BaseToReference` — base table column = referenced table column (standard FK direction).
- `ReferenceToBase` — referenced table column = base table column (reverse / parent lookup).

Multiple `RelationFilter` entries can be combined. Each adds another `JOIN` and its `IN(...)` clause to the `WHERE`.

---

## 17. Error Handling

### Default Behavior (ThrowOnError = true)

Exceptions propagate normally:

```csharp
try
{
    dc.Values["Status"] = "Active";
    dc.Insert();
}
catch (Exception ex)
{
    // handle
}
```

### Silent Mode (ThrowOnError = false)

Methods return their zero/null default on failure instead of throwing:

```csharp
DynamicClass.ThrowOnError = false;

object? id = dc.Insert();
if (id == null)
{
    // check dc.LastErrorMessage or dc.LastException
    Console.WriteLine(dc.LastErrorMessage);
}
```

### Foreign Key Delete Blocked

```csharp
try
{
    dc.DeleteByKey(10);
}
catch (ForeignKeyDeleteBlockedException ex)
{
    Console.WriteLine($"Blocked by FK: {ex.ConstraintName} on table {ex.ReferencedTable}");
}
```

---

## 18. Logging

`DynamicClass` logs operation details and errors through a static sink. Wire it up once at startup to route logs to your existing logging system:

```csharp
DynamicClass.LogSink = (level, message, source, exception, extras) =>
{
    // level  = LogLevel.Info / .Error / .Debug / .Trace
    // source = dc.LogSource (default "DynamicClass")
    // extras = Dictionary with schema, table, sql, durationMs, error, etc.
    MyLogger.Log(level, $"[{source}] {message}", exception);
};
```

The sink is set-once — calling it a second time throws `InvalidOperationException`. If no sink is set, the class falls back to `Aarohi.Core.Logger._logger`.

Set per-instance log source to distinguish multiple instances in logs:

```csharp
var dc = new DynamicClass("dbo", "Machine", "MachineId");
dc.LogSource = "MachineModule";
```

---

## 19. Quick API Reference

### Connection

| Method / Property | Description |
|---|---|
| `DynamicClass.ConnectionFactory` | Set global default connection factory (once at startup) |
| `dc.InstanceConnectionFactory` | Override factory for a single instance |

### Key Column

| Method | Description |
|---|---|
| `DetectAndSetKeyColumn(bool, bool)` | Auto-detects and assigns PK or identity column |
| `GetPrimaryKeyColumns()` | Returns array of PK column names |

### CRUD

| Method | Description |
|---|---|
| `Insert()` | Insert row from `Values`; returns new key |
| `UpdateByKey()` | Update only changed columns for the key in `Values` |
| `Save(askBeforeOverwrite, Warningneeded)` | Insert-or-update (upsert); shows diff `MessageBox` if asked |
| `DeleteByKey(object)` | Delete row by key; throws `ForeignKeyDeleteBlockedException` on FK conflict |
| `InsertAsync(ct)` | Async insert |
| `UpdateByKeyAsync(ct)` | Async update |
| `SaveAsync(askBeforeOverwrite, confirmAsync, ct)` | Async upsert with async confirm delegate |
| `DeleteByKeyAsync(object, ct)` | Async delete |

### Bulk

| Method | Description |
|---|---|
| `BulkInsert(DataTable, ...)` | Bulk insert via `SqlBulkCopy` |
| `BulkUpsert(DataTable, ...)` | Bulk insert+update via `MERGE` |
| `BulkInsertAsync(IEnumerable<IDictionary>, ...)` | Async bulk insert from dictionary list |
| `BulkInsertAsync(DataTable, ...)` | Async bulk insert from DataTable |

### Query

| Method | Description |
|---|---|
| `Select(whereSql, params, top, orderBy, DisplayName, WantFormating, chunkSize, ...)` | Sync parameterized SELECT |
| `SelectAsync(...)` | Async SELECT |
| `SelectChunksAsync(...)` | Async enumerable — yields one `DataTable` per chunk |
| `SelectChunkedTable(...)` | Sync — loads first chunk, then background-loads the rest into `ChunkedTable` |
| `SelectChunkedTableAsync(...)` | Async version of `SelectChunkedTable` |
| `GetColumnValues(column, whereSql, ...)` | Get distinct string values for one column |
| `GetColumnValuesFromDataTable(dt, column)` | Static — extract a column from existing DataTable |
| `GetRowAsDictionary(column, value)` | Get a single row as `Dictionary<string, object?>` |
| `AutoSelectWithJoins(...)` | SELECT with automatic FK joins |
| `SelectWithRelationFilters(filters, ...)` | SELECT filtered via related tables |

### DDL — Table

| Method | Description |
|---|---|
| `EnsureTable(pkName)` | Create if missing; add new columns if existing (idempotent) |
| `CreateTable(pkName)` | Create new table from `SchemaSpec` |
| `DropTable()` | Drop table if it exists |

### DDL — Column

| Method | Description |
|---|---|
| `AddColumn(string, string, bool, bool)` | Add column by name+type (simple) |
| `AddColumn(ColumnAddOptions)` | Add column with full control (computed, identity, default, precision…) |
| `AlterColumn(ColumnAlterOptions)` | Change column type or nullability |
| `RenameColumn(oldName, newName)` | Rename via `sp_rename` |
| `DropColumn(column)` | Drop column if it exists |

### Column Options

| Method | Description |
|---|---|
| `GetOptions(column)` | Return allowed values parsed from CHECK constraint |
| `SetOptions(column, options)` | Replace CHECK constraint with new allowed values |
| `AddOption(column, option)` | Add one value to the allowed set |
| `RemoveOption(column, option)` | Remove one value from the allowed set |
| `DropOptionsConstraint(column)` | Drop the generated CHECK constraint |

### Metadata

| Method | Description |
|---|---|
| `GetColumns()` | Full column metadata including extended properties |
| `GetColumnNames(tablenameWant)` | Friendly (display) column names |
| `GetColumnNamesOrignal()` | Physical column names |
| `SetTableDisplayName(name)` | Set table-level display name |
| `GetTableDisplayName()` | Get table-level display name |
| `SetColumnDisplayName(col, name)` | Set column display name |
| `GetColumnDisplayName(col)` | Get column display name |
| `SetColumnFormat(col, format)` | Set format string |
| `GetColumnFormat(col)` | Get format string |
| `SetOrder(col, n)` | Set column display order |
| `GetOrder(col)` | Get column display order |
| `SetShowInDataGrid(col, bool)` | Control DataGrid visibility |
| `GetShowInDataGrid(col)` | Read DataGrid visibility |
| `SetHideInCrudForm(col, bool)` | Control CRUD form visibility |
| `GetHideInCrudForm(col)` | Read CRUD form visibility |
| `SetColumnDefaultUnit(col, unit)` | Set default engineering unit |
| `GetColumnDefaultUnit(col)` | Get default engineering unit |
| `SetInputUnit(col, unit)` | Set input unit |
| `SetLastUsedUnit(col, unit)` | Set last used unit |
| `SetColumnParameter(col, param)` | Set parameter tag |
| `GetColumnParameter(col)` | Get parameter tag |
| `SetCustomColumnProperty(col, propName, value)` | Set arbitrary extended property |
| `GetCustomColumnProperty(col, propName)` | Get arbitrary extended property |
| `ApplyDisplayNames(DataTable)` | Rename DataTable columns to their DisplayName |

### Utilities (Static)

| Method | Description |
|---|---|
| `MapSqlTypeToCSharp(sqlType, isNullable)` | Map SQL type name to C# `Type` |
| `MapSqlTypeToCSharpString(sqlType, isNullable)` | Map SQL type name to C# type string |
| `FitsDecimal(value, precision, scale)` | Check whether a decimal value fits a SQL column |

---

## Full Example

```csharp
using Aarohi.Classes;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;
using System.Threading;

// ── Startup ──────────────────────────────────────────────────────────────────
DynamicClass.ConnectionFactory = () =>
    new SqlConnection("Server=.;Database=AarohiDb;Trusted_Connection=True;TrustServerCertificate=True;");

// ── Ensure table exists ───────────────────────────────────────────────────────
var dc = new DynamicClass("dbo", "Machine", "MachineId");

dc.SchemaSpec["MachineId"] = new DynamicClass.ColumnDef
    { Name = "MachineId", SqlType = "int", Nullable = false, Identity = true, PrimaryKey = true };
dc.SchemaSpec["MachineName"] = new DynamicClass.ColumnDef
    { Name = "MachineName", SqlType = "nvarchar(200)", Nullable = false };
dc.SchemaSpec["Status"] = new DynamicClass.ColumnDef
    { Name = "Status", SqlType = "nvarchar(50)", Nullable = true };

dc.EnsureTable();

// ── Set metadata (once, or on first run) ──────────────────────────────────────
dc.SetColumnDisplayName("MachineName", "Machine Name");
dc.SetColumnFormat("CreatedOn", "dd-MMM-yyyy");
dc.SetOrder("MachineName", 1);
dc.SetOptions("Status", new[] { "Active", "Stopped", "Maintenance" });

// ── Insert ────────────────────────────────────────────────────────────────────
var insertDc = new DynamicClass("dbo", "Machine", "MachineId");
insertDc.Values["MachineName"] = "Line 1";
insertDc.Values["Status"] = "Active";
object? newId = insertDc.Save(askBeforeOverwrite: false);

// ── Query ─────────────────────────────────────────────────────────────────────
DataTable? rows = dc.Select(
    whereSql: "{Status} = @s",
    parameters: new Dictionary<string, object?> { ["s"] = "Active" },
    orderBy: "{MachineName} ASC",
    chunkSize: 50,
    DisplayName: true);

// ── Chunked load for a DataGridView ───────────────────────────────────────────
CancellationTokenSource cts = new();
ChunkedTable table = await dc.SelectChunkedTableAsync(
    orderBy: "{MachineId} ASC",
    chunkSize: 150,
    backgroundChunkSize: 1000,
    loadRemainingInBackground: true,
    ct: cts.Token);

dataGridView1.DataSource = table.FirstChunkData;
table.Updated += (_, e) =>
{
    if (e.IsFinalChunk) dataGridView1.DataSource = table.AllRowsData;
};

// ── Delete ────────────────────────────────────────────────────────────────────
try
{
    dc.DeleteByKey(newId!);
}
catch (Aarohi.Core.Exceptions.ForeignKeyDeleteBlockedException ex)
{
    MessageBox.Show($"Cannot delete — referenced by {ex.ReferencedTable}");
}
```
