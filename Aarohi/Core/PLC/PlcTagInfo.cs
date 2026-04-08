using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Core.PLC
{
    public sealed class PlcTagInfo
    {
        public string DbName { get; init; } = "";
        public int DbNumber { get; init; }
        public string SheetName { get; init; } = "";

        public string Name { get; init; } = "";
        public string DataType { get; init; } = "";
        public string OffsetRaw { get; init; } = "";

        public string FullAddress { get; init; } = "";
        public int ByteLength { get; init; }
        public string? Warning { get; init; }
    }

}
