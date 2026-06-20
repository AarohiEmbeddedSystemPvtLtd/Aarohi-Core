using NCalc;
using NCalc.Domain;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Aarohi.Classes
{
    public static class UnitConverisonEngine
    {
        private sealed class CachedRule
        {
            public string ToUnit { get; init; } = string.Empty;
            public string FormulaText { get; init; } = string.Empty;
            public string Format { get; init; } = string.Empty;
            public LogicalExpression? ParsedExpression { get; init; }
            public string? FormulaError { get; init; }
        }

        private sealed class CacheSnapshot
        {
            public static readonly CacheSnapshot Empty = new();

            public DataTable? RulesTable { get; init; }
            public DataTable? ParameterMappingTable { get; init; }
            public bool RulesReady { get; init; }
            public bool ParameterMappingReady { get; init; }
            public bool HasFormatColumn { get; init; }
            public Dictionary<string, CachedRule> RulesByKey { get; init; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> FormatsByToKey { get; init; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> FormatsByFromKey { get; init; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> FromUnits { get; init; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string[]> UnitsByParameter { get; init; } = new(StringComparer.OrdinalIgnoreCase);
            public string[] Parameters { get; init; } = Array.Empty<string>();
        }

        private static readonly object CacheBuildGate = new();
        private static CacheSnapshot _cache = CacheSnapshot.Empty;

        private static string sQuantity = "Quantity";
        private static string sFrom = "FromUnit";
        private static string sTo = "ToUnit";
        private static string sFormula = "Formula";

        // Optional conversion table column.
        // If this column does not exist, default format will be used.
        private static string sFormat = "Format";
        private static string sDefaultFormat = "0.00";

        private static string sParameterCol = "Perameter";
        private static string sUnitsCol = "Units";

        public static DataTable? ConversionRules
        {
            get => Volatile.Read(ref _cache).RulesTable;
            set => Load(value, Volatile.Read(ref _cache).ParameterMappingTable);
        }

        public static DataTable? ParameterUnitMapping
        {
            get => Volatile.Read(ref _cache).ParameterMappingTable;
            set => Load(Volatile.Read(ref _cache).RulesTable, value);
        }

        public static string Quantity
        {
            get => sQuantity;
            set
            {
                sQuantity = value;
                RebuildCurrentCache();
            }
        }

        public static string From
        {
            get => sFrom;
            set
            {
                sFrom = value;
                RebuildCurrentCache();
            }
        }

        public static string To
        {
            get => sTo;
            set
            {
                sTo = value;
                RebuildCurrentCache();
            }
        }

        public static string Formula
        {
            get => sFormula;
            set
            {
                sFormula = value;
                RebuildCurrentCache();
            }
        }

        public static string Format
        {
            get => sFormat;
            set
            {
                sFormat = string.IsNullOrWhiteSpace(value) ? "Format" : value.Trim();
                RebuildCurrentCache();
            }
        }

        public static string DefaultFormat
        {
            get => sDefaultFormat;
            set
            {
                sDefaultFormat = string.IsNullOrWhiteSpace(value) ? "0.00" : value.Trim();
                RebuildCurrentCache();
            }
        }

        public static string ParameterColumnName
        {
            get => sParameterCol;
            set
            {
                sParameterCol = value;
                RebuildCurrentCache();
            }
        }

        public static string UnitsColumnName
        {
            get => sUnitsCol;
            set
            {
                sUnitsCol = value;
                RebuildCurrentCache();
            }
        }

        public static void Load(DataTable? conversionRules, DataTable? parameterUnitMapping)
        {
            CacheSnapshot replacement = BuildCache(conversionRules, parameterUnitMapping);

            lock (CacheBuildGate)
            {
                Volatile.Write(ref _cache, replacement);
            }
        }

        private static void RebuildCurrentCache()
        {
            CacheSnapshot current = Volatile.Read(ref _cache);
            Load(current.RulesTable, current.ParameterMappingTable);
        }

        public static bool HasMapping(string fromUnit)
        {
            CacheSnapshot cache = GetRulesCache();

            if (string.IsNullOrWhiteSpace(fromUnit))
                return false;

            return cache.FromUnits.Contains(Normalize(fromUnit));
        }

        public static (double value, string toUnit) convert(
            string parameter,
            object inputValue,
            string fromUnit,
            string toUnit)
        {
            var result = ConvertInternal(parameter, inputValue, fromUnit, toUnit);
            return (result.value, result.toUnit);
        }

        public static (string value, string toUnit) convertFormatted(
            string parameter,
            object inputValue,
            string fromUnit,
            string toUnit)
        {
            var result = ConvertInternal(parameter, inputValue, fromUnit, toUnit);
            string formattedValue = FormatValue(result.value, result.format);
            return (formattedValue, result.toUnit);
        }

        public static double ConvertValue(
            string quantity,
            object inputValue,
            string fromUnit,
            string toUnit)
        {
            return convert(quantity, inputValue, fromUnit, toUnit).value;
        }

        public static string ConvertValueFormatted(
            string quantity,
            object inputValue,
            string fromUnit,
            string toUnit)
        {
            return convertFormatted(quantity, inputValue, fromUnit, toUnit).value;
        }

        public static string GetNumberFormat(
            string parameter,
            string fromUnit,
            string toUnit)
        {
            CacheSnapshot cache = GetRulesCache();

            parameter = parameter?.Trim() ?? string.Empty;
            fromUnit = fromUnit?.Trim() ?? string.Empty;
            toUnit = toUnit?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(parameter))
                return sDefaultFormat;

            if (string.IsNullOrWhiteSpace(toUnit))
                return sDefaultFormat;

            return ResolveFormat(cache, parameter, fromUnit, toUnit);
        }

        public static string FormatValue(double value, string format)
        {
            string formatted = value.ToString(
                string.IsNullOrWhiteSpace(format) ? sDefaultFormat : format,
                CultureInfo.InvariantCulture);

            if (formatted.Contains(".", StringComparison.OrdinalIgnoreCase))
            {
                formatted = formatted.TrimEnd('0').TrimEnd('.');
            }

            return formatted;
        }

        private static (double value, string toUnit, string format) ConvertInternal(
            string parameter,
            object inputValue,
            string fromUnit,
            string toUnit)
        {
            CacheSnapshot cache = GetRulesCache();

            if (string.IsNullOrWhiteSpace(parameter))
                throw new ArgumentException("Parameter cannot be null or empty.", nameof(parameter));

            if (string.IsNullOrWhiteSpace(fromUnit))
                throw new ArgumentException("FromUnit cannot be null or empty.", nameof(fromUnit));

            if (string.IsNullOrWhiteSpace(toUnit))
                throw new ArgumentException("ToUnit cannot be null or empty.", nameof(toUnit));

            double value = ToDouble(inputValue);

            string selectedFormat = ResolveFormat(cache, parameter, fromUnit, toUnit);

            if (string.Equals(fromUnit.Trim(), toUnit.Trim(), StringComparison.OrdinalIgnoreCase))
                return (value, toUnit.Trim(), selectedFormat);

            if (!cache.RulesByKey.TryGetValue(GetRuleKey(parameter, fromUnit, toUnit), out CachedRule? rule))
            {
                throw new InvalidOperationException(
                    $"No conversion rule found for parameter='{parameter}', FromUnit='{fromUnit}', ToUnit='{toUnit}'.");
            }

            if (string.IsNullOrWhiteSpace(rule.FormulaText))
            {
                throw new InvalidOperationException(
                    $"Formula is empty for parameter='{parameter}', FromUnit='{fromUnit}', ToUnit='{toUnit}'.");
            }

            if (rule.ParsedExpression == null)
            {
                throw new InvalidOperationException(
                    $"Invalid conversion formula for parameter='{parameter}', FromUnit='{fromUnit}', ToUnit='{toUnit}': {rule.FormulaError ?? "Formula could not be compiled."}");
            }

            object? result = EvaluateFormula(rule.ParsedExpression, value);
            double convertedValue = ToDouble(result!);

            return (convertedValue, rule.ToUnit, rule.Format);
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

            CacheSnapshot cache = GetParameterMappingCache();
            return cache.UnitsByParameter.TryGetValue(Normalize(parameter), out string[]? units)
                ? new List<string>(units)
                : new List<string>();
        }

        public static string[] GetAllParameters()
        {
            CacheSnapshot cache = GetParameterMappingCache();
            var list = new List<string>(cache.Parameters);

            list.Insert(0, "--Select--");
            return list.ToArray();
        }

        private static CacheSnapshot GetRulesCache()
        {
            CacheSnapshot cache = Volatile.Read(ref _cache);
            DataTable? rules = cache.RulesTable;

            if (rules == null)
                throw new InvalidOperationException("ConversionRules DataTable is null. Set UnitConverisonEngine.ConversionRules first.");

            if (cache.RulesReady)
                return cache;

            if (!rules.Columns.Contains(sQuantity))
                throw new InvalidOperationException($"Missing column '{sQuantity}' in ConversionRules.");

            if (!rules.Columns.Contains(sFrom))
                throw new InvalidOperationException($"Missing column '{sFrom}' in ConversionRules.");

            if (!rules.Columns.Contains(sTo))
                throw new InvalidOperationException($"Missing column '{sTo}' in ConversionRules.");

            if (!rules.Columns.Contains(sFormula))
                throw new InvalidOperationException($"Missing column '{sFormula}' in ConversionRules.");

            return cache;
        }

        private static CacheSnapshot GetParameterMappingCache()
        {
            CacheSnapshot cache = Volatile.Read(ref _cache);
            DataTable? mapping = cache.ParameterMappingTable;

            if (mapping == null)
                throw new InvalidOperationException("ParameterUnitMapping DataTable is null. Set UnitConverisonEngine.ParameterUnitMapping first.");

            if (cache.ParameterMappingReady)
                return cache;

            if (!mapping.Columns.Contains(sParameterCol))
                throw new InvalidOperationException($"Missing column '{sParameterCol}' in ParameterUnitMapping.");

            if (!mapping.Columns.Contains(sUnitsCol))
                throw new InvalidOperationException($"Missing column '{sUnitsCol}' in ParameterUnitMapping.");

            return cache;
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

        private static object? EvaluateFormula(LogicalExpression parsedExpression, double v)
        {
            var exp = new Expression(parsedExpression, EvaluateOptions.IgnoreCase);
            exp.Parameters["v"] = v;

            object? result = exp.Evaluate();

            if (result is decimal dec)
                return (double)dec;

            return result;
        }

        private static CacheSnapshot BuildCache(DataTable? rules, DataTable? parameterMapping)
        {
            var rulesByKey = new Dictionary<string, CachedRule>(StringComparer.OrdinalIgnoreCase);
            var formatsByToKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var formatsByFromKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var fromUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (HasRuleColumns(rules))
            {
                foreach (DataRow row in rules!.Rows)
                {
                    string quantity = ReadCell(row, sQuantity);
                    string fromUnit = ReadCell(row, sFrom);
                    string toUnit = ReadCell(row, sTo);

                    if (string.IsNullOrWhiteSpace(quantity) || string.IsNullOrWhiteSpace(fromUnit) || string.IsNullOrWhiteSpace(toUnit))
                        continue;

                    string formulaText = ReadCell(row, sFormula);
                    string format = rules.Columns.Contains(sFormat) ? ReadCell(row, sFormat) : string.Empty;
                    format = string.IsNullOrWhiteSpace(format) ? sDefaultFormat : format;

                    LogicalExpression? parsedExpression = null;
                    string? formulaError = null;

                    if (!string.IsNullOrWhiteSpace(formulaText))
                    {
                        try
                        {
                            parsedExpression = Expression.Compile(formulaText, false);
                        }
                        catch (Exception ex)
                        {
                            formulaError = ex.Message;
                        }
                    }

                    var cachedRule = new CachedRule
                    {
                        ToUnit = toUnit,
                        FormulaText = formulaText,
                        Format = format,
                        ParsedExpression = parsedExpression,
                        FormulaError = formulaError
                    };

                    rulesByKey.TryAdd(GetRuleKey(quantity, fromUnit, toUnit), cachedRule);
                    formatsByToKey.TryAdd(GetPairKey(quantity, toUnit), format);
                    formatsByFromKey.TryAdd(GetPairKey(quantity, fromUnit), format);
                    fromUnits.Add(Normalize(fromUnit));
                }
            }

            var unitsByParameter = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            var parameterDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (HasParameterMappingColumns(parameterMapping))
            {
                foreach (DataRow row in parameterMapping!.Rows)
                {
                    string parameter = ReadCell(row, sParameterCol);
                    if (string.IsNullOrWhiteSpace(parameter))
                        continue;

                    string normalizedParameter = Normalize(parameter);
                    parameterDisplayNames.TryAdd(normalizedParameter, parameter);

                    IEnumerable<string> currentUnits = unitsByParameter.TryGetValue(normalizedParameter, out string[]? existing)
                        ? existing
                        : Array.Empty<string>();
                    string[] combinedUnits = currentUnits
                        .Concat(ReadCell(row, sUnitsCol).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        .Select(unit => unit.Trim())
                        .Where(unit => !string.IsNullOrWhiteSpace(unit))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    unitsByParameter[normalizedParameter] = combinedUnits;
                }
            }

            return new CacheSnapshot
            {
                RulesTable = rules,
                ParameterMappingTable = parameterMapping,
                RulesReady = HasRuleColumns(rules),
                ParameterMappingReady = HasParameterMappingColumns(parameterMapping),
                HasFormatColumn = rules?.Columns.Contains(sFormat) == true,
                RulesByKey = rulesByKey,
                FormatsByToKey = formatsByToKey,
                FormatsByFromKey = formatsByFromKey,
                FromUnits = fromUnits,
                UnitsByParameter = unitsByParameter,
                Parameters = parameterDisplayNames.Values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }

        private static string ResolveFormat(CacheSnapshot cache, string parameter, string fromUnit, string toUnit)
        {
            if (!cache.HasFormatColumn)
                return sDefaultFormat;

            if (!string.IsNullOrWhiteSpace(fromUnit) &&
                cache.RulesByKey.TryGetValue(GetRuleKey(parameter, fromUnit, toUnit), out CachedRule? exactRule))
            {
                return exactRule.Format;
            }

            if (cache.FormatsByToKey.TryGetValue(GetPairKey(parameter, toUnit), out string? toFormat))
                return toFormat;

            return cache.FormatsByFromKey.TryGetValue(GetPairKey(parameter, toUnit), out string? fromFormat)
                ? fromFormat
                : sDefaultFormat;
        }

        private static bool HasRuleColumns(DataTable? table)
        {
            return table != null && table.Columns.Contains(sQuantity) && table.Columns.Contains(sFrom) &&
                   table.Columns.Contains(sTo) && table.Columns.Contains(sFormula);
        }

        private static bool HasParameterMappingColumns(DataTable? table)
        {
            return table != null && table.Columns.Contains(sParameterCol) && table.Columns.Contains(sUnitsCol);
        }

        private static string ReadCell(DataRow row, string columnName)
        {
            return row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
                ? Convert.ToString(row[columnName])?.Trim() ?? string.Empty
                : string.Empty;
        }

        private static string GetRuleKey(string quantity, string fromUnit, string toUnit)
        {
            return $"{Normalize(quantity)}\u001f{Normalize(fromUnit)}\u001f{Normalize(toUnit)}";
        }

        private static string GetPairKey(string quantity, string unit)
        {
            return $"{Normalize(quantity)}\u001f{Normalize(unit)}";
        }

        private static string Normalize(string value)
        {
            return value?.Trim().ToUpperInvariant() ?? string.Empty;
        }
    }
}
