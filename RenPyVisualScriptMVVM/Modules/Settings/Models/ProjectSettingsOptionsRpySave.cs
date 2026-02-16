using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RenPyVisualScriptMVVM.Modules.Settings.Models;

public partial class ProjectSettings
{
    /// <summary>
    /// Сохраняет текущие настройки обратно в Ren'Py файл <c>game/options.rpy</c>.
    /// Обновляет существующие строки <c>define/default</c>. Если ключ не найден — добавляет его
    /// в конец секции "Основное" (или в конец файла, если секция не найдена).
    /// </summary>
    public void SaveToOptionsRpy(string optionsRpyPath)
    {
        if (string.IsNullOrWhiteSpace(optionsRpyPath))
            throw new ArgumentException("optionsRpyPath is empty", nameof(optionsRpyPath));

        var dir = Path.GetDirectoryName(optionsRpyPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        // Если файла нет — создаём минимальный.
        if (!File.Exists(optionsRpyPath))
        {
            var minimal = BuildMinimalOptionsRpy();
            File.WriteAllText(optionsRpyPath, minimal, Encoding.UTF8);
            return;
        }

        var original = File.ReadAllText(optionsRpyPath, Encoding.UTF8);
        var hasCrlf = original.Contains("\r\n", StringComparison.Ordinal);
        var text = original.Replace("\r\n", "\n");

        // 1) Основное
        text = ReplaceOrAppendDefine(text, "config\\.name", $"_({Quote(ProjectName)})");
        text = ReplaceOrAppendDefine(text, "gui\\.show_name", Bool(ShowName));
        text = ReplaceOrAppendDefine(text, "config\\.version", Quote(Version));
        text = ReplaceOrAppendAbout(text, About);
        text = ReplaceOrAppendDefine(text, "build\\.name", Quote(BuildName));

        // 2) Звук/музыка/голос
        var hasSound = MusicCanal.Items.Any(i => string.Equals(i, "has_sound", StringComparison.OrdinalIgnoreCase))
            && MusicCanal.IsChecked(MusicCanal.Items.First(i => string.Equals(i, "has_sound", StringComparison.OrdinalIgnoreCase)));
        var hasMusic = MusicCanal.Items.Any(i => string.Equals(i, "has_music", StringComparison.OrdinalIgnoreCase))
            && MusicCanal.IsChecked(MusicCanal.Items.First(i => string.Equals(i, "has_music", StringComparison.OrdinalIgnoreCase)));
        var hasVoice = MusicCanal.Items.Any(i => string.Equals(i, "has_voice", StringComparison.OrdinalIgnoreCase))
            && MusicCanal.IsChecked(MusicCanal.Items.First(i => string.Equals(i, "has_voice", StringComparison.OrdinalIgnoreCase)));

        text = ReplaceOrAppendDefine(text, "config\\.has_sound", Bool(hasSound));
        text = ReplaceOrAppendDefine(text, "config\\.has_music", Bool(hasMusic));
        text = ReplaceOrAppendDefine(text, "config\\.has_voice", Bool(hasVoice));

        // 3) Переходы
        text = ReplaceOrAppendDefine(text, "config\\.enter_transition", Transition(EnterTransition.SelectedValue));
        text = ReplaceOrAppendDefine(text, "config\\.exit_transition", Transition(ExitTransition.SelectedValue));
        text = ReplaceOrAppendDefine(text, "config\\.intra_transition", Transition(IntraTransition.SelectedValue));
        text = ReplaceOrAppendDefine(text, "config\\.after_load_transition", Transition(AfterLoadTransitions.SelectedValue));
        text = ReplaceOrAppendDefine(text, "config\\.end_game_transition", Transition(EndGameTransitions.SelectedValue));

        // 4) Окна
        text = ReplaceOrAppendDefine(text, "config\\.window", Quote(DialogWindow.SelectedValue));
        text = ReplaceOrAppendDefine(text, "config\\.window_show_transition", $"Dissolve({FloatRenPy(WindowShowTransitions)})");
        text = ReplaceOrAppendDefine(text, "config\\.window_hide_transition", $"Dissolve({FloatRenPy(WindowHideTransitions)})");

        // 5) Текст
        text = ReplaceOrAppendDefault(text, "preferences\\.text_cps", TextCps.ToString(CultureInfo.InvariantCulture));
        text = ReplaceOrAppendDefault(text, "preferences\\.afm_time", AfmTime.ToString(CultureInfo.InvariantCulture));

        // 6) Монетизация/itch
        // Если покупки выключены — ключ комментируем, чтобы не ломать стандартный шаблон Ren'Py.
        text = ReplaceOrAppendDefine(text, "build\\.google_play_key",
            Quote(GooglePlayKey ?? string.Empty),
            allowCommentOut: !EnablePurchases || string.IsNullOrWhiteSpace(GooglePlayKey));

        text = ReplaceOrAppendDefine(text, "build\\.itch_project", Quote(ItchProject ?? string.Empty));

        if (hasCrlf)
            text = text.Replace("\n", "\r\n");

        File.WriteAllText(optionsRpyPath, text, Encoding.UTF8);
    }

    private static string BuildMinimalOptionsRpy()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Основное");
        sb.AppendLine();
        sb.AppendLine("define config.name = _(" + Quote("NewProject") + ")");
        sb.AppendLine("define gui.show_name = True");
        sb.AppendLine("define config.version = \"1.0\"");
        sb.AppendLine("define gui.about = _p(\"\"\"\"");
        sb.AppendLine("\"\"\")");
        sb.AppendLine("define build.name = \"NewProject\"");
        sb.AppendLine();
        sb.AppendLine("## Звуки и музыка");
        sb.AppendLine("define config.has_sound = True");
        sb.AppendLine("define config.has_music = True");
        sb.AppendLine("define config.has_voice = True");
        sb.AppendLine();
        sb.AppendLine("## Переходы");
        sb.AppendLine("define config.enter_transition = dissolve");
        sb.AppendLine("define config.exit_transition = dissolve");
        sb.AppendLine("define config.intra_transition = dissolve");
        sb.AppendLine("define config.after_load_transition = None");
        sb.AppendLine("define config.end_game_transition = None");
        sb.AppendLine();
        sb.AppendLine("## Управление окнами");
        sb.AppendLine("define config.window = \"auto\"");
        sb.AppendLine("define config.window_show_transition = Dissolve(.2)");
        sb.AppendLine("define config.window_hide_transition = Dissolve(.2)");
        sb.AppendLine();
        sb.AppendLine("## Стандартные настройки");
        sb.AppendLine("default preferences.text_cps = 0");
        sb.AppendLine("default preferences.afm_time = 15");
        sb.AppendLine();
        sb.AppendLine("## Дополнительно");
        sb.AppendLine("# define build.google_play_key = \"\"");
        sb.AppendLine("define build.itch_project = \"\"");
        return sb.ToString();
    }

    private static string ReplaceOrAppendDefine(string text, string keyRegex, string valueExpr, bool allowCommentOut = false)
        => ReplaceOrAppendLine(text, "define", keyRegex, valueExpr, allowCommentOut);

    private static string ReplaceOrAppendDefault(string text, string keyRegex, string valueExpr)
        => ReplaceOrAppendLine(text, "default", keyRegex, valueExpr, allowCommentOut: false);

    private static string ReplaceOrAppendLine(string text, string keyword, string keyRegex, string valueExpr, bool allowCommentOut)
    {
        // Ищем НЕ закомментированную строку с define/default.
        var rx = new Regex(
            $@"(?m)^(?<indent>\s*)(?<prefix>{keyword}\s+){keyRegex}(?<mid>\s*=\s*)(?<val>.+?)\s*$",
            RegexOptions.CultureInvariant);

        var m = rx.Match(text);
        if (m.Success)
        {
            var indent = m.Groups["indent"].Value;
            var prefix = m.Groups["prefix"].Value;
            var mid = m.Groups["mid"].Value;
            var replacement = indent + prefix + Regex.Unescape(keyRegex).Replace("\\.", ".") + mid + valueExpr;
            return rx.Replace(text, replacement, 1);
        }

        // Если не нашли — попробуем найти закомментированную строку (# define key = ...)
        // и заменить её (разкомментировать или оставить закомментированной).
        var rxCommented = new Regex(
            $@"(?m)^(?<indent>\s*)#\s*(?<prefix>{keyword}\s+){keyRegex}(?<mid>\s*=\s*)(?<val>.+?)\s*$",
            RegexOptions.CultureInvariant);

        var mc = rxCommented.Match(text);
        if (mc.Success)
        {
            var indent = mc.Groups["indent"].Value;
            var prefix = mc.Groups["prefix"].Value;
            var mid = mc.Groups["mid"].Value;
            var line = indent + prefix + Regex.Unescape(keyRegex).Replace("\\.", ".") + mid + valueExpr;
            if (allowCommentOut)
                line = indent + "# " + line.TrimStart();
            return rxCommented.Replace(text, line, 1);
        }

        // Добавляем в конец секции "Основное" если есть, иначе в конец файла.
        var insertAt = FindInsertPosition(text);
        var toInsert = $"{keyword} {Regex.Unescape(keyRegex).Replace("\\.", ".")} = {valueExpr}\n";
        if (allowCommentOut)
            toInsert = "# " + toInsert;

        return text.Insert(insertAt, "\n" + toInsert);
    }

    private static string ReplaceOrAppendAbout(string text, string about)
    {
        // Пытаемся заменить существующий _p(""" ... """) блок.
        var rx = new Regex(
            $@"(?s)(?m)^(?<indent>\s*)define\s+gui\.about\s*=\s*_p\(\s*""""""\s*(?<val>.*?)\s*""""""\s*\)\s*$",
            RegexOptions.CultureInvariant);

        var m = rx.Match(text);
        if (m.Success)
        {
            var indent = m.Groups["indent"].Value;
            var normalized = NormalizeAbout(about);
            var replacement = indent + "define gui.about = _p(\"\"\"\n" +
                              normalized +
                              "\n\"\"\")";
            return rx.Replace(text, replacement, 1);
        }

        // Если блока нет — добавляем как define.
        var insertAt = FindInsertPosition(text);
        var normalized2 = NormalizeAbout(about);
        var block = "define gui.about = _p(\"\"\"\n" + normalized2 + "\n\"\"\")\n";
        return text.Insert(insertAt, "\n" + block);
    }

    private static int FindInsertPosition(string text)
    {
        // Вставляем после заголовка "## Основное" если он есть.
        var m = Regex.Match(text, "(?m)^##\\s+Основное.*$", RegexOptions.CultureInvariant);
        if (!m.Success) return text.Length;

        // Ищем первую пустую строку после заголовка блока, вставим после неё.
        var after = m.Index + m.Length;
        var m2 = Regex.Match(text.Substring(after), "(?m)^\\s*$");
        return m2.Success ? after + m2.Index + m2.Length : after;
    }

    private static string NormalizeAbout(string about)
    {
        about = about ?? string.Empty;
        about = about.Replace("\r\n", "\n").Replace("\r", "\n");
        // Не трогаем отступы пользователя, но убираем ведущие/замыкающие пустые строки.
        return about.Trim('\n');
    }

    private static string Quote(string s)
    {
        s ??= string.Empty;
        // В Ren'Py обычная строка в двойных кавычках.
        s = s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return "\"" + s + "\"";
    }

    private static string Bool(bool v) => v ? "True" : "False";

    private static string Transition(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return "None";
        return token == "None" ? "None" : token;
    }

    private static string FloatRenPy(float v)
    {
        // Ren'Py принимает .2 и 0.2 одинаково. Сохраним компактный формат (.2).
        var s = v.ToString("0.###", CultureInfo.InvariantCulture);
        if (s.StartsWith("0.", StringComparison.Ordinal))
            s = s[1..];
        if (s.StartsWith("-0.", StringComparison.Ordinal))
            s = "-" + s[2..];
        return s;
    }
}
