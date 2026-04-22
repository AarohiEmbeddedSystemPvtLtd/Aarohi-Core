# DynamicClass Documentation

`DynamicClass` is a SQL Server helper for dynamic table work in Aarohi. It combines table creation, schema changes, metadata, select queries, insert/update/delete, bulk operations, relation filtering, and soft software names in one partial class.

## Declaration

```csharp
namespace Aarohi.Classes
{
    public sealed partial class DynamicClass : IDisposable
}
```

Main file location:

```text
Aarohi/Classes/DynamicClass/
```

The class is split into partial files by responsibility:

| File | Purpose |
| --- | --- |
| `DynamicClass.cs` | Core properties, connection factory, constructor, dispose |
| `DynamicClass.Query.cs` | Select queries, pagination, display names, formatting |
| `DynamicClass.Crud.cs` | Sync insert, update, save, delete, bulk insert/upsert |
| `DynamicClass.CrudAsync.cs` | Async insert, update, save, delete, bulk insert, select |
| `DynamicClass.SchemaDefinition.cs` | Create/drop/ensure table, add/alter/drop columns, options |
| `DynamicClass.Metadata.cs` | Extended properties, display names, units, format, order, visibility |
| `DynamicClass.NameResolution.cs` | Soft table/column name resolution |
| `DynamicClass.AutoJoin.cs` | Auto join select support |
| `DynamicClass.Relations.cs` | Relation-filtered select support |
| `DynamicClass.Logging.cs` | Logging support |
| `DynamicClass.Infrastructure.cs` | Shared helpers, models, error handling |

## What It Does

`DynamicClass` lets application code work with database tables without writing repetitive SQL for common operations.

It can:

- Create or ensure a table from `SchemaSpec`.
- Add, alter, rename, and drop columns.
- Insert, update by key, save/upsert, delete by key.
- Select rows with optional `WHERE`, `ORDER BY`, `TOP`, and pagination.
- Read table/column metadata from SQL Server.
- Store UI and software metadata in SQL Server extended properties.
- Use software-friendly names through the `Soft_Used_Name` extended property.
- Apply display names and formatting to selected data.
- Run bulk insert and bulk upsert operations.
- Query through relation filters or automatic joins.
- Log failures and keep `LastErrorMessage` / `LastException`.

## Basic Setup

Set the connection factory once during application startup:

```csharp
using Aarohi.Classes;
using Microsoft.Data.SqlClient;

DynamicClass.ConnectionFactory = () =>
    new SqlConnection("Server=.;Database=AarohiDb;Trusted_Connection=True;TrustServerCertificate=True;");
```

Then create an instance for one table:

```csharp
var machine = new DynamicClass("dbo", "Machine", "MachineId");
```

If `keyColumn` is not passed, `DynamicClass` tries to detect the primary key. If no primary key is found, it may use an identity column as fallback.

## Core Properties

| Property | Meaning |
| --- | --- |
| `Schema` | SQL schema, usually `dbo` |
| `Table` | DB table name or soft table name |
| `KeyColumn` | DB key column name or soft key column name |
| `Values` | Case-insensitive dictionary used by insert/update/save |
| `SchemaSpec` | Column definitions used by table creation |
| `LastErrorMessage` | Last captured error message |
| `LastException` | Last captured exception |
| `ConnectionFactory` | Static default SQL connection factory |
| `InstanceConnectionFactory` | Per-instance SQL connection factory |

## Soft Used Names

`DynamicClass` supports a SQL Server extended property named:

```text
Soft_Used_Name
```

This lets code use stable software names even when real database table or column names change.

Behavior:

- If `Soft_Used_Name` exists for a table, `Table` may use that soft table name.
- If `Soft_Used_Name` exists for a column, `Values`, `KeyColumn`, and supported query placeholders may use that soft column name.
- If no soft name exists, the real database name still works.
- Raw SQL is not automatically rewritten unless it uses placeholders like `{ColumnSoftName}`.

Set soft names:

```csharp
var dc = new DynamicClass("dbo", "Machine", "MachineId");

dc.SetTableSoftUsedName("MachineMaster");
dc.SetColumnSoftUsedName("MachineId", "Id");
dc.SetColumnSoftUsedName("MachineName", "Name");
```

Use soft names later:

```csharp
var dc = new DynamicClass("dbo", "MachineMaster", "Id");

dc.Values["Name"] = "Line 1";
dc.Save();
```

Use placeholders in query fragments:

```csharp
var rows = dc.Select(
    whereSql: "{Name} = @name",
    parameters: new Dictionary<string, object?> { ["name"] = "Line 1" },
    orderBy: "{Id} DESC");
```

## Select And Pagination

`Select` supports optional filtering, ordering, display names, formatting, and pagination.

```csharp
DataTable? rows = dc.Select(
    whereSql: "{Status} = @status",
    parameters: new Dictionary<string, object?> { ["status"] = "Active" },
    orderBy: "{CreatedOn} DESC",
    pageNumber: 1,
    pageSize: 50);
```

Rules:

- `pageNumber` starts at `1`.
- `pageNumber` and `pageSize` must be used together.
- `top` and pagination cannot be used together.
- SQL Server pagination requires ordering. If no `orderBy` is provided, `DynamicClass` orders by key column, then first column.

Async version:

```csharp
DataTable? rows = await dc.SelectAsync(
    orderBy: "{CreatedOn} DESC",
    pageNumber: 2,
    pageSize: 50);
```

## Insert, Update, Save, Delete

Insert:

```csharp
var dc = new DynamicClass("dbo", "Machine", "MachineId");

dc.Values["MachineName"] = "Line 1";
dc.Values["Status"] = "Active";

object? newId = dc.Insert();
```

Update by key:

```csharp
dc.Values["MachineId"] = 10;
dc.Values["Status"] = "Stopped";

int updated = dc.UpdateByKey();
```

Save/upsert:

```csharp
dc.Values["MachineId"] = 10;
dc.Values["MachineName"] = "Line 1";
dc.Values["Status"] = "Active";

object? key = dc.Save(askBeforeOverwrite: false);
```

Delete:

```csharp
int deleted = dc.DeleteByKey(10);
```

## Delegates / Callbacks

`DynamicClass` currently exposes this delegate:

```csharp
public delegate Task<bool> ConfirmUpdateAsync(string changeSummary, CancellationToken ct);
```

It is used by `SaveAsync` to let caller code decide whether an existing row should be overwritten.

Example:

```csharp
object? key = await dc.SaveAsync(
    askBeforeOverwrite: true,
    confirmAsync: async (changeSummary, ct) =>
    {
        Console.WriteLine(changeSummary);
        await Task.CompletedTask;
        return true; // return false to cancel the update
    });
```

The sync `Save` method uses a `MessageBox` confirmation when `askBeforeOverwrite` is `true`.

## Table Creation

Use `SchemaSpec` when the table should be created or ensured by code.

```csharp
var dc = new DynamicClass("dbo", "Machine", "MachineId");

dc.SchemaSpec["MachineId"] = new DynamicClass.ColumnDef
{
    Name = "MachineId",
    SqlType = "int",
    Nullable = false,
    Identity = true,
    PrimaryKey = true
};

dc.SchemaSpec["MachineName"] = new DynamicClass.ColumnDef
{
    Name = "MachineName",
    SqlType = "nvarchar(200)",
    Nullable = false
};

dc.EnsureTable();
```

DDL methods such as `CreateTable`, `EnsureTable`, and `AddColumn` work with physical SQL object names.

## Metadata

Metadata is stored in SQL Server extended properties.

Common helpers:

```csharp
dc.SetTableDisplayName("Machine Master");
dc.SetColumnDisplayName("MachineName", "Machine Name");
dc.SetColumnFormat("CreatedOn", "dd-MMM-yyyy HH:mm");
dc.SetOrder("MachineName", 1);
dc.SetShowInDataGrid("Status", true);
dc.SetHideInCrudForm("MachineId", true);
```

Read column metadata:

```csharp
List<DynamicClass.ColumnInfo>? columns = dc.GetColumns();
```

`GetColumns()` returns physical database column names plus metadata such as data type, nullability, identity, primary key, foreign key, display name, soft used name, units, format, order, and options.

## Relation Filter Example

```csharp
var rows = dc.SelectWithRelationFilters(
    new List<DynamicClass.RelationFilter>
    {
        new()
        {
            Direction = DynamicClass.ForeignFilterDirection.BaseToReference,
            BaseColumn = "CustomerId",
            RefSchema = "dbo",
            RefTable = "Customer",
            RefColumn = "CustomerId",
            FilterColumn = "City",
            Values = new List<object?> { "Ahmedabad", "Surat" }
        }
    },
    orderBy: "{CreatedOn} DESC");
```

## Error Handling

By default:

```csharp
DynamicClass.ThrowOnError = true;
```

When an operation fails, `DynamicClass`:

- Writes a log entry.
- Sets `LastErrorMessage`.
- Sets `LastException`.
- Rethrows the exception if `ThrowOnError` is `true`.

If `ThrowOnError` is `false`, failed methods return their default value.

## Practical Small Example

```csharp
using Aarohi.Classes;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;

DynamicClass.ConnectionFactory = () =>
    new SqlConnection("Server=.;Database=AarohiDb;Trusted_Connection=True;TrustServerCertificate=True;");

var machine = new DynamicClass("dbo", "Machine", "MachineId");

machine.SetColumnSoftUsedName("MachineName", "Name");
machine.SetColumnSoftUsedName("CreatedOn", "Created");

machine.Values["Name"] = "Line 1";
machine.Values["Status"] = "Active";
machine.Save(askBeforeOverwrite: false);

DataTable? firstPage = machine.Select(
    whereSql: "{Status} = @status",
    parameters: new Dictionary<string, object?> { ["status"] = "Active" },
    orderBy: "{Created} DESC",
    pageNumber: 1,
    pageSize: 25,
    DisplayName: true);
```

This example writes using the soft column name `Name`, filters with `{Status}`, orders with `{Created}`, and returns the first page of 25 rows.
