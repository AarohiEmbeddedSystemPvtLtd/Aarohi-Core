using Aarohi.Core.Logger;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Aarohi.Core.Exceptions;

namespace Aarohi.Classes
{
    public sealed partial class DynamicClass
    {

        #region DDL (TABLE/COLUMN CRUD) 

        #region Table Manager

        // Method: CreateTable
        /// <summary>
        /// Creates the table defined by <see cref="Schema"/> and <see cref="Table"/> using the column definitions in <see cref="SchemaSpec"/>.
        /// Optionally sets a primary key column.
        /// </summary>
        /// <param name="pkName">Optional column name to use as PK when no <see cref="ColumnDef.PrimaryKey"/> is marked.</param>
        /// <returns><c>true</c> on success.</returns>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="SchemaSpec"/> is empty.</exception>
        public bool CreateTable(string? pkName = null) => SafeExecute("DDL_CREATE_TABLE", extras =>
        {
            EnsureIdent(Schema); EnsureIdent(Table);
            if (SchemaSpec.Count == 0) throw new InvalidOperationException("SchemaSpec empty. Add columns before CreateTable.");

            using var cn = Open();
            using var tx = cn.BeginTransaction();
            try
            {
                CreateTableInternal(cn, tx, Schema, Table, SchemaSpec, pkName ?? KeyColumn, SqlType);
                tx.Commit();
                InvalidateColumnMetadataCache();
                return true;
            }
            catch { tx.Rollback(); throw; }
        });

        // Method: DropTable
        /// <summary>
        /// Drops the target table if it exists (idempotent).
        /// </summary>
        /// <returns><c>true</c> on success.</returns>
        public bool DropTable() => SafeExecute("DDL_DROP_TABLE", extras =>
        {
            EnsureIdent(Schema); EnsureIdent(Table);
            using var cn = Open();
            if (SqlType == SqlType.Sqlite)
            {
                using var cmd = CreateCommand(cn, $"DROP TABLE IF EXISTS \"{Table}\";");
                extras["sql"] = "DROP TABLE IF EXISTS";
                cmd.ExecuteNonQuery();

                EnsureMetadataTableExists(cn);
                using var metaCmd = CreateCommand(cn, "DELETE FROM [__Aarohi_Metadata] WHERE TableName = @t;");
                AddCommandParameter(metaCmd, "@t", Table);
                metaCmd.ExecuteNonQuery();
            }
            else if (SqlType == SqlType.PostgreSql)
            {
                var s = ResolveSchema(Schema, SqlType);
                using var cmd = CreateCommand(cn, $"DROP TABLE IF EXISTS \"{s}\".\"{Table}\" CASCADE;");
                extras["sql"] = "DROP TABLE IF EXISTS (postgres)";
                cmd.ExecuteNonQuery();

                EnsureMetadataTableExists(cn, sqlType: SqlType, schema: Schema);
                using var metaCmd = CreateCommand(cn, $"DELETE FROM \"{s}\".\"__Aarohi_Metadata\" WHERE \"TableName\" = @t;");
                AddCommandParameter(metaCmd, "@t", Table);
                metaCmd.ExecuteNonQuery();
            }
            else
            {
                using var cmd = CreateCommand(cn,
                    $"IF OBJECT_ID(@full,'U') IS NOT NULL DROP TABLE [{Schema}].[{Table}];");
                AddCommandParameter(cmd, "@full", $"{Schema}.{Table}");
                extras["sql"] = "DROP TABLE IF EXISTS";
                cmd.ExecuteNonQuery();
            }
            InvalidateColumnMetadataCache();
            return true;
        });

        // Method: EnsureTable
        /// <summary>
        /// Ensures the target table exists. If missing, creates it from <see cref="SchemaSpec"/>.
        /// If present and <see cref="SchemaSpec"/> has entries, adds any missing columns.
        /// </summary>
        /// <param name="pkName">Optional column name to consider as PK when creating.</param>
        /// <returns><c>true</c> when the operation completes successfully.</returns>
        public bool EnsureTable(string? pkName = null) => SafeExecute("DDL_ENSURE_TABLE", extras =>
        {
            EnsureIdent(Schema); EnsureIdent(Table);
            using var cn = Open();
            using var tx = cn.BeginTransaction();
            try
            {
                var exists = TableExists(cn, tx, Schema, Table, SqlType);
                extras["existsBefore"] = exists;

                if (!exists)
                {
                    if (SchemaSpec.Count == 0)
                        throw new InvalidOperationException("SchemaSpec empty. Provide definitions to create table.");
                    CreateTableInternal(cn, tx, Schema, Table, SchemaSpec, pkName ?? KeyColumn, SqlType);
                }
                else if (SchemaSpec.Count > 0)
                {
                    EnsureColumnsInternal(cn, tx, Schema, Table, SchemaSpec, SqlType);
                }

                tx.Commit();
                InvalidateColumnMetadataCache();
                return true;
            }
            catch { tx.Rollback(); throw; }
        });

        #endregion

        #region Column Manager

        // Method: AddColumn
        /// <summary>
        /// Adds a column to the target table if it does not already exist.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="sqlType">SQL type definition (e.g., "int", "nvarchar(200)").</param>
        /// <param name="nullable">Whether the column permits NULLs.</param>
        /// <param name="identity">Whether the column is an identity.</param>
        /// <returns><c>true</c> on success.</returns>
        public sealed class ColumnAddOptions
        {
            public required string Name { get; init; }                 // Column name
            public required string Type { get; init; }                 // Base type e.g. "varchar", "decimal", "int"

            public int? Length { get; init; }                          // For varchar/nvarchar/char/nchar/varbinary/binary; use -1 for MAX
            public int? Precision { get; init; }                       // For decimal/numeric
            public int? Scale { get; init; }                           // For decimal/numeric

            public bool Nullable { get; init; } = true;                // NOT NULL if false
            public bool Identity { get; init; } = false;               // IDENTITY(Seed,Inc)
            public int IdentitySeed { get; init; } = 1;
            public int IdentityIncrement { get; init; } = 1;

            public string? DefaultSql { get; init; }

            public string? ComputedExpressionSql { get; init; }
            public bool Persisted { get; init; } = false;
            public string? DefaultConstraintName { get; init; }
        }

        private const string EpName_AddedFromSoftware = "AddedFromSoftware";
        private static string EpValue_AddedFromSoftware = string.Empty;
        public static string Soft_Name
        {
            get => EpValue_AddedFromSoftware;
            set => EpValue_AddedFromSoftware = value ?? string.Empty;
        }

        private string BuildUpsertColumnExtendedPropertySql(string columnName)
        {
            string epName = EpName_AddedFromSoftware.Replace("'", "''");
            string epValue = EpValue_AddedFromSoftware.Replace("'", "''");

            return $@"
IF EXISTS (
    SELECT 1
    FROM sys.fn_listextendedproperty
    (N'{epName}', N'SCHEMA', N'{Schema}', N'TABLE', N'{Table}', N'COLUMN', N'{columnName}')
)
    EXEC sys.sp_updateextendedproperty
        @name = N'{epName}',
        @value = N'{epValue}',
        @level0type = N'SCHEMA', @level0name = N'{Schema}',
        @level1type = N'TABLE',  @level1name = N'{Table}',
        @level2type = N'COLUMN', @level2name = N'{columnName}';
ELSE
    EXEC sys.sp_addextendedproperty
        @name = N'{epName}',
        @value = N'{epValue}',
        @level0type = N'SCHEMA', @level0name = N'{Schema}',
        @level1type = N'TABLE',  @level1name = N'{Table}',
        @level2type = N'COLUMN', @level2name = N'{columnName}';
";
        }


        public bool AddColumn(string column, string sqlType, bool nullable = true, bool identity = false)
            => AddColumn(new ColumnAddOptions
            {
                Name = column,
                Type = sqlType,          // if you pass raw like "varchar(100)" it will be used as-is
                Nullable = nullable,
                Identity = identity
            });

        public bool AddColumn(ColumnAddOptions opt)
            => SafeExecute("DDL_ADD_COLUMN", extras =>
            {
                EnsureIdent(Schema);
                EnsureIdent(Table);
                EnsureIdent(opt.Name);

                using var cn = Open();

                if (SqlType == SqlType.Sqlite)
                {
                    bool exists = false;
                    using (var pragmaCmd = CreateCommand(cn, $"PRAGMA table_info(\"{Table}\");"))
                    using (var rd = pragmaCmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            if (string.Equals(rd["name"]?.ToString(), opt.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                exists = true;
                                break;
                            }
                        }
                    }

                    if (!exists)
                    {
                        string sqliteType = MapSqlTypeToSqlite(opt.Type);
                        string defaultClause = !string.IsNullOrWhiteSpace(opt.DefaultSql) ? $" DEFAULT ({opt.DefaultSql})" : "";
                        string nullClause = (!opt.Nullable && !string.IsNullOrWhiteSpace(opt.DefaultSql)) ? " NOT NULL" : " NULL";

                        string colSql;
                        if (!string.IsNullOrWhiteSpace(opt.ComputedExpressionSql))
                        {
                            var storage = opt.Persisted ? "STORED" : "VIRTUAL";
                            colSql = $"ALTER TABLE \"{Table}\" ADD COLUMN \"{opt.Name}\" AS ({opt.ComputedExpressionSql}) {storage};";
                        }
                        else
                        {
                            colSql = $"ALTER TABLE \"{Table}\" ADD COLUMN \"{opt.Name}\" {sqliteType}{defaultClause}{nullClause};";
                        }

                        using var cmd = CreateCommand(cn, colSql);
                        extras["sql"] = "ALTER TABLE ADD COLUMN (sqlite)";
                        cmd.ExecuteNonQuery();

                        if (!string.IsNullOrEmpty(EpValue_AddedFromSoftware))
                        {
                            UpsertExtendedProperty(EpName_AddedFromSoftware, EpValue_AddedFromSoftware, opt.Name);
                        }
                    }
                }
                else if (SqlType == SqlType.PostgreSql)
                {
                    var s = ResolveSchema(Schema, SqlType);
                    bool exists = false;
                    using (var chkCmd = CreateCommand(cn, "SELECT 1 FROM information_schema.columns WHERE table_schema = @s AND table_name = @t AND column_name = @c;"))
                    {
                        AddCommandParameter(chkCmd, "@s", s);
                        AddCommandParameter(chkCmd, "@t", Table);
                        AddCommandParameter(chkCmd, "@c", opt.Name);
                        exists = chkCmd.ExecuteScalar() != null;
                    }

                    if (!exists)
                    {
                        string rawType = ComposeTypeSql(opt);
                        string pgType = opt.Identity ? "BIGSERIAL" : MapSqlTypeToPostgreSql(rawType);
                        string defaultClause = !string.IsNullOrWhiteSpace(opt.DefaultSql) ? $" DEFAULT ({opt.DefaultSql})" : "";
                        string nullClause = opt.Nullable ? " NULL" : " NOT NULL";

                        string colSql;
                        if (!string.IsNullOrWhiteSpace(opt.ComputedExpressionSql))
                        {
                            colSql = $"ALTER TABLE \"{s}\".\"{Table}\" ADD COLUMN \"{opt.Name}\" {pgType} GENERATED ALWAYS AS ({opt.ComputedExpressionSql}) STORED;";
                        }
                        else
                        {
                            colSql = $"ALTER TABLE \"{s}\".\"{Table}\" ADD COLUMN \"{opt.Name}\" {pgType}{defaultClause}{nullClause};";
                        }

                        using var cmd = CreateCommand(cn, colSql);
                        extras["sql"] = "ALTER TABLE ADD COLUMN (postgres)";
                        cmd.ExecuteNonQuery();

                        if (!string.IsNullOrEmpty(EpValue_AddedFromSoftware))
                        {
                            UpsertExtendedProperty(EpName_AddedFromSoftware, EpValue_AddedFromSoftware, opt.Name);
                        }
                    }
                }
                else
                {
                    // If the column already exists => do nothing
                    using var cmd = CreateCommand(cn,
                        $"IF COL_LENGTH(@f,@c) IS NULL BEGIN {BuildAddColumnSql(opt, cn, extras)} END");

                    AddCommandParameter(cmd, "@f", $"{Schema}.{Table}");
                    AddCommandParameter(cmd, "@c", opt.Name);

                    extras["sql"] = "ALTER TABLE ADD COLUMN (rich)";
                    cmd.ExecuteNonQuery();
                }

                InvalidateColumnMetadataCache();
                return true;
            });

        private string BuildAddColumnSql(ColumnAddOptions opt, DbConnection cn, IDictionary<string, object> extras)
        {
            // Computed column rules
            bool isComputed = !string.IsNullOrWhiteSpace(opt.ComputedExpressionSql);

            if (opt.Persisted && !isComputed)
                throw new InvalidOperationException("Persisted can be used only with a computed column (ComputedExpressionSql).");

            if (isComputed)
            {
                if (opt.Identity)
                    throw new InvalidOperationException("Identity cannot be used on a computed column.");
                if (!string.IsNullOrWhiteSpace(opt.DefaultSql))
                    throw new InvalidOperationException("Default cannot be used on a computed column.");
            }

            if (!isComputed && !opt.Nullable && string.IsNullOrWhiteSpace(opt.DefaultSql))
            {
                using var check = CreateCommand(cn, $"SELECT TOP(1) 1 FROM [{Schema}].[{Table}];");
                var hasRows = check.ExecuteScalar() != null;
                if (hasRows)
                    throw new InvalidOperationException(
                        $"Cannot add NOT NULL column [{opt.Name}] without Default on a table that already has rows. " +
                        $"Either provide DefaultSql, or add as NULL first and then backfill + ALTER to NOT NULL.");
            }

            string typeSql = isComputed ? "" : ComposeTypeSql(opt);
            string nullSql = isComputed ? "" : (opt.Nullable ? "NULL" : "NOT NULL");
            string identitySql = (!isComputed && opt.Identity) ? $" IDENTITY({opt.IdentitySeed},{opt.IdentityIncrement})" : "";

            string defaultSql = "";
            if (!isComputed && !string.IsNullOrWhiteSpace(opt.DefaultSql))
            {
                var dfName = MakeSafeConstraintName(opt.DefaultConstraintName ?? $"DF_{Table}_{opt.Name}");
                EnsureIdent(dfName);
                defaultSql = $" CONSTRAINT [{dfName}] DEFAULT ({opt.DefaultSql})";
            }

            string colSql;
            if (isComputed)
            {
                var persistedSql = opt.Persisted ? " PERSISTED" : "";
                colSql = $"ALTER TABLE [{Schema}].[{Table}] ADD [{opt.Name}] AS ({opt.ComputedExpressionSql}){persistedSql};";
            }
            else
            {
                colSql = $"ALTER TABLE [{Schema}].[{Table}] ADD [{opt.Name}] {typeSql}{identitySql}{defaultSql} {nullSql};";
            }

            extras["column"] = opt.Name;
            extras["type"] = isComputed ? "COMPUTED" : typeSql;
            extras["nullable"] = opt.Nullable;
            extras["identity"] = opt.Identity;
            extras["default"] = opt.DefaultSql ?? "";
            extras["persisted"] = opt.Persisted;

            colSql += "\n" + BuildUpsertColumnExtendedPropertySql(opt.Name);

            extras["extended_property"] = $"{EpName_AddedFromSoftware}={EpValue_AddedFromSoftware}";

            return colSql;
        }

        private string ComposeTypeSql(ColumnAddOptions opt)
        {
            // If caller passed already-formed type like "varchar(100)" or "decimal(18,2)", use it as-is.
            // (This keeps your old behavior working.)
            string t = (opt.Type ?? "").Trim();
            if (t.Contains("(") || t.Contains(" "))
                return t;

            t = t.ToLowerInvariant();

            switch (t)
            {
                // Length types
                case "varchar":
                case "nvarchar":
                case "char":
                case "nchar":
                case "varbinary":
                case "binary":
                    if (opt.Length is null)
                        throw new InvalidOperationException($"{t} requires Length (use -1 for MAX where applicable).");

                    if (opt.Length == -1)
                    {
                        if (t is "char" or "nchar" or "binary")
                            throw new InvalidOperationException($"{t} does not support MAX.");
                        return $"{t}(MAX)";
                    }

                    if (opt.Length <= 0)
                        throw new InvalidOperationException($"{t} Length must be > 0 (or -1 for MAX).");

                    return $"{t}({opt.Length})";

                case "decimal":
                case "numeric":
                    int p = opt.Precision ?? 18;
                    int s = opt.Scale ?? 0;
                    if (p < 1 || p > 38) throw new InvalidOperationException($"{t} Precision must be 1..38.");
                    if (s < 0 || s > p) throw new InvalidOperationException($"{t} Scale must be 0..Precision.");
                    return $"{t}({p},{s})";

                case "datetime2":
                case "time":
                case "datetimeoffset":
                    if (opt.Scale is null) return t;
                    if (opt.Scale < 0 || opt.Scale > 7) throw new InvalidOperationException($"{t} Scale must be 0..7.");
                    return $"{t}({opt.Scale})";

                // Common fixed types
                default:
                    return t; // int, bigint, bit, float, real, date, datetime, uniqueidentifier, etc.
            }
        }

        private static string MakeSafeConstraintName(string name)
        {
            // SQL identifier rules are bigger than this, but this makes stable safe names.
            // Keep letters/digits/_ only. Trim to 128 chars.
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (var ch in name)
                sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');

            var s = sb.ToString().Trim('_');
            if (string.IsNullOrWhiteSpace(s)) s = "DF_" + Guid.NewGuid().ToString("N");

            return s.Length <= 128 ? s : s.Substring(0, 128);
        }


        // Method: AlterColumn
        /// <summary>
        /// Alters a column's type and nullability.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="sqlType">New SQL type.</param>
        /// <param name="nullable">New nullability.</param>
        /// <returns><c>true</c> on success.</returns>
        public sealed class ColumnAlterOptions
        {
            public required string Name { get; init; }
            public required string Type { get; init; }

            public int? Length { get; init; }      // -1 for MAX where allowed
            public int? Precision { get; init; }   // decimal/numeric
            public int? Scale { get; init; }       // decimal/numeric / datetime2 scale
            public bool Nullable { get; init; } = true;
        }

        public bool AlterColumn(ColumnAlterOptions opt)
            => SafeExecute("DDL_ALTER_COLUMN", extras =>
            {
                EnsureIdent(Schema); EnsureIdent(Table); EnsureIdent(opt.Name);

                using var cn = Open();

                if (SqlType == SqlType.Sqlite)
                {
                    // SQLite does not support ALTER TABLE ALTER COLUMN directly due to its dynamic type affinity system.
                    extras["sql"] = "-- SQLite: ALTER COLUMN not natively supported (dynamic typing); skipped";
                }
                else if (SqlType == SqlType.PostgreSql)
                {
                    var s = ResolveSchema(Schema, SqlType);
                    var rawType = ComposeTypeSql(new ColumnAddOptions
                    {
                        Name = opt.Name,
                        Type = opt.Type,
                        Length = opt.Length,
                        Precision = opt.Precision,
                        Scale = opt.Scale,
                        Nullable = opt.Nullable
                    });
                    var pgType = MapSqlTypeToPostgreSql(rawType);
                    var nullAction = opt.Nullable ? "DROP NOT NULL" : "SET NOT NULL";
                    var sql = $"ALTER TABLE \"{s}\".\"{Table}\" ALTER COLUMN \"{opt.Name}\" TYPE {pgType}, ALTER COLUMN \"{opt.Name}\" {nullAction};";

                    using var cmd = CreateCommand(cn, sql);
                    extras["sql"] = "ALTER TABLE ALTER COLUMN (postgres)";
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    // Build the type string the same way you do in AddColumn
                    var sqlType = ComposeTypeSql(new ColumnAddOptions
                    {
                        Name = opt.Name,
                        Type = opt.Type,
                        Length = opt.Length,
                        Precision = opt.Precision,
                        Scale = opt.Scale,
                        Nullable = opt.Nullable
                    });

                    using var cmd = CreateCommand(cn,
                        $"ALTER TABLE [{Schema}].[{Table}] ALTER COLUMN [{opt.Name}] {sqlType} {(opt.Nullable ? "NULL" : "NOT NULL")};");

                    extras["sql"] = "ALTER TABLE ALTER COLUMN (rich)";
                    cmd.ExecuteNonQuery();
                }
                InvalidateColumnMetadataCache();
                return true;
            });

        // Method: RenameColumn
        /// <summary>
        /// Renames a column using <c>sp_rename</c> (SQL Server) or <c>ALTER TABLE RENAME COLUMN</c> (SQLite).
        /// </summary>
        /// <param name="oldName">Existing column name.</param>
        /// <param name="newName">New column name.</param>
        /// <returns><c>true</c> on success.</returns>
        public bool RenameColumn(string oldName, string newName)
            => SafeExecute("DDL_RENAME_COLUMN", extras =>
            {
                EnsureIdent(Schema); EnsureIdent(Table); EnsureIdent(oldName); EnsureIdent(newName);
                using var cn = Open();

                if (SqlType == SqlType.Sqlite)
                {
                    using var cmd = CreateCommand(cn, $"ALTER TABLE \"{Table}\" RENAME COLUMN \"{oldName}\" TO \"{newName}\";");
                    extras["sql"] = "ALTER TABLE RENAME COLUMN";
                    cmd.ExecuteNonQuery();

                    EnsureMetadataTableExists(cn);
                    using var metaCmd = CreateCommand(cn, "UPDATE [__Aarohi_Metadata] SET ColumnName = @newName WHERE TableName = @t AND ColumnName = @oldName;");
                    AddCommandParameter(metaCmd, "@newName", newName);
                    AddCommandParameter(metaCmd, "@t", Table);
                    AddCommandParameter(metaCmd, "@oldName", oldName);
                    metaCmd.ExecuteNonQuery();
                }
                else if (SqlType == SqlType.PostgreSql)
                {
                    var s = ResolveSchema(Schema, SqlType);
                    using var cmd = CreateCommand(cn, $"ALTER TABLE \"{s}\".\"{Table}\" RENAME COLUMN \"{oldName}\" TO \"{newName}\";");
                    extras["sql"] = "ALTER TABLE RENAME COLUMN (postgres)";
                    cmd.ExecuteNonQuery();

                    EnsureMetadataTableExists(cn, sqlType: SqlType, schema: Schema);
                    using var metaCmd = CreateCommand(cn, $"UPDATE \"{s}\".\"__Aarohi_Metadata\" SET \"ColumnName\" = @newName WHERE \"TableName\" = @t AND \"ColumnName\" = @oldName;");
                    AddCommandParameter(metaCmd, "@newName", newName);
                    AddCommandParameter(metaCmd, "@t", Table);
                    AddCommandParameter(metaCmd, "@oldName", oldName);
                    metaCmd.ExecuteNonQuery();
                }
                else
                {
                    using var cmd = CreateCommand(cn, "EXEC sp_rename @full, @new, 'COLUMN';");
                    AddCommandParameter(cmd, "@full", $"{Schema}.{Table}.{oldName}");
                    AddCommandParameter(cmd, "@new", newName);
                    extras["sql"] = "sp_rename COLUMN";
                    cmd.ExecuteNonQuery();
                }
                InvalidateColumnMetadataCache();
                return true;
            });

        // Method: DropColumn
        /// <summary>
        /// Drops a column if it exists (idempotent).
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <returns><c>true</c> on success.</returns>
        public bool DropColumn(string column)
            => SafeExecute("DDL_DROP_COLUMN", extras =>
            {
                EnsureIdent(Schema); EnsureIdent(Table); EnsureIdent(column);
                using var cn = Open();

                if (SqlType == SqlType.Sqlite)
                {
                    bool exists = false;
                    using (var pragmaCmd = CreateCommand(cn, $"PRAGMA table_info(\"{Table}\");"))
                    using (var rd = pragmaCmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            if (string.Equals(rd["name"]?.ToString(), column, StringComparison.OrdinalIgnoreCase))
                            {
                                exists = true;
                                break;
                            }
                        }
                    }

                    if (exists)
                    {
                        using var cmd = CreateCommand(cn, $"ALTER TABLE \"{Table}\" DROP COLUMN \"{column}\";");
                        extras["sql"] = "ALTER TABLE DROP COLUMN";
                        cmd.ExecuteNonQuery();

                        EnsureMetadataTableExists(cn);
                        using var metaCmd = CreateCommand(cn, "DELETE FROM [__Aarohi_Metadata] WHERE TableName = @t AND ColumnName = @c;");
                        AddCommandParameter(metaCmd, "@t", Table);
                        AddCommandParameter(metaCmd, "@c", column);
                        metaCmd.ExecuteNonQuery();
                    }
                }
                else if (SqlType == SqlType.PostgreSql)
                {
                    var s = ResolveSchema(Schema, SqlType);
                    using var cmd = CreateCommand(cn, $"ALTER TABLE \"{s}\".\"{Table}\" DROP COLUMN IF EXISTS \"{column}\" CASCADE;");
                    extras["sql"] = "ALTER TABLE DROP COLUMN (postgres)";
                    cmd.ExecuteNonQuery();

                    EnsureMetadataTableExists(cn, sqlType: SqlType, schema: Schema);
                    using var metaCmd = CreateCommand(cn, $"DELETE FROM \"{s}\".\"__Aarohi_Metadata\" WHERE \"TableName\" = @t AND \"ColumnName\" = @c;");
                    AddCommandParameter(metaCmd, "@t", Table);
                    AddCommandParameter(metaCmd, "@c", column);
                    metaCmd.ExecuteNonQuery();
                }
                else
                {
                    using var cmd = CreateCommand(cn,
                        $"IF COL_LENGTH(@f,@c) IS NOT NULL ALTER TABLE [{Schema}].[{Table}] DROP COLUMN [{column}];");
                    AddCommandParameter(cmd, "@f", $"{Schema}.{Table}");
                    AddCommandParameter(cmd, "@c", column);
                    extras["sql"] = "ALTER TABLE DROP COLUMN";
                    cmd.ExecuteNonQuery();
                }
                InvalidateColumnMetadataCache();
                return true;
            });

        #endregion

        #region Column Options
        // Method: GetOptions
        /// <summary>
        /// Returns the current set of options from the column's Options extended property.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <returns>Array of options; empty if none detected.</returns>
        public string[] GetOptions(string column)
        {
            var cols = GetColumns() ?? new List<ColumnInfo>();
            var ci = cols.FirstOrDefault(c => c.Name.Equals(column, StringComparison.OrdinalIgnoreCase));
            return (ci?.Options ?? Array.Empty<string>())
                   .Where(s => !string.IsNullOrWhiteSpace(s))
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToArray();
        }

        // Method: GetOptionsExtended
        /// <summary>
        /// Returns the current set of options from the column's Options extended property.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <returns>Array of options; empty if none detected.</returns>
        public string[] GetOptionsExtended(string column)
        {
            EnsureIdent(Schema);
            EnsureIdent(Table);

            if (string.IsNullOrWhiteSpace(column))
                return Array.Empty<string>();

            var value = ReadExtendedProperty("Options", column)?.ToString();

            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();

            return value
                .Split(',')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        // Method: AddOption
        /// <summary>
        /// Adds a single option to a column's Options extended property.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="option">Option value to add.</param>
        /// <returns><c>true</c> on success.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="option"/> is null/empty.</exception>
        public bool AddOption(string column, string option)
        {
            if (string.IsNullOrWhiteSpace(option))
                throw new ArgumentException("Option cannot be empty.", nameof(option));

            var current = GetOptionsExtended(column).ToList();
            if (!current.Contains(option, StringComparer.OrdinalIgnoreCase))
                current.Add(option);

            return SetOptions(column, current);
        }

        // Method: RemoveOption
        /// <summary>
        /// Removes an option from a column's Options extended property. If no options remain, removes the property.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="option">Option value to remove.</param>
        /// <returns><c>true</c> on success.</returns>
        public bool RemoveOption(string column, string option)
        {
            var list = GetOptionsExtended(column).ToList();
            var removed = list.RemoveAll(s => s.Equals(option, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return true; // nothing to do

            if (list.Count == 0)
                return ClearOptions(column);

            return SetOptions(column, list);
        }

        // Method: SetOptions
        /// <summary>
        /// Replaces the options for a column using the Options extended property.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="options">New set of options (must contain at least one non-empty value).</param>
        /// <returns><c>true</c> on success.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no options are provided or column is not found.</exception>
        public bool SetOptions(string column, IEnumerable<string> options)
            => SafeExecute("DDL_SET_OPTIONS", extras =>
            {
                EnsureIdent(Schema); EnsureIdent(Table);

                var opts = (options ?? Enumerable.Empty<string>())
                   .Where(s => !string.IsNullOrWhiteSpace(s))
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToArray();

                if (opts.Length == 0)
                    throw new InvalidOperationException($"No options provided for column '{column}'.");

                var colInfo = (GetColumns() ?? new List<ColumnInfo>())
                      .FirstOrDefault(c => c.Name.Equals(column, StringComparison.OrdinalIgnoreCase));
                if (colInfo is null)
                    throw new InvalidOperationException($"Column '{column}' not found in [{Schema}].[{Table}].");

                var optionsText = string.Join(",", opts);
                var saved = UpsertExtendedProperty("Options", optionsText, column);

                AddCommonExtras(extras,
                    ("extendedProperty", "Options"),
                    ("optionsCount", opts.Length));

                return saved;
            });

        public bool ClearOptions(string column)
            => SafeExecute("DDL_CLEAR_OPTIONS", extras =>
            {
                EnsureIdent(Schema); EnsureIdent(Table);

                var colInfo = (GetColumns() ?? new List<ColumnInfo>())
                    .FirstOrDefault(c => c.Name.Equals(column, StringComparison.OrdinalIgnoreCase));
                if (colInfo is null)
                    throw new InvalidOperationException($"Column '{column}' not found in [{Schema}].[{Table}].");

                var removed = DropExtendedProperty("Options", column);
                AddCommonExtras(extras, ("extendedProperty", "Options"), ("removed", removed));
                return removed;
            });

        private static bool IsNumericType(string sqlType)
        {
            if (string.IsNullOrWhiteSpace(sqlType)) return false;
            var t = sqlType.Trim().ToLowerInvariant();

            return t.StartsWith("int") || t.StartsWith("bigint") || t.StartsWith("smallint") ||
                   t.StartsWith("tinyint") || t.StartsWith("decimal") || t.StartsWith("numeric") ||
                   t.StartsWith("float") || t.StartsWith("real") || t.StartsWith("money") ||
                   t.StartsWith("smallmoney");
        }



        #endregion

        #endregion
    }
}
