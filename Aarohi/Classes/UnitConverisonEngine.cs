using NCalc;
using NCalc.Domain;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Aarohi.Classes
{
    public static class UnitConverisonEngine
    {
        private sealed class CachedRule
        {
            public string ToUnit { get; init; } = string.Empty;
            public string FormulaText { get; init; } = string.Empty;
            public string NumericFormulaText { get; init; } = string.Empty;
            public string Format { get; init; } = string.Empty;
            public Func<double, double>? FastFormula { get; init; }
            public LogicalExpression? ParsedExpression { get; init; }
            public (string Name, LogicalExpression Expression, Dictionary<string, string> Parameters)[] TemplateParts { get; init; }
                = Array.Empty<(string Name, LogicalExpression Expression, Dictionary<string, string> Parameters)>();
            public string? NumericFormulaError { get; init; }
            public string? TemplateFormulaError { get; init; }
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
            CacheSnapshot cache = GetRulesCache();

            if (string.IsNullOrWhiteSpace(parameter))
                throw new ArgumentException("Parameter cannot be null or empty.", nameof(parameter));

            if (string.IsNullOrWhiteSpace(fromUnit))
                throw new ArgumentException("FromUnit cannot be null or empty.", nameof(fromUnit));

            if (string.IsNullOrWhiteSpace(toUnit))
                throw new ArgumentException("ToUnit cannot be null or empty.", nameof(toUnit));

            if (!string.Equals(fromUnit.Trim(), toUnit.Trim(), StringComparison.OrdinalIgnoreCase) &&
                cache.RulesByKey.TryGetValue(GetRuleKey(parameter, fromUnit, toUnit), out CachedRule? rule))
            {
                string format = ResolveFormat(cache, parameter, fromUnit, toUnit);

                if (rule.TemplateParts.Length > 0 && Regex.IsMatch(format, @"\{[^{}]+\}"))
                {
                    if (!string.IsNullOrWhiteSpace(rule.TemplateFormulaError))
                    {
                        throw new InvalidOperationException(
                            $"Invalid template formula for parameter='{parameter}', FromUnit='{fromUnit}', ToUnit='{toUnit}': {rule.TemplateFormulaError}");
                    }

                    var values = EvaluateTemplate(rule, ToDouble(inputValue));
                    return (ApplyTemplate(format, values), rule.ToUnit);
                }
            }

            var result = ConvertInternal(parameter, inputValue, fromUnit, toUnit);
            return (FormatValue(result.value, result.format), result.toUnit);
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

            if (string.IsNullOrWhiteSpace(rule.NumericFormulaText))
            {
                throw new InvalidOperationException(
                    $"Numeric conversion expression is missing for parameter='{parameter}', FromUnit='{fromUnit}', ToUnit='{toUnit}'. " +
                    "When template variables are used, keep the numeric expression first, for example: v/60000;{a}=...;{b}=...");
            }

            if (rule.ParsedExpression == null || !string.IsNullOrWhiteSpace(rule.NumericFormulaError))
            {
                throw new InvalidOperationException(
                    $"Invalid numeric conversion formula for parameter='{parameter}', FromUnit='{fromUnit}', ToUnit='{toUnit}': " +
                    $"{rule.NumericFormulaError ?? "Formula could not be compiled."}");
            }

            if (rule.FastFormula != null)
                return (rule.FastFormula(value), rule.ToUnit, rule.Format);

            object? result = EvaluateFormula(rule.ParsedExpression, value);
<<<<<<< Updated upstream
            double convertedValue = ToDouble(result!);

            return (convertedValue, rule.ToUnit, rule.Format);
=======
            return (ToDouble(result!), rule.ToUnit, selectedFormat);
>>>>>>> Stashed changes
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

        private static Dictionary<string, double> EvaluateTemplate(CachedRule rule, double inputValue)
        {
            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["v"] = inputValue
            };

            foreach (var part in rule.TemplateParts)
            {
                var exp = new Expression(part.Expression, EvaluateOptions.IgnoreCase);
                exp.Parameters["v"] = inputValue;

                foreach (var parameter in part.Parameters)
                {
                    if (!values.TryGetValue(parameter.Value, out double tokenValue))
                    {
                        throw new InvalidOperationException(
                            $"Template variable '{{{parameter.Value}}}' must be calculated before '{{{part.Name}}}'.");
                    }

                    exp.Parameters[parameter.Key] = tokenValue;
                }

                values[part.Name] = ToDouble(exp.Evaluate()!);
            }

            return values;
        }

        private static string ApplyTemplate(string format, IReadOnlyDictionary<string, double> values)
        {
            return Regex.Replace(
                format,
                @"\{(?<name>[^{}:]+)(?::(?<format>[^{}]+))?\}",
                match =>
                {
                    string name = match.Groups["name"].Value.Trim();

                    if (!values.TryGetValue(name, out double value))
                        throw new InvalidOperationException($"Unknown template token '{{{name}}}'.");

                    string numberFormat = match.Groups["format"].Success
                        ? match.Groups["format"].Value
                        : "0.########";

                    return value.ToString(numberFormat, CultureInfo.InvariantCulture);
                });
        }

        private static Func<double, double>? TryCreateFastFormula(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
                return null;

            var parser = new FastFormulaParser(formula);
            return parser.TryParse(out Func<double, double>? compiled) ? compiled : null;
        }

        private sealed class FastFormulaParser
        {
            private readonly string _text;
            private int _pos;

            public FastFormulaParser(string text)
            {
                _text = text;
            }

            public bool TryParse(out Func<double, double>? compiled)
            {
                compiled = null;
                try
                {
                    Func<double, double> expression = ParseExpression();
                    SkipWhiteSpace();
                    if (_pos != _text.Length)
                        return false;

                    compiled = expression;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private Func<double, double> ParseExpression()
            {
                Func<double, double> left = ParseTerm();
                while (true)
                {
                    SkipWhiteSpace();
                    if (Match('+'))
                    {
                        Func<double, double> right = ParseTerm();
                        Func<double, double> currentLeft = left;
                        left = v => currentLeft(v) + right(v);
                    }
                    else if (Match('-'))
                    {
                        Func<double, double> right = ParseTerm();
                        Func<double, double> currentLeft = left;
                        left = v => currentLeft(v) - right(v);
                    }
                    else
                    {
                        return left;
                    }
                }
            }

            private Func<double, double> ParseTerm()
            {
                Func<double, double> left = ParseFactor();
                while (true)
                {
                    SkipWhiteSpace();
                    if (Match('*'))
                    {
                        Func<double, double> right = ParseFactor();
                        Func<double, double> currentLeft = left;
                        left = v => currentLeft(v) * right(v);
                    }
                    else if (Match('/'))
                    {
                        Func<double, double> right = ParseFactor();
                        Func<double, double> currentLeft = left;
                        left = v => currentLeft(v) / right(v);
                    }
                    else
                    {
                        return left;
                    }
                }
            }

            private Func<double, double> ParseFactor()
            {
                SkipWhiteSpace();

                if (Match('+'))
                    return ParseFactor();

                if (Match('-'))
                {
                    Func<double, double> factor = ParseFactor();
                    return v => -factor(v);
                }

                if (Match('('))
                {
                    Func<double, double> expression = ParseExpression();
                    if (!Match(')'))
                        throw new FormatException();

                    return expression;
                }

                if (_pos < _text.Length && char.ToLowerInvariant(_text[_pos]) == 'v')
                {
                    _pos++;
                    return v => v;
                }

                double number = ParseNumber();
                return _ => number;
            }

            private double ParseNumber()
            {
                SkipWhiteSpace();
                int start = _pos;

                while (_pos < _text.Length &&
                       (char.IsDigit(_text[_pos]) || _text[_pos] == '.' || _text[_pos] == 'e' || _text[_pos] == 'E' ||
                        (_pos > start && (_text[_pos] == '+' || _text[_pos] == '-') && (_text[_pos - 1] == 'e' || _text[_pos - 1] == 'E'))))
                {
                    _pos++;
                }

                if (start == _pos)
                    throw new FormatException();

                return double.Parse(_text[start.._pos], NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            private bool Match(char c)
            {
                SkipWhiteSpace();
                if (_pos >= _text.Length || _text[_pos] != c)
                    return false;

                _pos++;
                return true;
            }

            private void SkipWhiteSpace()
            {
                while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
                    _pos++;
            }
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

                    if (string.IsNullOrWhiteSpace(quantity) ||
                        string.IsNullOrWhiteSpace(fromUnit) ||
                        string.IsNullOrWhiteSpace(toUnit))
                    {
                        continue;
                    }

                    string formulaText = ReadCell(row, sFormula);
                    string format = rules.Columns.Contains(sFormat) ? ReadCell(row, sFormat) : string.Empty;
                    format = string.IsNullOrWhiteSpace(format) ? sDefaultFormat : format;

                    string numericFormulaText = string.Empty;
                    LogicalExpression? parsedExpression = null;
                    string? numericFormulaError = null;
                    string? templateFormulaError = null;

                    var templateParts =
                        new List<(string Name, LogicalExpression Expression, Dictionary<string, string> Parameters)>();

                    if (!string.IsNullOrWhiteSpace(formulaText))
                    {
                        var templateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (string part in formulaText.Split(
                                     ';',
                                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            Match assignment = Regex.Match(
                                part,
                                @"^\s*\{(?<name>[^{}:=;]+)\}\s*=\s*(?<expression>.+?)\s*$");

                            if (assignment.Success)
                            {
                                string name = assignment.Groups["name"].Value.Trim();

                                if (string.Equals(name, "v", StringComparison.OrdinalIgnoreCase))
                                {
                                    templateFormulaError ??=
                                        "'v' is reserved for the input value and cannot be a template variable name.";
                                    continue;
                                }

                                if (!templateNames.Add(name))
                                {
                                    templateFormulaError ??= $"Duplicate template variable '{{{name}}}'.";
                                    continue;
                                }

                                try
                                {
                                    var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                    var safeNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                                    string expressionText = Regex.Replace(
                                        assignment.Groups["expression"].Value,
                                        @"\{(?<name>[^{}:]+)\}",
                                        token =>
                                        {
                                            string tokenName = token.Groups["name"].Value.Trim();

                                            if (!safeNames.TryGetValue(tokenName, out string? safeName))
                                            {
                                                safeName = $"__p{safeNames.Count}";
                                                safeNames[tokenName] = safeName;
                                                parameters[safeName] = tokenName;
                                            }

                                            return safeName;
                                        });

                                    templateParts.Add((
                                        name,
                                        Expression.Compile(expressionText, false),
                                        parameters));
                                }
                                catch (Exception ex)
                                {
                                    templateFormulaError ??= ex.Message;
                                }

                                continue;
                            }

                            if (part.TrimStart().StartsWith("{", StringComparison.Ordinal))
                            {
                                templateFormulaError ??= $"Invalid template assignment '{part}'.";
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(numericFormulaText))
                            {
                                numericFormulaError ??=
                                    "Only one numeric conversion expression is allowed before template assignments.";
                                continue;
                            }

                            numericFormulaText = part.Trim();
                        }

                        if (!string.IsNullOrWhiteSpace(numericFormulaText))
                        {
                            try
                            {
                                parsedExpression = Expression.Compile(numericFormulaText, false);
                            }
                            catch (Exception ex)
                            {
                                numericFormulaError = ex.Message;
                            }
                        }
                    }

                    var cachedRule = new CachedRule
                    {
                        ToUnit = toUnit,
                        FormulaText = formulaText,
                        NumericFormulaText = numericFormulaText,
                        Format = format,
                        FastFormula = string.IsNullOrWhiteSpace(numericFormulaError)
                            ? TryCreateFastFormula(numericFormulaText)
                            : null,
                        ParsedExpression = parsedExpression,
                        TemplateParts = templateParts.ToArray(),
                        NumericFormulaError = numericFormulaError,
                        TemplateFormulaError = templateFormulaError
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

                    IEnumerable<string> currentUnits =
                        unitsByParameter.TryGetValue(normalizedParameter, out string[]? existing)
                            ? existing
                            : Array.Empty<string>();

                    string[] combinedUnits = currentUnits
                        .Concat(ReadCell(row, sUnitsCol).Split(
                            new[] { ',' },
                            StringSplitOptions.RemoveEmptyEntries))
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
                Parameters = parameterDisplayNames.Values
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
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
