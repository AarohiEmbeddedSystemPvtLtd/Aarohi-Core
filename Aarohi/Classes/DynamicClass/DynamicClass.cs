using Aarohi.Core.Logger;
using Aarohi.Globals;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;
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
    public sealed partial class DynamicClass : IDisposable
    {
        #region Fields
        // ---------- Global config ----------
        public static bool LogInfo { get; set; } = true;
        public static bool LogTrace { get; set; } = false;
        public static int BulkCopyTimeoutSeconds { get; set; } = 600;

        /// <summary>
        /// Gets or sets the default database engine type for DynamicClass. Defaults to <see cref="SqlType.SqlServer"/>.
        /// </summary>
        public static SqlType DefaultSqlType { get; set; } = SqlType.SqlServer;

        /// <summary>
        /// Gets or sets the database engine type for this DynamicClass instance. Defaults to <see cref="DefaultSqlType"/>.
        /// </summary>
        public SqlType SqlType { get; set; } = DefaultSqlType;

        // ---------- Core properties ----------
        public string Schema { get; set; } = "dbo";
        public string Table { get; set; } = "";
        public string KeyColumn { get; set; } = "";
        public string LogSource { get; set; } = "DynamicClass";

        private Func<DbConnection>? _instanceFactory;

        public Func<DbConnection>? InstanceConnectionFactory
        {
            get => Volatile.Read(ref _instanceFactory);
            set
            {
                if (value is null) throw new ArgumentNullException(nameof(InstanceConnectionFactory));
                Volatile.Write(ref _instanceFactory, value);
            }
        }

        private static Func<DbConnection>? _factory;
        public static Func<DbConnection>? ConnectionFactory
        {
            get => Volatile.Read(ref _factory) ?? throw new InvalidOperationException("DefaultConnectionFactory is not set.");
            set
            {
                if (value is null) throw new ArgumentNullException(nameof(ConnectionFactory));
                Volatile.Write(ref _factory, value);
            }
        }

        /// <summary>
        /// Configures the global default database engine and connection factory.
        /// </summary>
        /// <param name="sqlType">The SQL database engine to use (e.g. SqlServer, Sqlite).</param>
        /// <param name="factory">Delegate returning a newly created, unmanaged connection.</param>
        public static void ConfigureDefault(SqlType sqlType, Func<DbConnection> factory)
        {
            DefaultSqlType = sqlType;
            ConnectionFactory = factory;
        }

        /// <summary>
        /// Configures the instance-specific database engine and connection factory.
        /// </summary>
        /// <param name="sqlType">The SQL database engine to use for this instance.</param>
        /// <param name="factory">Delegate returning a newly created connection.</param>
        public void ConfigureInstance(SqlType sqlType, Func<DbConnection> factory)
        {
            SqlType = sqlType;
            InstanceConnectionFactory = factory;
        }

        private Func<DbConnection> ResolveFactory()
        {
            return _instanceFactory
                ?? ConnectionFactory
                ?? throw new InvalidOperationException(
                    "DynamicClass.ConnectionFactory is not set. Set default at startup or set InstanceConnectionFactory for this instance.");
        }

        public Dictionary<string, object?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ColumnDef> SchemaSpec { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? LastErrorMessage { get; private set; }
        public Exception? LastException { get; private set; }

        private static readonly ConcurrentDictionary<string, List<ColumnInfo>> ColumnMetadataCache =
            new(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Constructors and Disposers

        /// <summary>
        /// Initializes a new <see cref="DynamicClass"/> with default settings.
        /// </summary>
        public DynamicClass()
        {
            SqlType = DefaultSqlType;
        }

        /// <summary>
        /// Initializes a new <see cref="DynamicClass"/> targeting a specific table.
        /// If <paramref name="keyColumn"/> is null or empty, attempts to auto-detect
        /// the key column (PK preferred; falls back to identity if enabled).
        /// </summary>
        /// <param name="schema">Database schema name (e.g., "dbo").</param>
        /// <param name="table">Database table name.</param>
        /// <param name="keyColumn">Optional key column name.</param>
        /// <param name="sqlType">Optional database engine type override.</param>
        public DynamicClass(string schema, string table, string? keyColumn = null, SqlType? sqlType = null)
        {
            SqlType = sqlType ?? DefaultSqlType;
            Schema = schema;
            Table = table;
            KeyColumn = keyColumn?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(KeyColumn))
            {
                DetectAndSetKeyColumn(preferIdentityFallback: true, throwOnComposite: false);
            }
        }

        public DynamicClass(
                    string schema,
                    string table,
                    Func<DbConnection> connectionFactory,
                    string? keyColumn = null,
                    SqlType? sqlType = null)
        {
            if (connectionFactory == null)
                throw new ArgumentNullException(nameof(connectionFactory));

            SqlType = sqlType ?? DefaultSqlType;
            Schema = schema;
            Table = table;
            KeyColumn = keyColumn?.Trim() ?? "";

            // Important: set instance factory before DetectAndSetKeyColumn()
            _instanceFactory = connectionFactory;

            if (string.IsNullOrWhiteSpace(KeyColumn))
            {
                DetectAndSetKeyColumn(preferIdentityFallback: true, throwOnComposite: false);
            }
        }

        /// <summary>
        /// Releases transient state (last error, values, schema spec) and suppresses finalization.
        /// </summary>
        public void Dispose()
        {
            if (LastException != null)
            {
                LastException = null;
            }

            Values.Clear();
            SchemaSpec.Clear();

            GC.SuppressFinalize(this);
        }

        private string GetColumnMetadataCacheKey()
        {
            EnsureIdent(Schema);
            EnsureIdent(Table);

            Func<DbConnection> factory = ResolveFactory();

            using DbConnection con = factory();

            string server = con.DataSource?.Trim() ?? "";
            string database = con.Database?.Trim() ?? "";

            return $"{SqlType}|{server}|{database}|{Schema}.{Table}";
        }

        private void InvalidateColumnMetadataCache()
        {
            ColumnMetadataCache.TryRemove(GetColumnMetadataCacheKey(), out _);
        }

        public bool RemoveColumnProperty(string propertyName, string column)
        {
            return DropExtendedProperty(propertyName, column);
        }
        #endregion

    }
}
