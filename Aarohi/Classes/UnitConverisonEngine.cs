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
        private static DataTable? dtMapping;
        private static DataTable? dtRules;

        // Parameter -> Unit mapping table
        private static DataTable? dtParameterUnitMapping;

        private static string sQuantity = "Quantity";
        private static string sFrom = "FromUnit";
        private static string sTo = "ToUnit";
        private static string sFormula = "Formula";

        // NEW: column names for parameter mapping table
        private static string sParameterCol = "Perameter"; // keep your spelling as-is
        private static string sUnitsCol = "Units";

        public static DataTable? ConversionMapping
        {
            get => dtMapping;
            set => dtMapping = value;
        }

        public static DataTable? ConversionRules
        {
            get => dtRules;
            set => dtRules = value;
        }

        // NEW: Parameter mapping table property
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

        // NEW: column name props for parameter mapping
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

        public static bool hasMapping(string fromUnit)
        {
            if (dtRules == null) return false;

            return dtRules.AsEnumerable()
                .Any(v => string.Equals(v[sFrom]?.ToString()?.Trim(), fromUnit, StringComparison.OrdinalIgnoreCase));
        }

        public static (double value, string toUnit) convert(
            string quantityValue,
            object inputValue,
            string fromUnit,
            string toUnit
        )
        {
            EnsureRulesReady();

            double v = ToDouble(inputValue);

            var rule = dtRules!.AsEnumerable().FirstOrDefault(r =>
                StrEq(r[sQuantity], quantityValue) &&
                StrEq(r[sFrom], fromUnit) &&
                StrEq(r[sTo], toUnit)
            );

            if (rule == null)
                throw new InvalidOperationException(
                    $"No rule found for {sQuantity}='{quantityValue}' {sFrom}='{fromUnit}' -> {sTo}='{toUnit}'."
                );

            string formulaText = Convert.ToString(rule[sFormula])?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(formulaText))
                throw new InvalidOperationException(
                    $"Formula is empty for {sQuantity}='{quantityValue}', {sFrom}='{fromUnit}', {sTo}='{toUnit}'."
                );

            object? result = EvaluateFormula(formulaText, v);
            double outVal = ToDouble(result!);

            return (outVal, toUnit);
        }

        public static (object? value, string toUnit) convert(string quantityValue, object inputValue, string? desiredToUnit = null)
        {
            EnsureMappingReady();

            double v = ToDouble(inputValue);

            var rows = dtMapping!.AsEnumerable()
                .Where(r => StrEq(r[sQuantity], quantityValue))
                .ToList();

            if (rows.Count == 0)
                throw new InvalidOperationException($"No conversion rule found for {sQuantity}='{quantityValue}'.");

            DataRow rule;

            if (!string.IsNullOrWhiteSpace(desiredToUnit))
            {
                rule = rows.FirstOrDefault(r => StrEq(r[sTo], desiredToUnit!))
                       ?? throw new InvalidOperationException(
                           $"No rule found for {sQuantity}='{quantityValue}' with {sTo}='{desiredToUnit}'.");
            }
            else
            {
                if (rows.Count > 1)
                {
                    var options = string.Join(", ",
                        rows.Select(r => Convert.ToString(r[sTo])?.Trim())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.OrdinalIgnoreCase));

                    throw new InvalidOperationException(
                        $"Multiple rules found for {sQuantity}='{quantityValue}'. " +
                        $"Specify desiredToUnit. Options: {options}");
                }

                rule = rows[0];
            }

            string toUnit = Convert.ToString(rule[sTo])?.Trim() ?? "";
            string formulaText = Convert.ToString(rule[sFormula])?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(formulaText))
                throw new InvalidOperationException($"Formula is empty for {sQuantity}='{quantityValue}', {sTo}='{toUnit}'.");

            object? result = EvaluateFormula(formulaText, v);

            return (result, toUnit);
        }

        // ✅ NEW: get unit from parameter mapping table
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

        private static void EnsureMappingReady()
        {
            if (dtMapping == null)
                throw new InvalidOperationException("ConversionMapping DataTable is null. Set UnitConverisonEngine.ConversionMapping first.");

            if (!dtMapping.Columns.Contains(sQuantity)) throw new InvalidOperationException($"Missing column '{sQuantity}' in ConversionMapping.");
            if (!dtMapping.Columns.Contains(sFrom)) throw new InvalidOperationException($"Missing column '{sFrom}' in ConversionMapping.");
            if (!dtMapping.Columns.Contains(sTo)) throw new InvalidOperationException($"Missing column '{sTo}' in ConversionMapping.");
            if (!dtMapping.Columns.Contains(sFormula)) throw new InvalidOperationException($"Missing column '{sFormula}' in ConversionMapping.");
        }

        private static void EnsureRulesReady()
        {
            if (dtRules == null)
                throw new InvalidOperationException("ConversionRules DataTable is null. Set UnitConverisonEngine.ConversionRules first.");

            if (!dtRules.Columns.Contains(sQuantity)) throw new InvalidOperationException($"Missing column '{sQuantity}' in ConversionRules.");
            if (!dtRules.Columns.Contains(sFrom)) throw new InvalidOperationException($"Missing column '{sFrom}' in ConversionRules.");
            if (!dtRules.Columns.Contains(sTo)) throw new InvalidOperationException($"Missing column '{sTo}' in ConversionRules.");
            if (!dtRules.Columns.Contains(sFormula)) throw new InvalidOperationException($"Missing column '{sFormula}' in ConversionRules.");
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
            string sa = Convert.ToString(a)?.Trim() ?? "";
            return sa.Equals(b?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);
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

            if (double.TryParse(Convert.ToString(v), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                return parsed;

            return Convert.ToDouble(v, CultureInfo.InvariantCulture);
        }

        private static object? EvaluateFormula(string formulaText, double v)
        {
            var exp = new Expression(formulaText, EvaluateOptions.IgnoreCase);
            exp.Parameters["v"] = v;

            object? res = exp.Evaluate();

            if (res is decimal dec) return (double)dec;
            return res;
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

            // Always first item
            list.Insert(0, "--Select--");

            return list.ToArray();
        }
    }
}