using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NCalc;
using NCalc.Domain;

namespace Aarohi.Classes;

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

        public (string Name, LogicalExpression Expression, Dictionary<string, string> Parameters)[] TemplateParts { get; init; } = Array.Empty<(string, LogicalExpression, Dictionary<string, string>)>();

        public string? NumericFormulaError { get; init; }

        public string? TemplateFormulaError { get; init; }
    }

    private sealed class CacheSnapshot
    {
        public static readonly CacheSnapshot Empty = new CacheSnapshot();

        public DataTable? RulesTable { get; init; }

        public DataTable? ParameterMappingTable { get; init; }

        public DataTable? UnitFormatRulesTable { get; init; }

        public bool RulesReady { get; init; }

        public bool ParameterMappingReady { get; init; }

        public bool UnitFormatRulesReady { get; init; }

        public bool HasFormatColumn { get; init; }

        public Dictionary<string, CachedRule> RulesByKey { get; init; } = new Dictionary<string, CachedRule>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> FormatsByToKey { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> FormatsByFromKey { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> UnitFormatsByKey { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> FromUnits { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string[]> UnitsByParameter { get; init; } = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        public string[] Parameters { get; init; } = Array.Empty<string>();
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
                Func<double, double> func = ParseExpression();
                SkipWhiteSpace();
                if (_pos != _text.Length)
                {
                    return false;
                }

                compiled = func;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Func<double, double> ParseExpression()
        {
            Func<double, double> func = ParseTerm();
            while (true)
            {
                SkipWhiteSpace();
                if (Match('+'))
                {
                    Func<double, double> right = ParseTerm();
                    Func<double, double> currentLeft = func;
                    func = (double v) => currentLeft(v) + right(v);
                    continue;
                }

                if (!Match('-'))
                {
                    break;
                }

                Func<double, double> right2 = ParseTerm();
                Func<double, double> currentLeft2 = func;
                func = (double v) => currentLeft2(v) - right2(v);
            }

            return func;
        }

        private Func<double, double> ParseTerm()
        {
            Func<double, double> func = ParseFactor();
            while (true)
            {
                SkipWhiteSpace();
                if (Match('*'))
                {
                    Func<double, double> right = ParseFactor();
                    Func<double, double> currentLeft = func;
                    func = (double v) => currentLeft(v) * right(v);
                    continue;
                }

                if (!Match('/'))
                {
                    break;
                }

                Func<double, double> right2 = ParseFactor();
                Func<double, double> currentLeft2 = func;
                func = (double v) => currentLeft2(v) / right2(v);
            }

            return func;
        }

        private Func<double, double> ParseFactor()
        {
            SkipWhiteSpace();
            if (Match('+'))
            {
                return ParseFactor();
            }

            if (Match('-'))
            {
                Func<double, double> factor = ParseFactor();
                return (double v) => 0.0 - factor(v);
            }

            if (Match('('))
            {
                Func<double, double> result = ParseExpression();
                if (!Match(')'))
                {
                    throw new FormatException();
                }

                return result;
            }

            if (_pos < _text.Length && char.ToLowerInvariant(_text[_pos]) == 'v')
            {
                _pos++;
                return (double v) => v;
            }

            double number = ParseNumber();
            return (double _) => number;
        }

        private double ParseNumber()
        {
            SkipWhiteSpace();
            int pos = _pos;
            while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.' || _text[_pos] == 'e' || _text[_pos] == 'E' || (_pos > pos && (_text[_pos] == '+' || _text[_pos] == '-') && (_text[_pos - 1] == 'e' || _text[_pos - 1] == 'E'))))
            {
                _pos++;
            }

            if (pos == _pos)
            {
                throw new FormatException();
            }

            string text = _text;
            int num = pos;
            return double.Parse(text.Substring(num, _pos - num), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private bool Match(char c)
        {
            SkipWhiteSpace();
            if (_pos >= _text.Length || _text[_pos] != c)
            {
                return false;
            }

            _pos++;
            return true;
        }

        private void SkipWhiteSpace()
        {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
            {
                _pos++;
            }
        }
    }

    private static readonly object CacheBuildGate = new object();

    private static CacheSnapshot _cache = CacheSnapshot.Empty;

    private static string sQuantity = "Quantity";

    private static string sFrom = "FromUnit";

    private static string sTo = "ToUnit";

    private static string sFormula = "Formula";

    private static string sFormat = "Format";

    private static string sDefaultFormat = "0.00";

    private static string sParameterCol = "Perameter";

    private static string sUnitsCol = "Units";

    private static string sUnitFormatUnitCol = "Unit";

    public static DataTable? ConversionRules
    {
        get
        {
            return Volatile.Read(in _cache).RulesTable;
        }
        set
        {
            CacheSnapshot cacheSnapshot = Volatile.Read(in _cache);
            Load(value, cacheSnapshot.ParameterMappingTable, cacheSnapshot.UnitFormatRulesTable);
        }
    }

    public static DataTable? ParameterUnitMapping
    {
        get
        {
            return Volatile.Read(in _cache).ParameterMappingTable;
        }
        set
        {
            CacheSnapshot cacheSnapshot = Volatile.Read(in _cache);
            Load(cacheSnapshot.RulesTable, value, cacheSnapshot.UnitFormatRulesTable);
        }
    }

    public static DataTable? UnitFormatRules
    {
        get
        {
            return Volatile.Read(in _cache).UnitFormatRulesTable;
        }
        set
        {
            CacheSnapshot cacheSnapshot = Volatile.Read(in _cache);
            Load(cacheSnapshot.RulesTable, cacheSnapshot.ParameterMappingTable, value);
        }
    }

    public static string Quantity
    {
        get
        {
            return sQuantity;
        }
        set
        {
            sQuantity = value;
            RebuildCurrentCache();
        }
    }

    public static string From
    {
        get
        {
            return sFrom;
        }
        set
        {
            sFrom = value;
            RebuildCurrentCache();
        }
    }

    public static string To
    {
        get
        {
            return sTo;
        }
        set
        {
            sTo = value;
            RebuildCurrentCache();
        }
    }

    public static string Formula
    {
        get
        {
            return sFormula;
        }
        set
        {
            sFormula = value;
            RebuildCurrentCache();
        }
    }

    public static string Format
    {
        get
        {
            return sFormat;
        }
        set
        {
            sFormat = (string.IsNullOrWhiteSpace(value) ? "Format" : value.Trim());
            RebuildCurrentCache();
        }
    }

    public static string DefaultFormat
    {
        get
        {
            return sDefaultFormat;
        }
        set
        {
            sDefaultFormat = (string.IsNullOrWhiteSpace(value) ? "0.00" : value.Trim());
            RebuildCurrentCache();
        }
    }

    public static string ParameterColumnName
    {
        get
        {
            return sParameterCol;
        }
        set
        {
            sParameterCol = value;
            RebuildCurrentCache();
        }
    }

    public static string UnitsColumnName
    {
        get
        {
            return sUnitsCol;
        }
        set
        {
            sUnitsCol = value;
            RebuildCurrentCache();
        }
    }

    public static string UnitFormatUnitColumnName
    {
        get
        {
            return sUnitFormatUnitCol;
        }
        set
        {
            sUnitFormatUnitCol = (string.IsNullOrWhiteSpace(value) ? "Unit" : value.Trim());
            RebuildCurrentCache();
        }
    }

    public static void Load(DataTable? conversionRules, DataTable? parameterUnitMapping)
    {
        Load(conversionRules, parameterUnitMapping, Volatile.Read(in _cache).UnitFormatRulesTable);
    }

    public static void Load(DataTable? conversionRules, DataTable? parameterUnitMapping, DataTable? unitFormatRules)
    {
        CacheSnapshot value = BuildCache(conversionRules, parameterUnitMapping, unitFormatRules);
        lock (CacheBuildGate)
        {
            Volatile.Write(ref _cache, value);
        }
    }

    private static void RebuildCurrentCache()
    {
        CacheSnapshot cacheSnapshot = Volatile.Read(in _cache);
        Load(cacheSnapshot.RulesTable, cacheSnapshot.ParameterMappingTable, cacheSnapshot.UnitFormatRulesTable);
    }

    public static bool HasMapping(string fromUnit)
    {
        CacheSnapshot rulesCache = GetRulesCache();
        if (string.IsNullOrWhiteSpace(fromUnit))
        {
            return false;
        }

        return rulesCache.FromUnits.Contains(Normalize(fromUnit));
    }

    public static (double value, string toUnit) convert(string parameter, object inputValue, string fromUnit, string toUnit)
    {
        (double, string, string) tuple = ConvertInternal(parameter, inputValue, fromUnit, toUnit);
        return (value: tuple.Item1, toUnit: tuple.Item2);
    }

    public static (string value, string toUnit) convertFormatted(string parameter, object inputValue, string fromUnit, string toUnit)
    {
        CacheSnapshot rulesCache = GetRulesCache();
        if (string.IsNullOrWhiteSpace(parameter))
        {
            throw new ArgumentException("Parameter cannot be null or empty.", "parameter");
        }

        if (string.IsNullOrWhiteSpace(fromUnit))
        {
            throw new ArgumentException("FromUnit cannot be null or empty.", "fromUnit");
        }

        if (string.IsNullOrWhiteSpace(toUnit))
        {
            throw new ArgumentException("ToUnit cannot be null or empty.", "toUnit");
        }

        if (!string.Equals(fromUnit.Trim(), toUnit.Trim(), StringComparison.OrdinalIgnoreCase) && rulesCache.RulesByKey.TryGetValue(GetRuleKey(parameter, fromUnit, toUnit), out var value))
        {
            string text = ResolveFormat(rulesCache, parameter, fromUnit, toUnit);
            if (value.TemplateParts.Length != 0 && Regex.IsMatch(text, "\\{[^{}]+\\}"))
            {
                if (!string.IsNullOrWhiteSpace(value.TemplateFormulaError))
                {
                    throw new InvalidOperationException($"Invalid template formula for parameter='{parameter}', FromUnit='{fromUnit}', ToUnit='{toUnit}': {value.TemplateFormulaError}");
                }

                Dictionary<string, double> values = EvaluateTemplate(value, ToDouble(inputValue));
                return (value: ApplyTemplate(text, values), toUnit: value.ToUnit);
            }
        }

        (double, string, string) tuple = ConvertInternal(parameter, inputValue, fromUnit, toUnit);
        return (value: FormatValue(tuple.Item1, tuple.Item3), toUnit: tuple.Item2);
    }

    public static double ConvertValue(string quantity, object inputValue, string fromUnit, string toUnit)
    {
        return convert(quantity, inputValue, fromUnit, toUnit).value;
    }

    public static string ConvertValueFormatted(string quantity, object inputValue, string fromUnit, string toUnit)
    {
        return convertFormatted(quantity, inputValue, fromUnit, toUnit).value;
    }

    public static string GetNumberFormat(string parameter, string fromUnit, string toUnit)
    {
        CacheSnapshot rulesCache = GetRulesCache();
        parameter = parameter?.Trim() ?? string.Empty;
        fromUnit = fromUnit?.Trim() ?? string.Empty;
        toUnit = toUnit?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(parameter))
        {
            return sDefaultFormat;
        }

        if (string.IsNullOrWhiteSpace(toUnit))
        {
            return sDefaultFormat;
        }

        return ResolveFormat(rulesCache, parameter, fromUnit, toUnit);
    }

    public static string FormatValue(double value, string format)
    {
        string text = value.ToString(string.IsNullOrWhiteSpace(format) ? sDefaultFormat : format, CultureInfo.InvariantCulture);
        if (text.Contains(".", StringComparison.OrdinalIgnoreCase))
        {
            text = text.TrimEnd('0').TrimEnd('.');
        }

        return text;
    }

    private static (double value, string toUnit, string format) ConvertInternal(string parameter, object inputValue, string fromUnit, string toUnit)
    {
        CacheSnapshot rulesCache = GetRulesCache();
        if (string.IsNullOrWhiteSpace(parameter))
        {
            throw new ArgumentException("Parameter cannot be null or empty.", "parameter");
        }

        if (string.IsNullOrWhiteSpace(fromUnit))
        {
            throw new ArgumentException("FromUnit cannot be null or empty.", "fromUnit");
        }

        if (string.IsNullOrWhiteSpace(toUnit))
        {
            throw new ArgumentException("ToUnit cannot be null or empty.", "toUnit");
        }

        double num = ToDouble(inputValue);
        string item = ResolveFormat(rulesCache, parameter, fromUnit, toUnit);
        if (string.Equals(fromUnit.Trim(), toUnit.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return (value: num, toUnit: toUnit.Trim(), format: item);
        }

        if (!rulesCache.RulesByKey.TryGetValue(GetRuleKey(parameter, fromUnit, toUnit), out var value))
        {
            throw new InvalidOperationException($"No conversion rule found for parameter='{parameter}', FromUnit='{fromUnit}', ToUnit='{toUnit}'.");
        }

        if (string.IsNullOrWhiteSpace(value.FormulaText))
        {
            throw new InvalidOperationException($"Formula is empty for parameter='{parameter}', FromUnit='{fromUnit}', ToUnit='{toUnit}'.");
        }

        if (string.IsNullOrWhiteSpace(value.NumericFormulaText))
        {
            throw new InvalidOperationException($"Numeric conversion expression is missing for parameter='{parameter}', FromUnit='{fromUnit}', ToUnit='{toUnit}'. " + "When template variables are used, keep the numeric expression first, for example: v/60000;{a}=...;{b}=...");
        }

        if (value.ParsedExpression == null || !string.IsNullOrWhiteSpace(value.NumericFormulaError))
        {
            throw new InvalidOperationException($"Invalid numeric conversion formula for parameter='{parameter}', FromUnit='{fromUnit}', ToUnit='{toUnit}': {value.NumericFormulaError ?? "Formula could not be compiled."}");
        }

        if (value.FastFormula != null)
        {
            return (value: value.FastFormula(num), toUnit: value.ToUnit, format: item);
        }

        return (value: ToDouble(EvaluateFormula(value.ParsedExpression, num)), toUnit: value.ToUnit, format: item);
    }

    public static string GetUnitFromParameter(string parameter)
    {
        List<string> unitsFromParameter = GetUnitsFromParameter(parameter);
        if (unitsFromParameter.Count <= 0)
        {
            return string.Empty;
        }

        return unitsFromParameter[0];
    }

    public static List<string> GetUnitsFromParameter(string parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter))
        {
            return new List<string>();
        }

        if (!GetParameterMappingCache().UnitsByParameter.TryGetValue(Normalize(parameter), out var value))
        {
            return new List<string>();
        }

        return new List<string>(value);
    }

    public static string[] GetAllParameters()
    {
        List<string> list = new List<string>(GetParameterMappingCache().Parameters);
        list.Insert(0, "--Select--");
        return list.ToArray();
    }

    private static CacheSnapshot GetRulesCache()
    {
        CacheSnapshot cacheSnapshot = Volatile.Read(in _cache);
        DataTable rulesTable = cacheSnapshot.RulesTable;
        if (rulesTable == null)
        {
            throw new InvalidOperationException("ConversionRules DataTable is null. Set UnitConverisonEngine.ConversionRules first.");
        }

        if (cacheSnapshot.RulesReady)
        {
            return cacheSnapshot;
        }

        if (!rulesTable.Columns.Contains(sQuantity))
        {
            throw new InvalidOperationException("Missing column '" + sQuantity + "' in ConversionRules.");
        }

        if (!rulesTable.Columns.Contains(sFrom))
        {
            throw new InvalidOperationException("Missing column '" + sFrom + "' in ConversionRules.");
        }

        if (!rulesTable.Columns.Contains(sTo))
        {
            throw new InvalidOperationException("Missing column '" + sTo + "' in ConversionRules.");
        }

        if (!rulesTable.Columns.Contains(sFormula))
        {
            throw new InvalidOperationException("Missing column '" + sFormula + "' in ConversionRules.");
        }

        return cacheSnapshot;
    }

    private static CacheSnapshot GetParameterMappingCache()
    {
        CacheSnapshot cacheSnapshot = Volatile.Read(in _cache);
        DataTable parameterMappingTable = cacheSnapshot.ParameterMappingTable;
        if (parameterMappingTable == null)
        {
            throw new InvalidOperationException("ParameterUnitMapping DataTable is null. Set UnitConverisonEngine.ParameterUnitMapping first.");
        }

        if (cacheSnapshot.ParameterMappingReady)
        {
            return cacheSnapshot;
        }

        if (!parameterMappingTable.Columns.Contains(sParameterCol))
        {
            throw new InvalidOperationException("Missing column '" + sParameterCol + "' in ParameterUnitMapping.");
        }

        if (!parameterMappingTable.Columns.Contains(sUnitsCol))
        {
            throw new InvalidOperationException("Missing column '" + sUnitsCol + "' in ParameterUnitMapping.");
        }

        return cacheSnapshot;
    }

    private static double ToDouble(object v)
    {
        if (v == null || v == DBNull.Value)
        {
            throw new ArgumentNullException("v", "Input value is null.");
        }

        if (!(v is double result))
        {
            if (v is float num)
            {
                return num;
            }

            if (v is decimal num2)
            {
                return (double)num2;
            }

            if (v is int num3)
            {
                return num3;
            }

            if (v is long num4)
            {
                return num4;
            }

            if (v is short num5)
            {
                return num5;
            }

            if (v is byte b)
            {
                return (int)b;
            }

            if (double.TryParse(Convert.ToString(v), NumberStyles.Any, CultureInfo.InvariantCulture, out var result2))
            {
                return result2;
            }

            return Convert.ToDouble(v, CultureInfo.InvariantCulture);
        }

        return result;
    }

    private static object? EvaluateFormula(LogicalExpression parsedExpression, double v)
    {
        object obj = new Expression(parsedExpression, EvaluateOptions.IgnoreCase)
        {
            Parameters = { ["v"] = v }
        }.Evaluate();
        if (obj is decimal num)
        {
            return (double)num;
        }

        return obj;
    }

    private static Dictionary<string, double> EvaluateTemplate(CachedRule rule, double inputValue)
    {
        Dictionary<string, double> dictionary = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["v"] = inputValue };
        (string, LogicalExpression, Dictionary<string, string>)[] templateParts = rule.TemplateParts;
        for (int i = 0; i < templateParts.Length; i++)
        {
            (string, LogicalExpression, Dictionary<string, string>) tuple = templateParts[i];
            Expression expression = new Expression(tuple.Item2, EvaluateOptions.IgnoreCase);
            expression.Parameters["v"] = inputValue;
            foreach (KeyValuePair<string, string> item in tuple.Item3)
            {
                if (!dictionary.TryGetValue(item.Value, out var value))
                {
                    throw new InvalidOperationException($"Template variable '{{{item.Value}}}' must be calculated before '{{{tuple.Item1}}}'.");
                }

                expression.Parameters[item.Key] = value;
            }

            dictionary[tuple.Item1] = ToDouble(expression.Evaluate());
        }

        return dictionary;
    }

    private static string ApplyTemplate(string format, IReadOnlyDictionary<string, double> values)
    {
        return Regex.Replace(format, "\\{(?<name>[^{}:]+)(?::(?<format>[^{}]+))?\\}", delegate (Match match)
        {
            string text = match.Groups["name"].Value.Trim();
            if (!values.TryGetValue(text, out var value))
            {
                throw new InvalidOperationException("Unknown template token '{" + text + "}'.");
            }

            string text2 = (match.Groups["format"].Success ? match.Groups["format"].Value : "0.########");
            return value.ToString(text2, CultureInfo.InvariantCulture);
        });
    }

    private static Func<double, double>? TryCreateFastFormula(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return null;
        }

        if (!new FastFormulaParser(formula).TryParse(out Func<double, double> compiled))
        {
            return null;
        }

        return compiled;
    }

    private static CacheSnapshot BuildCache(DataTable? rules, DataTable? parameterMapping, DataTable? unitFormatRules)
    {
        Dictionary<string, CachedRule> dictionary = new Dictionary<string, CachedRule>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> dictionary2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> dictionary3 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> dictionary4 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (HasRuleColumns(rules))
        {
            foreach (DataRow row4 in rules.Rows)
            {
                string text = ReadCell(row4, sQuantity);
                string text2 = ReadCell(row4, sFrom);
                string text3 = ReadCell(row4, sTo);
                if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(text2) || string.IsNullOrWhiteSpace(text3))
                {
                    continue;
                }

                string text4 = ReadCell(row4, sFormula);
                string text5 = (rules.Columns.Contains(sFormat) ? ReadCell(row4, sFormat) : string.Empty);
                text5 = (string.IsNullOrWhiteSpace(text5) ? sDefaultFormat : text5);
                string text6 = string.Empty;
                LogicalExpression parsedExpression = null;
                string text7 = null;
                string text8 = null;
                List<(string, LogicalExpression, Dictionary<string, string>)> list = new List<(string, LogicalExpression, Dictionary<string, string>)>();
                if (!string.IsNullOrWhiteSpace(text4))
                {
                    HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    string[] array = text4.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    string[] array2 = array;
                    foreach (string text9 in array2)
                    {
                        Match match = Regex.Match(text9, "^\\s*\\{(?<name>[^{}:=;]+)\\}\\s*=\\s*(?<expression>.+?)\\s*$");
                        if (match.Success)
                        {
                            string text10 = match.Groups["name"].Value.Trim();
                            if (string.Equals(text10, "v", StringComparison.OrdinalIgnoreCase))
                            {
                                if (text8 == null)
                                {
                                    text8 = "'v' is reserved for the input value and cannot be a template variable name.";
                                }

                                continue;
                            }

                            if (!hashSet2.Add(text10))
                            {
                                if (text8 == null)
                                {
                                    text8 = "Duplicate template variable '{" + text10 + "}'.";
                                }

                                continue;
                            }

                            try
                            {
                                Dictionary<string, string> parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                Dictionary<string, string> safeNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                string expression = Regex.Replace(match.Groups["expression"].Value, "\\{(?<name>[^{}:]+)\\}", delegate (Match token)
                                {
                                    string text14 = token.Groups["name"].Value.Trim();
                                    if (!safeNames.TryGetValue(text14, out var value5))
                                    {
                                        value5 = $"__p{safeNames.Count}";
                                        safeNames[text14] = value5;
                                        parameters[value5] = text14;
                                    }

                                    return value5;
                                });
                                list.Add((text10, Expression.Compile(expression, nocache: false), parameters));
                            }
                            catch (Exception ex)
                            {
                                if (text8 == null)
                                {
                                    text8 = ex.Message;
                                }
                            }
                        }
                        else if (text9.TrimStart().StartsWith("{", StringComparison.Ordinal))
                        {
                            if (text8 == null)
                            {
                                text8 = "Invalid template assignment '" + text9 + "'.";
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(text6))
                        {
                            if (text7 == null)
                            {
                                text7 = "Only one numeric conversion expression is allowed before template assignments.";
                            }
                        }
                        else
                        {
                            text6 = text9.Trim();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(text6))
                    {
                        try
                        {
                            parsedExpression = Expression.Compile(text6, nocache: false);
                        }
                        catch (Exception ex2)
                        {
                            text7 = ex2.Message;
                        }
                    }
                }

                CachedRule value = new CachedRule
                {
                    ToUnit = text3,
                    FormulaText = text4,
                    NumericFormulaText = text6,
                    Format = text5,
                    FastFormula = (string.IsNullOrWhiteSpace(text7) ? TryCreateFastFormula(text6) : null),
                    ParsedExpression = parsedExpression,
                    TemplateParts = list.ToArray(),
                    NumericFormulaError = text7,
                    TemplateFormulaError = text8
                };
                dictionary.TryAdd(GetRuleKey(text, text2, text3), value);
                dictionary2.TryAdd(GetPairKey(text, text3), text5);
                dictionary3.TryAdd(GetPairKey(text, text2), text5);
                hashSet.Add(Normalize(text2));
            }
        }

        if (HasUnitFormatColumns(unitFormatRules))
        {
            foreach (DataRow row5 in unitFormatRules.Rows)
            {
                string text11 = ReadCell(row5, sQuantity);
                string text12 = ReadCell(row5, sUnitFormatUnitCol);
                if (!string.IsNullOrWhiteSpace(text11) && !string.IsNullOrWhiteSpace(text12))
                {
                    string text13 = ReadCell(row5, sFormat);
                    dictionary4.TryAdd(GetPairKey(text11, text12), string.IsNullOrWhiteSpace(text13) ? sDefaultFormat : text13);
                }
            }
        }

        Dictionary<string, string[]> dictionary5 = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> dictionary6 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (HasParameterMappingColumns(parameterMapping))
        {
            foreach (DataRow row6 in parameterMapping.Rows)
            {
                string value2 = ReadCell(row6, sParameterCol);
                if (!string.IsNullOrWhiteSpace(value2))
                {
                    string key = Normalize(value2);
                    dictionary6.TryAdd(key, value2);
                    IEnumerable<string> first;
                    if (!dictionary5.TryGetValue(key, out var value3))
                    {
                        IEnumerable<string> enumerable = Array.Empty<string>();
                        first = enumerable;
                    }
                    else
                    {
                        IEnumerable<string> enumerable = value3;
                        first = enumerable;
                    }

                    string[] value4 = (from unit in first.Concat(ReadCell(row6, sUnitsCol).Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                                       select unit.Trim() into unit
                                       where !string.IsNullOrWhiteSpace(unit)
                                       select unit).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
                    dictionary5[key] = value4;
                }
            }
        }

        return new CacheSnapshot
        {
            RulesTable = rules,
            ParameterMappingTable = parameterMapping,
            UnitFormatRulesTable = unitFormatRules,
            RulesReady = HasRuleColumns(rules),
            ParameterMappingReady = HasParameterMappingColumns(parameterMapping),
            UnitFormatRulesReady = HasUnitFormatColumns(unitFormatRules),
            HasFormatColumn = (rules?.Columns.Contains(sFormat) ?? false),
            RulesByKey = dictionary,
            FormatsByToKey = dictionary2,
            FormatsByFromKey = dictionary3,
            UnitFormatsByKey = dictionary4,
            FromUnits = hashSet,
            UnitsByParameter = dictionary5,
            Parameters = dictionary6.Values.OrderBy<string, string>((string result) => result, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static string ResolveFormat(CacheSnapshot cache, string parameter, string fromUnit, string toUnit)
    {
        if (cache.UnitFormatsByKey.TryGetValue(GetPairKey(parameter, toUnit), out var value))
        {
            return value;
        }

        if (!cache.HasFormatColumn)
        {
            return sDefaultFormat;
        }

        if (!string.IsNullOrWhiteSpace(fromUnit) && cache.RulesByKey.TryGetValue(GetRuleKey(parameter, fromUnit, toUnit), out var value2))
        {
            return value2.Format;
        }

        if (cache.FormatsByToKey.TryGetValue(GetPairKey(parameter, toUnit), out var value3))
        {
            return value3;
        }

        if (!cache.FormatsByFromKey.TryGetValue(GetPairKey(parameter, fromUnit), out var value4))
        {
            return sDefaultFormat;
        }

        return value4;
    }

    private static bool HasRuleColumns(DataTable? table)
    {
        if (table != null && table.Columns.Contains(sQuantity) && table.Columns.Contains(sFrom) && table.Columns.Contains(sTo))
        {
            return table.Columns.Contains(sFormula);
        }

        return false;
    }

    private static bool HasParameterMappingColumns(DataTable? table)
    {
        if (table != null && table.Columns.Contains(sParameterCol))
        {
            return table.Columns.Contains(sUnitsCol);
        }

        return false;
    }

    private static bool HasUnitFormatColumns(DataTable? table)
    {
        if (table != null && table.Columns.Contains(sQuantity) && table.Columns.Contains(sUnitFormatUnitCol))
        {
            return table.Columns.Contains(sFormat);
        }

        return false;
    }

    private static string ReadCell(DataRow row, string columnName)
    {
        if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
        {
            return string.Empty;
        }

        return Convert.ToString(row[columnName])?.Trim() ?? string.Empty;
    }

    private static string GetRuleKey(string quantity, string fromUnit, string toUnit)
    {
        return $"{Normalize(quantity)}\u001f{Normalize(fromUnit)}\u001f{Normalize(toUnit)}";
    }

    private static string GetPairKey(string quantity, string unit)
    {
        return Normalize(quantity) + "\u001f" + Normalize(unit);
    }

    private static string Normalize(string value)
    {
        return value?.Trim().ToUpperInvariant() ?? string.Empty;
    }
}