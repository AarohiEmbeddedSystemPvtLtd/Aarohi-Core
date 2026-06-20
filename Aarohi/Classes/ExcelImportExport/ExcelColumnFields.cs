using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aarohi.Classes.ExcelImportExport
{
    public class ExcelColumnFields
    {
        public static string Name => nameof(DynamicClass.ColumnInfo.Name);
        public static string DataType => nameof(DynamicClass.ColumnInfo.DataType);
        public static string Precision => nameof(DynamicClass.ColumnInfo.Precision);
        public static string Scale => nameof(DynamicClass.ColumnInfo.Scale);

        public static string Options => nameof(DynamicClass.ColumnInfo.Options);
        public static string DefaultValue => nameof(DynamicClass.ColumnInfo.DefaultValue);

        public static string DisplayName => nameof(DynamicClass.ColumnInfo.DisplayName);
        public static string Description => nameof(DynamicClass.ColumnInfo.Description);

        public static string Unit => nameof(DynamicClass.ColumnInfo.Unit);
        public static string DefaultUnit => nameof(DynamicClass.ColumnInfo.DefaultUnit);
        public static string InputUnit => nameof(DynamicClass.ColumnInfo.InputUnit);
        public static string LastUsedUnit => nameof(DynamicClass.ColumnInfo.LastUsedUnit);
        public static string ShowUnit => nameof(DynamicClass.ColumnInfo.ShowUnit);
        public static string ReportUnit => nameof(DynamicClass.ColumnInfo.ReportUnit);

        public static string DatagridShow => nameof(DynamicClass.ColumnInfo.DatagridShow);
        public static string HideInCrudForm => nameof(DynamicClass.ColumnInfo.HideInCrudForm);

        public static string Format => nameof(DynamicClass.ColumnInfo.Format);
        public static string Parameter => nameof(DynamicClass.ColumnInfo.Parameter);
        public static string Order => nameof(DynamicClass.ColumnInfo.Order);
        public static string Visible => nameof(DynamicClass.ColumnInfo.Visible);

        public static string[] ExportOrder => new[]
        {
            Name,
            DataType,
            Precision,
            Scale,

            Options,
            DefaultValue,

            DisplayName,
            Description,

            Unit,
            DefaultUnit,
            InputUnit,
            LastUsedUnit,
            ShowUnit,
            ReportUnit,

            DatagridShow,
            HideInCrudForm,

            Format,
            Parameter,
            Order,
            Visible
        };

        public static (string ColumnName, int Min, int Max)[] WholeNumberColumns => new[]
        {
            (Precision, 0, 38),
            (Scale, 0, 38),
            (Order, 0, 999999)
        };

        public static string[] DefaultBooleanColumns => new[]
        {
            DatagridShow,
            HideInCrudForm,
            Visible
        };

        public static string[] TextColumns => new[]
        {
            Name,
            DisplayName,
            Description,
            Format
        };

        public static string[] UnitColumns => new[]
        {
            ShowUnit,
            DefaultUnit,
            InputUnit,
            LastUsedUnit,
            ReportUnit
        };

        public static string[] LockedColumns => new[]
        {
            Name,
            DataType,
            Precision,
            Scale,
            Unit
        };

        public static string[] SkipImportColumns => new[]
        {
            Name,
            DataType,
            Precision,
            Scale,
            Options,
            DefaultValue
        };

    }
}
