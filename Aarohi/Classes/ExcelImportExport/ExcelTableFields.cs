using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Classes.ExcelImportExport
{
    public static class ExcelTableFields
    {
         public static string SheetName => "Tables";

        // Technical Excel identity column.
        public static string UniqueId => "Unique_ID";

        // These come from Dbhand.TableInfo
        public static string Schema => nameof(Dbhand.TableInfo.Schema);
        public static string TableName => nameof(Dbhand.TableInfo.Name);

        // Generated Excel columns
        public static string FullTableName => "FullTableName";
        public static string DisplayName => "DisplayName";

        public static string[] ExportOrder => new[]
        {
            UniqueId,
            Schema,
            TableName,
            FullTableName,
            DisplayName
        };

    }
}
