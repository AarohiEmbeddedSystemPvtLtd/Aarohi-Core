using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Aarohi.Classes;

/// <summary>
/// Provides automated database provisioning and dynamic table synchronization via Reflection and DynamicClass.
/// Fully supports every SqlType (SqlServer, Sqlite, PostgreSql, MySql).
/// </summary>
public static class DynamicDatabaseTable
{
    private static readonly Regex ValidIdent = new Regex("^[A-Za-z0-9][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>
    /// Gets or sets the default SqlType used when not explicitly specified.
    /// </summary>
    public static SqlType DefaultSqlType { get; set; } = SqlType.SqlServer;

    /// <summary>
    /// Gets or sets the default schema name (defaults to "dbo" for SQL Server, "" for SQLite, "public" for PostgreSQL).
    /// </summary>
    public static string DefaultSchema { get; set; } = "dbo";

    /// <summary>
    /// Optional custom connection factory for custom connection pooling or unsupported drivers.
    /// </summary>
    public static Func<DbConnection>? ConnectionFactory { get; set; }

    /// <summary>
    /// Gets the most recently used connection string.
    /// </summary>
    public static string ConnectionString { get; private set; } = "";

    public static void EnsureIdent(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !ValidIdent.IsMatch(name))
            throw new ArgumentException($"Invalid SQL identifier: '{name}'");
    }

    /// <summary>
    /// Creates a newly initialized DbConnection instance for the given SqlType and connection string.
    /// </summary>
    public static DbConnection CreateConnection(SqlType sqlType, string connectionString)
    {
        if (ConnectionFactory != null)
            return ConnectionFactory();

        return sqlType switch
        {
            SqlType.SqlServer => new SqlConnection(connectionString),
            SqlType.Sqlite => new SqliteConnection(connectionString),
            SqlType.PostgreSql => new NpgsqlConnection(connectionString),
            _ => throw new NotSupportedException($"Default connection factory for SqlType.{sqlType} is not registered. Set DynamicDatabaseTable.ConnectionFactory.")
        };
    }

    #region EnsureDatabase

    /// <summary>
    /// Ensures that the target database exists across any SqlType.
    /// If the database is detected, creation is skipped unless forceSyncTablesIfDbExists is true.
    /// </summary>
    /// <param name="sqlType">The target SQL database engine.</param>
    /// <param name="databaseNameOrPath">Database name, SQLite file path, or connection string.</param>
    /// <param name="connectionString">Optional server or database connection string.</param>
    /// <param name="schema">Optional schema name override.</param>
    /// <param name="targetAssembly">Assembly containing table specification classes (defaults to calling assembly).</param>
    /// <param name="targetNamespace">Namespace containing table specification classes (optional; scans all nested Columns classes if omitted).</param>
    /// <param name="forceSyncTablesIfDbExists">When true, provisions/synchronizes tables even if the database already exists.</param>
    /// <returns>True when the operation succeeds.</returns>
    public static bool EnsureDatabase(
        SqlType sqlType,
        string databaseNameOrPath,
        string? connectionString = null,
        string? schema = null,
        Assembly? targetAssembly = null,
        string? targetNamespace = null,
        bool forceSyncTablesIfDbExists = false)
    {
        if (targetAssembly == null || targetAssembly == typeof(DynamicDatabaseTable).Assembly)
        {
            targetAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        }

        return sqlType switch
        {
            SqlType.Sqlite => EnsureSqliteInternal(databaseNameOrPath, connectionString, targetAssembly, targetNamespace, forceSyncTablesIfDbExists),
            SqlType.SqlServer => EnsureSqlServerInternal(databaseNameOrPath, connectionString, schema, targetAssembly, targetNamespace, forceSyncTablesIfDbExists),
            SqlType.PostgreSql => EnsurePostgreSqlInternal(databaseNameOrPath, connectionString, schema, targetAssembly, targetNamespace, forceSyncTablesIfDbExists),
            SqlType.MySql => EnsureMySqlInternal(databaseNameOrPath, connectionString, targetAssembly, targetNamespace, forceSyncTablesIfDbExists),
            _ => throw new NotSupportedException($"SqlType '{sqlType}' is not supported by EnsureDatabase.")
        };
    }

    /// <summary>
    /// Convenience method for SQLite databases.
    /// </summary>
    public static bool EnsureSqliteDatabase(
        string databasePathOrName,
        Assembly? targetAssembly = null,
        string? targetNamespace = null,
        bool forceSyncTablesIfDbExists = false)
    {
        if (targetAssembly == null || targetAssembly == typeof(DynamicDatabaseTable).Assembly)
        {
            targetAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        }
        return EnsureDatabase(SqlType.Sqlite, databasePathOrName, null, "dbo", targetAssembly, targetNamespace, forceSyncTablesIfDbExists);
    }

    /// <summary>
    /// Convenience method using DefaultSqlType.
    /// </summary>
    public static bool EnsureDatabase(
        string databaseNameOrPath,
        string? connectionString = null,
        bool forceSyncTablesIfDbExists = false)
    {
        return EnsureDatabase(DefaultSqlType, databaseNameOrPath, connectionString, null, Assembly.GetCallingAssembly(), null, forceSyncTablesIfDbExists);
    }

    private static bool EnsureSqliteInternal(
        string databasePathOrName,
        string? connectionString,
        Assembly targetAssembly,
        string? targetNamespace,
        bool forceSyncTablesIfDbExists)
    {
        string targetConnectionString;
        string dbFilePath;

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var csb = new SqliteConnectionStringBuilder(connectionString);
            dbFilePath = csb.DataSource;
            if (!string.IsNullOrWhiteSpace(dbFilePath) && !Path.IsPathRooted(dbFilePath))
            {
                dbFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbFilePath);
                csb.DataSource = dbFilePath;
            }
            targetConnectionString = csb.ConnectionString;
        }
        else if (databasePathOrName.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            var csb = new SqliteConnectionStringBuilder(databasePathOrName);
            dbFilePath = csb.DataSource;
            if (!string.IsNullOrWhiteSpace(dbFilePath) && !Path.IsPathRooted(dbFilePath))
            {
                dbFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbFilePath);
                csb.DataSource = dbFilePath;
            }
            targetConnectionString = csb.ConnectionString;
        }
        else
        {
            dbFilePath = databasePathOrName;
            if (!Path.HasExtension(dbFilePath))
            {
                dbFilePath += ".db";
            }
            if (!Path.IsPathRooted(dbFilePath))
            {
                dbFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbFilePath);
            }
            var csb = new SqliteConnectionStringBuilder { DataSource = dbFilePath };
            targetConnectionString = csb.ConnectionString;
        }

        ConnectionString = targetConnectionString;
        DynamicClass.ConfigureDefault(SqlType.Sqlite, () => new SqliteConnection(targetConnectionString));

        bool exists = File.Exists(dbFilePath);
        if (exists && !forceSyncTablesIfDbExists)
        {
            return true;
        }

        string? dir = Path.GetDirectoryName(Path.GetFullPath(dbFilePath));
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        TryToCreateTable(SqlType.Sqlite, targetConnectionString, "dbo", targetAssembly, targetNamespace);
        return true;
    }

    private static bool EnsureSqlServerInternal(
        string databaseName,
        string? connectionString,
        string? schema,
        Assembly targetAssembly,
        string? targetNamespace,
        bool forceSyncTablesIfDbExists)
    {
        EnsureIdent(databaseName);
        string serverConnStr = connectionString ?? ConnectionString;
        if (string.IsNullOrWhiteSpace(serverConnStr))
            throw new ArgumentException("A connection string is required for SQL Server database discovery.", nameof(connectionString));

        var masterCsb = new SqlConnectionStringBuilder(serverConnStr) { InitialCatalog = "master" };
        using (var masterConn = new SqlConnection(masterCsb.ConnectionString))
        {
            masterConn.Open();
            using var checkCmd = masterConn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(1) FROM sys.databases WHERE name = @dbName;";
            var p = checkCmd.CreateParameter();
            p.ParameterName = "@dbName";
            p.Value = databaseName;
            checkCmd.Parameters.Add(p);

            bool exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
            var targetCsb = new SqlConnectionStringBuilder(serverConnStr) { InitialCatalog = databaseName };
            ConnectionString = targetCsb.ConnectionString;

            if (!exists)
            {
                using var createCmd = masterConn.CreateCommand();
                createCmd.CommandText = $"CREATE DATABASE [{databaseName}];";
                createCmd.ExecuteNonQuery();
                TryToCreateTable(SqlType.SqlServer, ConnectionString, schema ?? "dbo", targetAssembly, targetNamespace);
            }
            else if (forceSyncTablesIfDbExists)
            {
                TryToCreateTable(SqlType.SqlServer, ConnectionString, schema ?? "dbo", targetAssembly, targetNamespace);
            }
        }

        return true;
    }

    private static bool EnsurePostgreSqlInternal(
        string databaseName,
        string? connectionString,
        string? schema,
        Assembly targetAssembly,
        string? targetNamespace,
        bool forceSyncTablesIfDbExists)
    {
        EnsureIdent(databaseName);
        string serverConnStr = connectionString ?? ConnectionString;
        if (string.IsNullOrWhiteSpace(serverConnStr))
            throw new ArgumentException("A connection string is required for PostgreSQL database discovery.", nameof(connectionString));

        var masterCsb = new NpgsqlConnectionStringBuilder(serverConnStr) { Database = "postgres" };
        using (var masterConn = new NpgsqlConnection(masterCsb.ConnectionString))
        {
            masterConn.Open();
            using var checkCmd = masterConn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(1) FROM pg_database WHERE datname = @dbName;";
            var p = checkCmd.CreateParameter();
            p.ParameterName = "@dbName";
            p.Value = databaseName;
            checkCmd.Parameters.Add(p);

            bool exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
            var targetCsb = new NpgsqlConnectionStringBuilder(serverConnStr) { Database = databaseName };
            ConnectionString = targetCsb.ConnectionString;

            if (!exists)
            {
                using var createCmd = masterConn.CreateCommand();
                createCmd.CommandText = $"CREATE DATABASE \"{databaseName}\";";
                createCmd.ExecuteNonQuery();
                TryToCreateTable(SqlType.PostgreSql, ConnectionString, schema ?? "public", targetAssembly, targetNamespace);
            }
            else if (forceSyncTablesIfDbExists)
            {
                TryToCreateTable(SqlType.PostgreSql, ConnectionString, schema ?? "public", targetAssembly, targetNamespace);
            }
        }

        return true;
    }

    private static bool EnsureMySqlInternal(
        string databaseName,
        string? connectionString,
        Assembly targetAssembly,
        string? targetNamespace,
        bool forceSyncTablesIfDbExists)
    {
        EnsureIdent(databaseName);
        string serverConnStr = connectionString ?? ConnectionString;
        if (string.IsNullOrWhiteSpace(serverConnStr))
            throw new ArgumentException("A connection string is required for MySQL database discovery.", nameof(connectionString));

        using (var conn = CreateConnection(SqlType.MySql, serverConnStr))
        {
            conn.Open();
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(1) FROM information_schema.schemata WHERE schema_name = @dbName;";
            var p = checkCmd.CreateParameter();
            p.ParameterName = "@dbName";
            p.Value = databaseName;
            checkCmd.Parameters.Add(p);

            bool exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
            ConnectionString = serverConnStr;

            if (!exists)
            {
                using var createCmd = conn.CreateCommand();
                createCmd.CommandText = $"CREATE DATABASE `{databaseName}`;";
                createCmd.ExecuteNonQuery();
                TryToCreateTable(SqlType.MySql, ConnectionString, "dbo", targetAssembly, targetNamespace);
            }
            else if (forceSyncTablesIfDbExists)
            {
                TryToCreateTable(SqlType.MySql, ConnectionString, "dbo", targetAssembly, targetNamespace);
            }
        }

        return true;
    }

    #endregion

    #region TryToCreateTable

    /// <summary>
    /// Scans the target assembly via Reflection for table specification classes and provisions tables and columns for any SqlType.
    /// </summary>
    public static void TryToCreateTable(
        SqlType sqlType,
        string targetConnectionString,
        string? schema = null,
        Assembly? targetAssembly = null,
        string? targetNamespace = null)
    {
        if (string.IsNullOrEmpty(targetConnectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(targetConnectionString));

        ConnectionString = targetConnectionString;
        if (targetAssembly == null || targetAssembly == typeof(DynamicDatabaseTable).Assembly)
        {
            targetAssembly = Assembly.GetEntryAssembly();
            if (targetAssembly == null || targetAssembly == typeof(DynamicDatabaseTable).Assembly)
            {
                targetAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => !a.IsDynamic && a != typeof(DynamicDatabaseTable).Assembly && a.GetTypes().Any(t => t.GetNestedType("Columns", BindingFlags.Public | BindingFlags.Static) != null))
                    ?? Assembly.GetCallingAssembly();
            }
        }
        schema ??= (sqlType == SqlType.PostgreSql ? "public" : "dbo");

        // Set global default connection for DynamicClass
        DynamicClass.ConfigureDefault(sqlType, () => CreateConnection(sqlType, targetConnectionString));

        try
        {
            // 1. Ensure schema exists if applicable
            EnsureSchemaExists(sqlType, schema, targetConnectionString);

            // 2. Discover specification classes
            var specificationClasses = DiscoverTableSpecificationClasses(targetAssembly, targetNamespace);

            // 3. Provision each discovered table
            foreach (Type specClass in specificationClasses)
            {
                string tableName = specClass.GetField("Name")?.GetValue(null)?.ToString() ?? specClass.Name;
                string tableDisplayName = specClass.GetField("DisplayName")?.GetValue(null)?.ToString() ?? specClass.Name;
                string description = specClass.GetField("Description")?.GetValue(null)?.ToString() ?? specClass.Name;
                string groups = specClass.GetField("Groups")?.GetValue(null)?.ToString() ?? specClass.Name;

                Type? columnsClass = specClass.GetNestedType("Columns", BindingFlags.Public | BindingFlags.Static);
                if (columnsClass == null)
                    continue;

                List<Column> columns = GetColumns(columnsClass);
                if (columns.Count == 0)
                    continue;

                Column? pkColumn = columns.FirstOrDefault(c => c.IsPrimaryKey)
                    ?? columns.FirstOrDefault(c => c.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                                                   c.Name.Equals($"{tableName}Id", StringComparison.OrdinalIgnoreCase))
                    ?? columns.FirstOrDefault();

                if (pkColumn == null)
                    continue;

                DynamicClass dc = new DynamicClass(schema, tableName, pkColumn.Name, sqlType);
                dc.InstanceConnectionFactory = () => CreateConnection(sqlType, targetConnectionString);

                // Build Primary Key Definition
                string builtSqlType = MapDataType(sqlType, pkColumn.DataType, pkColumn.MaxLength, pkColumn.Precision, pkColumn.Scale);

                var pkDef = new DynamicClass.ColumnDef
                {
                    Name = pkColumn.Name,
                    SqlType = builtSqlType,
                    Nullable = pkColumn.Nullable,
                    Identity = pkColumn.Identity,
                    PrimaryKey = true
                };
                pkDef.Validate();
                dc.SchemaSpec.Add(pkColumn.Name, pkDef);

                // Create or verify table
                dc.EnsureTable();

                // Set table metadata
                dc.SetTableDisplayName(tableDisplayName);
                dc.SetTableProperty("Description", description);
                dc.SetTableProperty("Groups", groups);

                // Set PK column extended properties
                SetColumnMetadataProperties(dc, pkColumn);

                // Add and configure non-PK columns
                foreach (Column column in columns)
                {
                    if (column.Name.Equals(pkColumn.Name, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string cleanDataType = column.DataType?.Trim().ToLowerInvariant() ?? "";
                    bool isStringColumn = cleanDataType.Contains("varchar") ||
                                          cleanDataType.Contains("char") ||
                                          cleanDataType.Contains("text");

                    string targetType = column.DataType ?? (isStringColumn ? "TEXT" : "INTEGER");
                    if (isStringColumn && cleanDataType.Contains("("))
                    {
                        targetType = targetType.Split('(')[0].Trim();
                    }

                    int? finalLength = null;
                    if (isStringColumn)
                    {
                        if (column.MaxLength > 0 || column.MaxLength == -1)
                            finalLength = column.MaxLength;
                        else if (column.Precision > 0)
                            finalLength = column.Precision;
                        else
                            finalLength = 255;
                    }
                    else
                    {
                        if (column.MaxLength > 0 || column.MaxLength == -1)
                            finalLength = column.MaxLength;
                    }

                    var opt = new DynamicClass.ColumnAddOptions
                    {
                        Name = column.Name,
                        Type = targetType,
                        Nullable = column.Nullable,
                        Identity = column.Identity,
                        DefaultSql = column.DefaultValue?.ToString(),
                        Length = finalLength,
                        Precision = !isStringColumn && column.Precision > 0 ? (int?)column.Precision : null,
                        Scale = !isStringColumn && column.Scale > 0 ? (int?)column.Scale : null
                    };

                    dc.AddColumn(opt);

                    // Set column-level extended properties
                    SetColumnMetadataProperties(dc, column);
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Critical failure mapping metadata classes to {sqlType} tables via Reflection.", ex);
        }
    }

    private static void SetColumnMetadataProperties(DynamicClass dc, Column column)
    {
        foreach (PropertyInfo property in column.GetType().GetProperties())
        {
            if (property.Name == "Name" || property.Name == "DataType")
                continue;

            object? val = property.GetValue(column);
            if (val == null)
                continue;

            string propertyValueString = val is string[] array
                ? string.Join(",", array)
                : val.ToString() ?? "";

            dc.SetColumnProperty(property.Name, column.Name, propertyValueString);
        }
    }

    public static void TryToCreateTable(string targetConnectionString)
        => TryToCreateTable(DefaultSqlType, targetConnectionString, DefaultSchema, Assembly.GetCallingAssembly(), null);

    private static void EnsureSchemaExists(SqlType sqlType, string schema, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(schema))
            return;

        if (sqlType == SqlType.SqlServer && !schema.Equals("dbo", StringComparison.OrdinalIgnoreCase))
        {
            EnsureIdent(schema);
            using var conn = CreateConnection(sqlType, connectionString);
            conn.Open();
            string createSchemaSql = $@"IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = @schemaName)
                BEGIN
                    EXEC('CREATE SCHEMA [{schema}]');
                END;";
            using var cmd = conn.CreateCommand();
            cmd.CommandText = createSchemaSql;
            var p = cmd.CreateParameter();
            p.ParameterName = "@schemaName";
            p.Value = schema;
            cmd.Parameters.Add(p);
            cmd.ExecuteNonQuery();
        }
        else if (sqlType == SqlType.PostgreSql && !schema.Equals("public", StringComparison.OrdinalIgnoreCase))
        {
            EnsureIdent(schema);
            using var conn = CreateConnection(sqlType, connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"CREATE SCHEMA IF NOT EXISTS ""{schema}"";";
            cmd.ExecuteNonQuery();
        }
    }

    private static IEnumerable<Type> DiscoverTableSpecificationClasses(Assembly targetAssembly, string? targetNamespace)
    {
        var classes = targetAssembly.GetTypes().Where(t => t.IsClass && !t.IsNested);

        if (!string.IsNullOrWhiteSpace(targetNamespace))
        {
            classes = classes.Where(t => t.Namespace == targetNamespace);
        }

        // Return only classes that contain a nested static "Columns" class
        return classes.Where(t => t.GetNestedType("Columns", BindingFlags.Public | BindingFlags.Static) != null);
    }

    private static List<Column> GetColumns(Type columnsClass)
    {
        return columnsClass
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.FieldType == typeof(Column))
            .Select(x => (Column)x.GetValue(null)!)
            .OrderBy(x => x.Order)
            .ToList();
    }

    private static string MapDataType(SqlType sqlType, string? baseType, int maxLength, byte precision, byte scale)
    {
        string type = baseType ?? (sqlType == SqlType.Sqlite ? "INTEGER" : "int");

        if (sqlType == SqlType.Sqlite)
        {
            return type;
        }

        if (maxLength > 0)
            return $"{type}({maxLength})";
        if (precision > 0 && scale > 0)
            return $"{type}({precision},{scale})";

        return type;
    }

    #endregion

    #region Table Property Helpers

    /// <summary>
    /// Sets a table-level custom property across any supported SqlType.
    /// </summary>
    public static void SetTableProperty(SqlType sqlType, string connectionString, string schema, string tableName, string propertyName, object? value)
    {
        DynamicClass dc = new DynamicClass(schema, tableName, null, sqlType);
        dc.InstanceConnectionFactory = () => CreateConnection(sqlType, connectionString);
        dc.SetTableProperty(propertyName, value);
    }

    /// <summary>
    /// Reads a table-level custom property across any supported SqlType.
    /// </summary>
    public static object? GetTableProperty(SqlType sqlType, string connectionString, string schema, string tableName, string propertyName)
    {
        DynamicClass dc = new DynamicClass(schema, tableName, null, sqlType);
        dc.InstanceConnectionFactory = () => CreateConnection(sqlType, connectionString);
        return dc.GetTableProperty(propertyName);
    }

    #endregion
}
