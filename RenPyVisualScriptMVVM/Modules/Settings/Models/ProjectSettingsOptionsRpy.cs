using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RenPyVisualScriptMVVM.Modules.Settings.Models;

public partial class ProjectSettings
{
    /// <summary>
    /// Загружает настройки проекта из Ren'Py файла <c>game/options.rpy</c>.
    /// Если файл не найден/не распарсился — возвращает настройки по умолчанию.
    /// </summary>
    public static ProjectSettings LoadFromOptionsRpy(string optionsRpyPath)
    {
        var settings = new ProjectSettings();
        if (string.IsNullOrWhiteSpace(optionsRpyPath) || !File.Exists(optionsRpyPath))
            return settings;

        var text = File.ReadAllText(optionsRpyPath, Encoding.UTF8);
        // Нормализуем переносы, чтобы regex работали стабильно.
        text = text.Replace("\r\n", "\n");

        // 1) Основное
        settings.ProjectName = ParseStringValue(text, "config\\.name") ?? settings.ProjectName;
        settings.ShowName = ParseBoolValue(text, "gui\\.show_name") ?? settings.ShowName;
        settings.Version = ParseStringValue(text, "config\\.version") ?? settings.Version;

        var about = ParseTripleQuotedBlock(text, "gui\\.about");
        if (about != null)
            settings.About = about;

        settings.BuildName = ParseStringValue(text, "build\\.name") ?? settings.BuildName;

        // 2) Звуки
        ApplySoundFlags(settings, text);

        // 3) Переходы
        settings.EnterTransition.SelectedValue = ParseTransitionToken(text, "config\\.enter_transition") ?? settings.EnterTransition.SelectedValue;
        settings.ExitTransition.SelectedValue = ParseTransitionToken(text, "config\\.exit_transition") ?? settings.ExitTransition.SelectedValue;
        settings.IntraTransition.SelectedValue = ParseTransitionToken(text, "config\\.intra_transition") ?? settings.IntraTransition.SelectedValue;
        settings.AfterLoadTransitions.SelectedValue = ParseTransitionToken(text, "config\\.after_load_transition") ?? settings.AfterLoadTransitions.SelectedValue;
        settings.EndGameTransitions.SelectedValue = ParseTransitionToken(text, "config\\.end_game_transition") ?? settings.EndGameTransitions.SelectedValue;

        // 4) Окна
        var windowMode = ParseStringValue(text, "config\\.window");
        if (!string.IsNullOrWhiteSpace(windowMode))
            settings.DialogWindow.SelectedValue = windowMode;

        var showT = ParseDissolveSeconds(text, "config\\.window_show_transition");
        if (showT.HasValue) settings.WindowShowTransitions = showT.Value;
        var hideT = ParseDissolveSeconds(text, "config\\.window_hide_transition");
        if (hideT.HasValue) settings.WindowHideTransitions = hideT.Value;

        // 5) Текст
        var cps = ParseIntValue(text, "preferences\\.text_cps");
        if (cps.HasValue) settings.TextCps = cps.Value;
        var afm = ParseIntValue(text, "preferences\\.afm_time");
        if (afm.HasValue) settings.AfmTime = afm.Value;

        // 6) Монетизация/itch
        var gKey = ParseStringValue(text, "build\\.google_play_key");
        if (gKey != null)
        {
            settings.GooglePlayKey = gKey;
            settings.EnablePurchases = !string.IsNullOrWhiteSpace(gKey);
        }

        var itch = ParseStringValue(text, "build\\.itch_project");
        if (itch != null)
            settings.ItchProject = itch;

        return settings;
    }

    private static void ApplySoundFlags(ProjectSettings settings, string text)
    {
        bool? sound = ParseBoolValue(text, "config\\.has_sound");
        bool? music = ParseBoolValue(text, "config\\.has_music");
        bool? voice = ParseBoolValue(text, "config\\.has_voice");

        // Если хотя бы один флаг явно True, применяем точное состояние.
        if (sound.HasValue || music.HasValue || voice.HasValue)
        {
            var checkedItems = new System.Collections.Generic.List<string>(3);
            if (sound ?? true) checkedItems.Add("has_sound");
            if (music ?? true) checkedItems.Add("has_music");
            if (voice ?? true) checkedItems.Add("has_voice");
            settings.MusicCanal.ApplyChecks(checkedItems);
        }
    }

    /// <summary>
    /// Ищет строковые значения вида:
    /// define X = "..." / define X = _('...') / define X = _("...")
    /// и возвращает содержимое.
    /// Комментированные строки (начинающиеся с #) игнорируются.
    /// </summary>
    private static string? ParseStringValue(string text, string keyRegex)
    {
        // Берём первую НЕ комментированную строку, где есть key = value.
        // Разрешаем: define|default, пробелы, разные обёртки перевода _(), _p().
        var rx = new Regex(
            $@"(?m)^(?!\s*#)\s*(?:define|default)\s+{keyRegex}\s*=\s*(?<val>.+?)\s*$",
            RegexOptions.CultureInvariant);

        var m = rx.Match(text);
        if (!m.Success) return null;

        var raw = m.Groups["val"].Value.Trim();

        // _("...") / _('...')
        raw = Regex.Replace(raw, "^_\\((.*)\\)$", "$1");

        // _p(""" ... """) handled elsewhere, but if someone uses _p("...") — тоже поддержим.
        raw = Regex.Replace(raw, "^_p\\((.*)\\)$", "$1");

        // Снимаем кавычки
        var s = Unquote(raw);
        return s;
    }

    private static bool? ParseBoolValue(string text, string keyRegex)
    {
        var rx = new Regex(
            $@"(?m)^(?!\s*#)\s*(?:define|default)\s+{keyRegex}\s*=\s*(?<val>True|False)\s*$",
            RegexOptions.CultureInvariant);
        var m = rx.Match(text);
        if (!m.Success) return null;
        return string.Equals(m.Groups["val"].Value, "True", StringComparison.OrdinalIgnoreCase);
    }

    private static int? ParseIntValue(string text, string keyRegex)
    {
        var rx = new Regex(
            $@"(?m)^(?!\s*#)\s*(?:define|default)\s+{keyRegex}\s*=\s*(?<val>-?\d+)\s*$",
            RegexOptions.CultureInvariant);
        var m = rx.Match(text);
        if (!m.Success) return null;
        return int.TryParse(m.Groups["val"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    private static string? ParseTransitionToken(string text, string keyRegex)
    {
        // define config.enter_transition = dissolve
        // define config.after_load_transition = None
        var rx = new Regex(
            $@"(?m)^(?!\s*#)\s*define\s+{keyRegex}\s*=\s*(?<val>[A-Za-z_][A-Za-z0-9_]*|None)\s*$",
            RegexOptions.CultureInvariant);
        var m = rx.Match(text);
        if (!m.Success) return null;
        return m.Groups["val"].Value == "None" ? "None" : m.Groups["val"].Value;
    }

    private static float? ParseDissolveSeconds(string text, string keyRegex)
    {
        // define config.window_show_transition = Dissolve(.2)
        var rx = new Regex(
            $@"(?m)^(?!\s*#)\s*define\s+{keyRegex}\s*=\s*Dissolve\(\s*(?<val>[0-9]*\.?[0-9]+)\s*\)\s*$",
            RegexOptions.CultureInvariant);

        var m = rx.Match(text);
        if (!m.Success) return null;
        return float.TryParse(m.Groups["val"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    private static string? ParseTripleQuotedBlock(string text, string keyRegex)
    {
        // define gui.about = _p("""
        // ...
        // """)
        var rx = new Regex(
            $@"(?s)(?m)^(?!\s*#)\s*define\s+{keyRegex}\s*=\s*_p\(\s*""""""\s*(?<val>.*?)\s*""""""\s*\)\s*$",
            RegexOptions.CultureInvariant);

        var m = rx.Match(text);
        if (!m.Success) return null;

        // Возвращаем содержимое как есть (без обёртки _p(""" """)), но сохраняем переносы.
        return m.Groups["val"].Value;
    }

    private static string Unquote(string s)
    {
        s = s.Trim();
        if (s.Length >= 2)
        {
            if ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\''))
                return s[1..^1];
        }
        return s;
    }
}
