using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RenPyVisualScriptMVVM.Modules.Settings.Models;

public partial class GUISettings
{
    public static GUISettings LoadFromGuiRpy(string guiRpyPath)
    {
        var s = new GUISettings();
        if (string.IsNullOrWhiteSpace(guiRpyPath) || !File.Exists(guiRpyPath))
            return s;

        var text = File.ReadAllText(guiRpyPath);

        // 1) Заполняем динамические значения (все define/default в gui.rpy),
        // исключая те, что уже представлены явными свойствами (KnownKeys).
        foreach (var e in ParseAllDefines(text))
        {
            if (KnownKeys.Contains(e.Key))
                continue;

            s.Dynamic[e.Key] = e;
        }

        // 2) Синхронизируем известные свойства (старые поля UI).
        // Colors
        TrySetColor(text, "gui.accent_color", v => s.Acent_color = v);
        TrySetColor(text, "gui.idle_color", v => s.Idle_color = v);
        TrySetColor(text, "gui.idle_small_color", v => s.Idle_small_color = v);
        TrySetColor(text, "gui.hover_color", v => s.Hover_color = v);
        TrySetColor(text, "gui.selected_color", v => s.Selected_color = v);
        TrySetColor(text, "gui.insensitive_color", v => s.Insensitive_color = v);
        TrySetColor(text, "gui.muted_color", v => s.Muted_color = v);
        TrySetColor(text, "gui.hover_muted_color", v => s.Hover_muted_color = v);
        TrySetColor(text, "gui.text_color", v => s.Text_color = v);
        TrySetColor(text, "gui.interface_text_color", v => s.Interface_text_color = v);

        // Fonts
        TrySetString(text, "gui.text_font", v => s.TextFont = v);
        TrySetString(text, "gui.name_text_font", v => s.NameTextFont = v);
        TrySetString(text, "gui.interface_text_font", v => s.InterfaceTextFont = v);

        // Sizes
        TrySetInt(text, "gui.text_size", v => s.TextSize = v);
        TrySetInt(text, "gui.name_text_size", v => s.NameTextSize = v);
        TrySetInt(text, "gui.interface_text_size", v => s.InterfaceTextSize = v);
        TrySetInt(text, "gui.label_text_size", v => s.LableTextSize = v);
        TrySetInt(text, "gui.notify_text_size", v => s.NotifyTextSize = v);
        TrySetInt(text, "gui.title_text_size", v => s.TitleTextSize = v);

        // Backgrounds
        TrySetString(text, "gui.main_menu_background", v => s.MainMenuBackground = v);
        TrySetString(text, "gui.game_menu_background", v => s.GameMenuBackground = v);

        return s;
    }

    public void SaveToGuiRpy(string guiRpyPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(guiRpyPath) ?? ".");

        var (text, hasBom) = ReadTextWithBom(guiRpyPath);
        if (text is null)
            text = DefaultGuiRpySkeleton();

        // 1) Сохраняем явные (старые) поля UI.
        text = ReplaceOrAppendDefine(text, "gui.accent_color", QuoteColor(Acent_color));
        text = ReplaceOrAppendDefine(text, "gui.idle_color", QuoteColor(Idle_color));
        text = ReplaceOrAppendDefine(text, "gui.idle_small_color", QuoteColor(Idle_small_color));
        text = ReplaceOrAppendDefine(text, "gui.hover_color", QuoteColor(Hover_color));
        text = ReplaceOrAppendDefine(text, "gui.selected_color", QuoteColor(Selected_color));
        text = ReplaceOrAppendDefine(text, "gui.insensitive_color", QuoteColor(Insensitive_color));
        text = ReplaceOrAppendDefine(text, "gui.muted_color", QuoteColor(Muted_color));
        text = ReplaceOrAppendDefine(text, "gui.hover_muted_color", QuoteColor(Hover_muted_color));
        text = ReplaceOrAppendDefine(text, "gui.text_color", QuoteColor(Text_color));
        text = ReplaceOrAppendDefine(text, "gui.interface_text_color", QuoteColor(Interface_text_color));

        text = ReplaceOrAppendDefine(text, "gui.text_font", QuoteString(TextFont));
        text = ReplaceOrAppendDefine(text, "gui.name_text_font", QuoteString(NameTextFont));
        text = ReplaceOrAppendDefine(text, "gui.interface_text_font", QuoteString(InterfaceTextFont));

        text = ReplaceOrAppendDefine(text, "gui.text_size", TextSize.ToString(CultureInfo.InvariantCulture));
        text = ReplaceOrAppendDefine(text, "gui.name_text_size", NameTextSize.ToString(CultureInfo.InvariantCulture));
        text = ReplaceOrAppendDefine(text, "gui.interface_text_size", InterfaceTextSize.ToString(CultureInfo.InvariantCulture));
        text = ReplaceOrAppendDefine(text, "gui.label_text_size", LableTextSize.ToString(CultureInfo.InvariantCulture));
        text = ReplaceOrAppendDefine(text, "gui.notify_text_size", NotifyTextSize.ToString(CultureInfo.InvariantCulture));
        text = ReplaceOrAppendDefine(text, "gui.title_text_size", TitleTextSize.ToString(CultureInfo.InvariantCulture));

        text = ReplaceOrAppendDefine(text, "gui.main_menu_background", QuoteString(MainMenuBackground));
        text = ReplaceOrAppendDefine(text, "gui.game_menu_background", QuoteString(GameMenuBackground));

        // 2) Сохраняем динамические поля (все распарсенные define/default из gui.rpy).
        foreach (var kv in Dynamic.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var key = kv.Key;
            var e = kv.Value;
            if (KnownKeys.Contains(key))
                continue;

            var literal = ToRenpyLiteral(e);
            text = ReplaceOrAppendDefine(text, key, literal);
        }

        WriteTextPreserveBom(guiRpyPath, text, hasBom);
    }

    private static string ToRenpyLiteral(GuiRpyEntry e)
    {
        if (e.Value is null)
            return "None";

        if (e.ValueType == typeof(bool) && e.Value is bool b)
            return b ? "True" : "False";

        if (e.ValueType == typeof(int) && e.Value is int i)
            return i.ToString(CultureInfo.InvariantCulture);

        if ((e.ValueType == typeof(double) || e.ValueType == typeof(float)) && e.Value is IConvertible)
            return Convert.ToDouble(e.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

        if (e.ValueType == typeof(Color) && e.Value is Color c)
            return QuoteColor(c);

        // Строки могут быть как "сырой" Ren'Py-выражение, так и обычная строка.
        if (e.Value is string s)
        {
            if (e.SaveAsRawExpression)
                return s.Trim();
            return QuoteString(s);
        }

        // Fallback.
        return e.Value.ToString() ?? "None";
    }

    private static IEnumerable<GuiRpyEntry> ParseAllDefines(string text)
    {
        // Нужная семантика gui.rpy:
        // 1) "## Раздел #######" -> Category.
        // 2) Несколько строк "## ..." сразу после заголовка раздела -> описание раздела.
        // 3) Несколько строк "## ..." перед define/default -> имя+подсказка поля.
        // 4) Если после блока комментариев идёт несколько define подряд, то комментарий применяется ко всем этим define.

        text = text.Replace("\r\n", "\n");

        var currentCategory = "gui.rpy";
        var currentCategoryDescriptionLines = new List<string>();
        var pendingFieldHintLines = new List<string>();
        var inSectionDescription = false;
        var orderInSection = 0;

        var usedDisplayNames = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Заголовок секции:
            //   ## Диалог ######################################################################
            var sectionHeader = Regex.Match(line, @"^\s*##\s*(?<name>.+?)\s*#{3,}\s*$");
            if (sectionHeader.Success)
            {
                currentCategory = sectionHeader.Groups["name"].Value.Trim();
                currentCategoryDescriptionLines.Clear();
                pendingFieldHintLines.Clear();
                inSectionDescription = true;
                orderInSection = 0;
                continue;
            }

            // Комментарии "## ..."
            if (Regex.IsMatch(line, @"^\s*##\s*\S"))
            {
                var c = Regex.Replace(line, @"^\s*##\s*", "").TrimEnd();
                if (string.IsNullOrWhiteSpace(c))
                    continue;

                if (inSectionDescription)
                {
                    // Описание раздела (многострочное)
                    currentCategoryDescriptionLines.Add(c);
                }
                else
                {
                    // Подсказка полей (многострочная)
                    pendingFieldHintLines.Add(c);
                }
                continue;
            }

            // Пустая строка отделяет блоки подсказок полей.
            if (string.IsNullOrWhiteSpace(line))
            {
                pendingFieldHintLines.Clear();
                // Пустая строка после заголовка секции отделяет её описание от полей.
                inSectionDescription = false;
                continue;
            }

            // define/default (не закомментировано)
            var m = Regex.Match(line, @"^(?!\s*#)\s*(?<kw>define|default)\s+(?<key>[A-Za-z0-9_.]+)\s*=\s*(?<val>.+?)\s*$");
            if (!m.Success)
            {
                // Любая другая строка обрывает блок подсказок полей.
                pendingFieldHintLines.Clear();
                inSectionDescription = false;
                continue;
            }

            var key = m.Groups["key"].Value.Trim();
            var valLiteral = StripInlineComment(m.Groups["val"].Value.Trim());

            inSectionDescription = false;

            var entry = BuildEntryFromLiteral(
                key,
                valLiteral,
                currentCategory,
                currentCategoryDescriptionLines,
                pendingFieldHintLines,
                orderInSection++);

            // Дубли DisplayName внутри секции делаем уникальными.
            if (!usedDisplayNames.TryGetValue(entry.Category, out var used))
            {
                used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                usedDisplayNames[entry.Category] = used;
            }
            if (!string.IsNullOrWhiteSpace(entry.DisplayName) && used.Contains(entry.DisplayName))
            {
                var suffix = entry.Key.Split('.').LastOrDefault() ?? entry.Key;
                entry.DisplayName = $"{entry.DisplayName} ({suffix})";
            }
            used.Add(entry.DisplayName);

            yield return entry;
            // pendingFieldHintLines намеренно не чистим: один блок комментариев может относиться к нескольким define подряд.
        }
    }

    private static GuiRpyEntry BuildEntryFromLiteral(
        string key,
        string literal,
        string category,
        List<string> categoryDescriptionLines,
        List<string> fieldHintLines,
        int order)
    {
        var fieldTitle = fieldHintLines.FirstOrDefault();
        var fieldHint = fieldHintLines.Count > 0 ? string.Join("\n", fieldHintLines) : "";
        var categoryHint = categoryDescriptionLines.Count > 0 ? string.Join("\n", categoryDescriptionLines) : "";
        var description = string.IsNullOrWhiteSpace(categoryHint)
            ? fieldHint
            : (string.IsNullOrWhiteSpace(fieldHint) ? categoryHint : categoryHint + "\n\n" + fieldHint);

        var e = new GuiRpyEntry
        {
            Key = key,
            Category = string.IsNullOrWhiteSpace(category) ? "gui.rpy" : category,
            Order = order,
            OriginalLiteral = literal,
            DisplayName = string.IsNullOrWhiteSpace(fieldTitle) ? key : fieldTitle,
            Description = description,
            ValueType = typeof(string),
            Value = literal,
            SaveAsRawExpression = true,
        };

        // bool
        if (string.Equals(literal, "True", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(literal, "False", StringComparison.OrdinalIgnoreCase))
        {
            e.ValueType = typeof(bool);
            e.Value = string.Equals(literal, "True", StringComparison.OrdinalIgnoreCase);
            e.SaveAsRawExpression = true;
            return e;
        }

        // None
        if (string.Equals(literal, "None", StringComparison.OrdinalIgnoreCase))
        {
            e.ValueType = typeof(string);
            e.Value = "None";
            e.SaveAsRawExpression = true;
            return e;
        }

        // int
        if (int.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
        {
            e.ValueType = typeof(int);
            e.Value = i;
            e.SaveAsRawExpression = true;
            return e;
        }

        // float
        if (double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) &&
            (literal.Contains('.') || literal.Contains('e') || literal.Contains('E')))
        {
            e.ValueType = typeof(double);
            e.Value = d;
            e.SaveAsRawExpression = true;
            return e;
        }

        // string literal "..." / '...'
        var s = TryUnquote(literal, out var quote);
        if (s != null)
        {
            // Color as '#RRGGBB' / '#RRGGBBAA'
            if (s.StartsWith("#", StringComparison.Ordinal) && (s.Length == 7 || s.Length == 9))
            {
                try
                {
                    var norm = NormalizeRenpyHexToAvalonia(s);
                    e.ValueType = typeof(Color);
                    e.Value = Color.Parse(norm);
                    e.SaveAsRawExpression = true; // сохраняем через QuoteColor
                    return e;
                }
                catch
                {
                    // fallback to string
                }
            }

            e.ValueType = typeof(string);
            e.Value = s;
            e.SaveAsRawExpression = false;
            return e;
        }

        // Остальные выражения сохраняем как "сырой" текст.
        e.ValueType = typeof(string);
        e.Value = literal;
        e.SaveAsRawExpression = true;
        return e;
    }

    private static string StripInlineComment(string s)
    {
        // Удаляем #... только если # не внутри одинарных/двойных кавычек.
        bool inSingle = false, inDouble = false;
        for (int i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (ch == '\\')
            {
                i++; // skip escaped
                continue;
            }
            if (ch == '\'' && !inDouble) inSingle = !inSingle;
            else if (ch == '"' && !inSingle) inDouble = !inDouble;
            else if (ch == '#' && !inSingle && !inDouble)
                return s.Substring(0, i).TrimEnd();
        }
        return s.TrimEnd();
    }

    private static string? TryUnquote(string s, out char quote)
    {
        quote = '\0';
        s = s.Trim();
        if (s.Length >= 2 &&
            ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
        {
            quote = s[0];
            return s.Substring(1, s.Length - 2);
        }
        return null;
    }

    private static void TrySetColor(string text, string key, Action<Color> set)
    {
        var v = FindDefineString(text, key);
        if (v is null)
            return;
        try
        {
            var norm = NormalizeRenpyHexToAvalonia(v);
            set(Color.Parse(norm));
        }
        catch
        {
            // ignore
        }
    }

    private static void TrySetString(string text, string key, Action<string> set)
    {
        var v = FindDefineString(text, key);
        if (v is null)
            return;
        set(v);
    }

    private static void TrySetInt(string text, string key, Action<int> set)
    {
        var raw = FindDefineRaw(text, key);
        if (raw is null)
            return;
        if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            set(n);
    }

    private static string? FindDefineString(string text, string key)
    {
        // define gui.xxx = '...'
        var raw = FindDefineRaw(text, key);
        if (raw is null)
            return null;

        raw = raw.Trim();
        if (raw.Length >= 2 && ((raw.StartsWith(""") && raw.EndsWith(""")) || (raw.StartsWith("'") && raw.EndsWith("'"))))
            return raw.Substring(1, raw.Length - 2);
        return null;
    }

    private static string? FindDefineRaw(string text, string key)
    {
        var k = Regex.Escape(key);
        var rx = new Regex($@"(?m)^(?!\s*#)\s*(?:define|default)\s+{k}\s*=\s*(?<val>[^\r\n#]+?)\s*$", RegexOptions.CultureInvariant);
        var m = rx.Match(text);
        if (!m.Success)
            return null;
        return m.Groups["val"].Value;
    }

    private static string ReplaceOrAppendDefine(string text, string key, string valueLiteral)
    {
        var k = Regex.Escape(key);
        var rx = new Regex($@"(?m)^(?<indent>\s*)#?\s*(?:define|default)\s+{k}\s*=.*$", RegexOptions.CultureInvariant);
        if (rx.IsMatch(text))
        {
            return rx.Replace(text, m => $"{m.Groups["indent"].Value}define {key} = {valueLiteral}", 1);
        }

        // Append near the end of configurable variables block if possible, otherwise at EOF.
        var marker = "## Конфигурируемые";
        var idx = text.IndexOf(marker, StringComparison.Ordinal);
        if (idx >= 0)
        {
            // Find next blank line after marker.
            var insertAt = text.IndexOf("\n\n", idx, StringComparison.Ordinal);
            if (insertAt < 0) insertAt = text.Length;
            return text.Insert(insertAt + 2, $"define {key} = {valueLiteral}\n");
        }

        if (!text.EndsWith("\n"))
            text += "\n";
        return text + $"define {key} = {valueLiteral}\n";
    }

    private static string QuoteString(string? s)
    {
        s ??= "";
        // Ren'Py в gui.rpy использует двойные кавычки для путей/шрифтов.
        return "\"" + s.Replace("\\", "/").Replace("\"", "\\\"") + "\"";
    }

    private static string QuoteColor(Color c)
    {
        // Ren'Py использует #RRGGBB или #RRGGBBAA (альфа в конце).
        var rrggbb = $"{c.R:X2}{c.G:X2}{c.B:X2}";
        var hex = c.A == 255
            ? $"#{rrggbb}"
            : $"#{rrggbb}{c.A:X2}";
        return "'" + hex.ToLowerInvariant() + "'";
    }

    private static string NormalizeRenpyHexToAvalonia(string s)
    {
        s = s.Trim();
        if (!s.StartsWith("#", StringComparison.Ordinal))
            return s;
        if (s.Length == 9)
        {
            // Ren'Py: #RRGGBBAA → Avalonia: #AARRGGBB
            var rrggbb = s.Substring(1, 6);
            var aa = s.Substring(7, 2);
            return "#" + aa + rrggbb;
        }
        return s;
    }

    private static (string? text, bool hasBom) ReadTextWithBom(string path)
    {
        if (!File.Exists(path))
            return (null, false);
        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: hasBom);
        return (enc.GetString(bytes), hasBom);
    }

    private static void WriteTextPreserveBom(string path, string text, bool hasBom)
    {
        var enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: hasBom);
        File.WriteAllText(path, text, enc);
    }

    private static string DefaultGuiRpySkeleton()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Auto-generated gui.rpy (minimal). You can replace it with a full template later.");
        sb.AppendLine();
        sb.AppendLine("init offset = -2");
        sb.AppendLine();
        sb.AppendLine("init python:");
        sb.AppendLine("    gui.init(1920, 1080)");
        sb.AppendLine();
        sb.AppendLine("## Конфигурируемые Переменные GUI");
        sb.AppendLine();
        return sb.ToString();
    }
}
