using NCalc;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace Aarohi.Classes
{
    public static class UnitConverisonEngine
    {
        private static DataTable? dtRules;
        private static DataTable? dtParameterUnitMapping;

        private static string sQuantity = "Quantity";
        private static string sFrom = "FromUnit";
        private static string sTo = "ToUnit";
        private static string sFormula = "Formula";

        private static string sParameterCol = "Perameter";
        private static string sUnitsCol = "Units";

        public static DataTable? ConversionRules
        {
            get => dtRules;
            set => dtRules = value;
        }

        public static DataTable? ParameterUnitMapping
        {
            get => dtParameterUnitMapping;
            set => dtParameterUnitMapping = value;
        }

        public static string Quantity
        {
            get => sQuantity;
            set => sQuantity = value;
        }

        public static string From
        {
            get => sFrom;
            set => sFrom = value;
        }

        public static string To
        {
            get => sTo;
            set => sTo = value;
        }

        public static string Formula
        {
            get => sFormula;
            set => sFormula = value;
        }

        public static string ParameterColumnName
        {
            get => sParameterCol;
            set => sParameterCol = value;
        }

        public static string UnitsColumnName
        {
            get => sUnitsCol;
            set => sUnitsCol = value;
        }

        public static bool HasMapping(string fromUnit)
        {
            EnsureRulesReady();

            if (string.IsNullOrWhiteSpace(fromUnit))
                return false;

            return dtRules!.AsEnumerable()
                .Any(r => StrEq(r[sFrom], fromUnit));
        }

        public static (double value, string toUnit) convert(
            string parameter,
            object inputValue,
            string fromUnit,
            string toUnit)
        {
            EnsureRulesReady();

            if (string.IsNullOrWhiteSpace(parameter))
                throw new ArgumentException("Parameter cannot be null or empty.", nameof(parameter));

            if (string.IsNullOrWhiteSpace(fromUnit))
                throw new ArgumentException("FromUnit cannot be null or empty.", nameof(fromUnit));

            if (string.IsNullOrWhiteSpace(toUnit))
                throw new ArgumentException("ToUnit cannot be null or empty.", nameof(toUnit));

            double value = ToDouble(inputValue);

            if (string.Equals(fromUnit.Trim(), toUnit.Trim(), StringComparison.OrdinalIgnoreCase))
                return (value, toUnit.Trim());

            DataRow? rule = dtRules!.AsEnumerable().FirstOrDefault(r =>
                StrEq(r[sQuantity], parameter) &&
                StrEq(r[sFrom], fromUnit) &&
                StrEq(r[sTo], toUnit));

            if (rule == null)
            {
                throw new InvalidOperationException(
                    $"No conversion rule found for parameter='{parameter}', FromUnit='{fromUnit}', ToUnit='{toUnit}'.");
            }

            string formulaText = Convert.ToString(rule[sFormula])?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(formulaText))
            {
                throw new InvalidOperationException(
                    $"Formula is empty for parameter='{parameter}', FromUnit='{fromUnit}', ToUnit='{toUnit}'.");
            }

            object? result = EvaluateFormula(formulaText, value);
            double convertedValue = ToDouble(result!);

            return (convertedValue, toUnit.Trim());
        }

        public static double ConvertValue(
            string quantity,
            object inputValue,
            string fromUnit,
            string toUnit)
        {
            return convert(quantity, inputValue, fromUnit, toUnit).value;
        }

        public static string GetUnitFromParameter(string parameter)
        {
            var units = GetUnitsFromParameter(parameter);
            return units.Count > 0 ? units[0] : string.Empty;
        }

        public static List<string> GetUnitsFromParameter(string parameter)
        {
            if (string.IsNullOrWhiteSpace(parameter))
                return new List<string>();

            EnsureParameterMappingReady();

            string key = parameter.Trim();

            return dtParameterUnitMapping!.AsEnumerable()
                .Where(r => StrEq(r[sParameterCol], key))
                .Select(r => Convert.ToString(r[sUnitsCol]))
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .SelectMany(u => u!
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string[] GetAllParameters()
        {
            EnsureParameterMappingReady();

            var list = dtParameterUnitMapping!.AsEnumerable()
                .Select(r => Convert.ToString(r[sParameterCol])?.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            list.Insert(0, "--Select--");
            return list.ToArray();
        }

        private static void EnsureRulesReady()
        {
            if (dtRules == null)
                throw new InvalidOperationException("ConversionRules DataTable is null. Set UnitConverisonEngine.ConversionRules first.");

            if (!dtRules.Columns.Contains(sQuantity))
                throw new InvalidOperationException($"Missing column '{sQuantity}' in ConversionRules.");

            if (!dtRules.Columns.Contains(sFrom))
                throw new InvalidOperationException($"Missing column '{sFrom}' in ConversionRules.");

            if (!dtRules.Columns.Contains(sTo))
                throw new InvalidOperationException($"Missing column '{sTo}' in ConversionRules.");

            if (!dtRules.Columns.Contains(sFormula))
                throw new InvalidOperationException($"Missing column '{sFormula}' in ConversionRules.");
        }

        private static void EnsureParameterMappingReady()
        {
            if (dtParameterUnitMapping == null)
                throw new InvalidOperationException("ParameterUnitMapping DataTable is null. Set UnitConverisonEngine.ParameterUnitMapping first.");

            if (!dtParameterUnitMapping.Columns.Contains(sParameterCol))
                throw new InvalidOperationException($"Missing column '{sParameterCol}' in ParameterUnitMapping.");

            if (!dtParameterUnitMapping.Columns.Contains(sUnitsCol))
                throw new InvalidOperationException($"Missing column '{sUnitsCol}' in ParameterUnitMapping.");
        }

        private static bool StrEq(object? a, string b)
        {
            string sa = Convert.ToString(a)?.Trim() ?? string.Empty;
            string sb = b?.Trim() ?? string.Empty;
            return sa.Equals(sb, StringComparison.OrdinalIgnoreCase);
        }

        private static double ToDouble(object v)
        {
            if (v == null || v == DBNull.Value)
                throw new ArgumentNullException(nameof(v), "Input value is null.");

            if (v is double d) return d;
            if (v is float f) return f;
            if (v is decimal m) return (double)m;
            if (v is int i) return i;
            if (v is long l) return l;
            if (v is short s) return s;
            if (v is byte b) return b;

            if (double.TryParse(Convert.ToString(v), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                return parsed;

            return Convert.ToDouble(v, CultureInfo.InvariantCulture);
        }

        private static object? EvaluateFormula(string formulaText, double v)
        {
            var exp = new Expression(formulaText, EvaluateOptions.IgnoreCase);
            exp.Parameters["v"] = v;

            object? result = exp.Evaluate();

            if (result is decimal dec)
                return (double)dec;

            return result;
        }
    }
}