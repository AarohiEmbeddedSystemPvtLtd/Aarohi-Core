using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Core
{
    internal class Exceptions
    {
        public sealed class ForeignKeyDeleteBlockedException : Exception
        {
            public string? Constraint { get; }
            public string? RefTable { get; }
            public string? RefColumn { get; }
            public ForeignKeyDeleteBlockedException(string message, string? constraint, string? refTable, string? refColumn)
                : base(message) { Constraint = constraint; RefTable = refTable; RefColumn = refColumn; }
        }

    }
}
