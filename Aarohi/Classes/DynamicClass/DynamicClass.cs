using Aarohi.Core.Logger;
using Aarohi.Globals;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
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

        // ---------- Core properties ----------
        public string Schema { get; set; } = "dbo";
        public string Table { get; set; } = "";
        public string KeyColumn { get; set; } = "";
        public string LogSource { get; set; } = "DynamicClass";

        private Func<SqlConnection>? _instanceFactory;

        public Func<SqlConnection>? InstanceConnectionFactory
        {
            get => Volatile.Read(ref _instanceFactory);
            set
            {
                if (value is null) throw new ArgumentNullException(nameof(InstanceConnectionFactory));
                if (Interlocked.CompareExchange(ref _instanceFactory, value, null) != null)
                    throw new InvalidOperationException("InstanceConnectionFactory already set for this instance.");
            }
        }

        private static Func<SqlConnection>? _factory;
        public static Func<SqlConnection>? ConnectionFactory
        {
            get => Volatile.Read(ref _factory) ?? throw new InvalidOperationException("DefaultConnectionFactory is not set.");
            set
            {
                if (value is null) throw new ArgumentNullException(nameof(ConnectionFactory));
                if (Interlocked.CompareExchange(ref _factory, value, null) != null)
                    throw new InvalidOperationException("InstanceConnectionFactory already set for this instance.");
            }
        }

        private Func<SqlConnection> ResolveFactory()
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
        public DynamicClass() { }

        /// <summary>
        /// Initializes a new <see cref="DynamicClass"/> targeting a specific table.
        /// If <paramref name="keyColumn"/> is null or empty, attempts to auto-detect
        /// the key column (PK preferred; falls back to identity if enabled).
        /// </summary>
        /// <param name="schema">Database schema name (e.g., "dbo").</param>
        /// <param name="table">Database table name.</param>
        /// <param name="keyColumn">Optional key column name.</param>
        public DynamicClass(string schema, string table, string? keyColumn = null)
        {
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
                    Func<SqlConnection> connectionFactory,
                    string? keyColumn = null)
        {
            if (connectionFactory == null)
                throw new ArgumentNullException(nameof(connectionFactory));

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

        //private string GetColumnMetadataCacheKey()
        //{
        //    EnsureIdent(Schema);
        //    EnsureIdent(Table);
        //    return $"{Schema}.{Table}";
        //}
        private string GetColumnMetadataCacheKey()
        {
            EnsureIdent(Schema);
            EnsureIdent(Table);

            Func<SqlConnection> factory = ResolveFactory();

            using SqlConnection con = factory();

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(con.ConnectionString);

            string server = builder.DataSource?.Trim() ?? "";
            string database = builder.InitialCatalog?.Trim() ?? "";

            return $"{server}|{database}|{Schema}.{Table}";
        }

        private void InvalidateColumnMetadataCache()
        {
            ColumnMetadataCache.TryRemove(GetColumnMetadataCacheKey(), out _);
        }
        #endregion

    }
}
