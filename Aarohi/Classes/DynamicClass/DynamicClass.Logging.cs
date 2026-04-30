using Aarohi.Core.Logger;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Data.SqlClient;
using System;
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
    public sealed partial class DynamicClass
    {

        #region logger 

        // ---------- Logger injection (set once at startup) ----------
        private static Action<LogLevel, string, string, Exception?, Dictionary<string, object>?>? _logSink;

        public static Action<LogLevel, string, string, Exception?, Dictionary<string, object>?>? LogSink
        {
            get => Volatile.Read(ref _logSink);
            set
            {
                if (value is null) throw new ArgumentNullException(nameof(LogSink));
                if (Interlocked.CompareExchange(ref _logSink, value, null) != null)
                    throw new InvalidOperationException("LogSink already set. Set it once at startup.");
            }
        }

        private static void WriteLog(LogLevel level, string message, string source, Exception? ex, Dictionary<string, object> extras)
        {
            var sink = Volatile.Read(ref _logSink);
            if (sink != null)
            {
                sink(level, message, source, ex, extras);
                return;
            }

            // fallback (still works even if LogSink is not set)
            Aarohi.Core.Logger._logger.Log(level, message, source, ex, extras);
        }

        #endregion

    }
}
