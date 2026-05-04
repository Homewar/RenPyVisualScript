using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using PropertyModels.Collections;
using PropertyModels.Extensions;

namespace RenPyVisualScriptMVVM.Modules.Settings.Models
{
    internal sealed record SettingsDto
    {
        public string ProjectName { get; init; } = "Новая новелла";
        public bool ShowName { get; init; } = true;
        public string Version { get; init; } = "1.0";
        public string About { get; init; } = "";
        public string BuildName { get; init; } = "build_1";

        public string[] MusicCanal { get; init; } = Array.Empty<string>();

        public string EnterTransition { get; init; } = "dissolve";
        public string ExitTransition { get; init; } = "dissolve";
        public string IntraTransition { get; init; } = "dissolve";
        public string AfterLoadTransition { get; init; } = "fade";
        public string EndGameTransition { get; init; } = "fade";

        public float WindowShowTransitions { get; init; } = .2f;
        public float WindowHideTransitions { get; init; } = .2f;

        public string DialogWindow { get; init; } = "auto";

        public int TextCps { get; init; } = 0;
        public int AfmTime { get; init; } = 15;

        public bool EnablePurchases { get; init; } = false;
        public string GooglePlayKey { get; init; } = "";
        public string ItchProject { get; init; } = "";
    }

    public partial class ProjectSettings
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public void SaveToJson(string path)
        {
            var dto = new SettingsDto
            {
                ProjectName = ProjectName,
                ShowName = ShowName,
                Version = Version,
                About = About,
                BuildName = BuildName,

                MusicCanal = MusicCanal.Items.Where(item => MusicCanal.IsChecked(item)).ToArray(),

                EnterTransition = EnterTransition.SelectedValue,
                ExitTransition = ExitTransition.SelectedValue,
                IntraTransition = IntraTransition.SelectedValue,
                AfterLoadTransition = AfterLoadTransitions.SelectedValue,
                EndGameTransition = EndGameTransitions.SelectedValue,

                WindowShowTransitions = WindowShowTransitions,
                WindowHideTransitions = WindowHideTransitions,

                DialogWindow = DialogWindow.SelectedValue,
                TextCps = TextCps,
                AfmTime = AfmTime,

                EnablePurchases = EnablePurchases,
                GooglePlayKey = GooglePlayKey,
                ItchProject = ItchProject
            };

            var json = JsonSerializer.Serialize(dto, _jsonOptions);
            File.WriteAllText(path, json);
        }

        public static ProjectSettings LoadFromJson(string path)
        {
            if (!File.Exists(path))
                return new ProjectSettings();

            SettingsDto? dto;
            try
            {
                var json = File.ReadAllText(path);
                dto = JsonSerializer.Deserialize<SettingsDto>(json, _jsonOptions);
            }
            catch
            {
                return new ProjectSettings();
            }

            if (dto is null)
                return new ProjectSettings();

            var settings = new ProjectSettings
            {
                ProjectName = dto.ProjectName,
                ShowName = dto.ShowName,
                Version = dto.Version,
                About = dto.About,
                BuildName = dto.BuildName,

                WindowShowTransitions = dto.WindowShowTransitions,
                WindowHideTransitions = dto.WindowHideTransitions,

                TextCps = dto.TextCps,
                AfmTime = dto.AfmTime,

                EnablePurchases = dto.EnablePurchases,
                GooglePlayKey = dto.GooglePlayKey,
                ItchProject = dto.ItchProject
            };

            settings.MusicCanal.ApplyChecks(dto.MusicCanal);

            settings.EnterTransition.SelectedValue = dto.EnterTransition;
            settings.ExitTransition.SelectedValue = dto.ExitTransition;
            settings.IntraTransition.SelectedValue = dto.IntraTransition;
            settings.AfterLoadTransitions.SelectedValue = dto.AfterLoadTransition;
            settings.EndGameTransitions.SelectedValue = dto.EndGameTransition;
            settings.DialogWindow.SelectedValue = dto.DialogWindow;

            return settings;
        }
    }
}

internal static class CheckedListExtensions
{
    /// <summary>
    /// Помечает элементы из <paramref name="values"/> как выбранные, используя
    /// публичный метод <c>Check(item, bool)</c> из библиотеки. Если метод
    /// недоступен, выполняется безопасный фолбэк.
    /// </summary>
    public static void ApplyChecks<T>(this CheckedList<T> list, IEnumerable<T>? values)
    {
        if (values is null) return;

        var checkMethod = list.GetType().GetMethod("Check", new[] { typeof(T), typeof(bool) });
        if (checkMethod != null)
        {
            foreach (var item in list.ToArray())
                checkMethod.Invoke(list, new object?[] { item, false });

            foreach (var v in values)
                checkMethod.Invoke(list, new object?[] { v, true });
            return;
        }

        // Фолбэк: если API изменился, просто очищаем и добавляем значения.
        foreach (var item in list.ToArray())
            list.Remove(item);

        foreach (var v in values)
            list.Add(v);
    }
}
