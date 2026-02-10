using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using PropertyModels.Collections;
using PropertyModels.ComponentModel;
using PropertyModels.ComponentModel.DataAnnotations;
using PropertyModels.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RenPyVisualScriptMVVM.Modules.Settings.Models;

/// <summary>
/// Настройки Ren'Py-проекта. Перенесено из RenPyIDe (NovellSettings)
/// и заменяет прежнюю модель Settings.
/// </summary>
public partial class ProjectSettings : ObservableObject
{
    [Browsable(false)]
    public static readonly string[] AllTransitions =
    {
        "fade","dissolve","pixellate","move","moveinright","moveinleft",
        "moveintop","moveinbottom","moveoutright","moveoutleft","moveouttop","moveoutbottom",
        "ease","easeinright","easeinleft","easeintop","easeinbottom","easeoutright",
        "easeoutleft","easeouttop","easeoutbottom","zoomin","zoomout","zoominout",
        "vpunch","hpunch","blinds","squares","wipeleft","wiperight",
        "wipeup","wipedown","slideleft","slideright","slideup","slidedown",
        "slideawayleft","slideawayright","slideawayup","slideawaydown","pushleft",
        "pushright","pushup","pushdown","irisin","irisout","None"
    };

    // Основное
    private string _projectname = "Новая новелла";
    private bool _showName = true;
    private string _version = "1.0";
    private string _about = "Об этой новелле...";
    private string _buildName = "build_1";

    // Управление окнами / переходы
    private float _windowShowTransitions = .2f;
    private float _windowHideTransitions = .2f;

    // Текст
    private int _textCps = 0;
    private int _afmTime = 15;

    // Монетизация
    private string _googlePlayKey = "";

    // Itch.io
    private string _itchProject = "renpytom/test-project";

    [Category("Основное")]
    [DisplayName("Название новеллы")]
    [Description("Читаемое название игры. Используется при установке стандартного заголовка")]
    public string ProjectName
    {
        get => _projectname;
        set => SetProperty(ref _projectname, value);
    }

    [Category("Основное")]
    [DisplayName("Показывать название новеллы")]
    [Description("Определяет, показывать ли заголовок, данный выше, на экране главного меню.")]
    public bool ShowName
    {
        get => _showName;
        set => SetProperty(ref _showName, value);
    }

    [Category("Основное")]
    [DisplayName("Версия игры")]
    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    [Category("Основное")]
    [DisplayName("О новелле")]
    [Description("Текст, помещённый в экран \"Об игре\". Поместите текст между тройными скобками.")]
    public string About
    {
        get => _about;
        set => SetProperty(ref _about, value);
    }

    [Category("Основное")]
    [DisplayName("Имя сборки")]
    [Description("Короткое название игры, используемое для исполняемых файлов и директорий при "
                + "постройке дистрибутивов. Оно должно содержать текст формата ASCII и не должно "
                + "содержать пробелы, двоеточия и точки с запятой.")]
    public string BuildName
    {
        get => _buildName;
        set => SetProperty(ref _buildName, value);
    }

    [Category("Звуки и музыка")]
    [DisplayName("Музыкальные каналы")]
    [Description("Эти три переменные управляют, среди прочего, тем, какие микшеры показываются "
                + "игроку по умолчанию. Установка одной из них в False скроет соответствующий микшер")]
    [SelectableListDisplayMode(SelectableListDisplayMode.Vertical)]
    public CheckedList<string> MusicCanal { get; set; } = new(["has_sound", "has_music", "has_voice"]);

    [Category("Переходы")]
    [DisplayName("Вход в игровое меню")]
    public SelectableList<string> EnterTransition { get; set; } = new(AllTransitions, "dissolve");

    [Category("Переходы")]
    [DisplayName("Выход из игрового меню")]
    public SelectableList<string> ExitTransition { get; set; } = new(AllTransitions, "dissolve");

    [Category("Переходы")]
    [DisplayName("Игровое меню")]
    [Description("Переход между экранами игрового меню")]
    public SelectableList<string> IntraTransition { get; set; } = new(AllTransitions, "dissolve");

    [Category("Переходы")]
    [DisplayName("Загрузка сохранения")]
    [Description("Переход, используемый после загрузки слота сохранения.")]
    public SelectableList<string> AfterLoadTransitions { get; set; } = new(AllTransitions, "fade");

    [Category("Переходы")]
    [DisplayName("Конец игры")]
    [Description("Используется при входе в главное меню после того, как игра закончится.")]
    public SelectableList<string> EndGameTransitions { get; set; } = new(AllTransitions, "fade");

    [Category("Переходы")]
    [DisplayName("Скорость показа окна")]
    [Trackable(0.0, 1.0)]
    [Range(0.0, 1.0)]
    public float WindowShowTransitions
    {
        get => _windowShowTransitions;
        set => SetProperty(ref _windowShowTransitions, value);
    }

    [Category("Переходы")]
    [DisplayName("Скорость сокрытия окна")]
    [Trackable(0.0, 1.0)]
    [Range(0.0, 1.0)]
    public float WindowHideTransitions
    {
        get => _windowHideTransitions;
        set => SetProperty(ref _windowHideTransitions, value);
    }

    [Category("Управление окнами")]
    [DisplayName("Диалоговое окно")]
    [Description("Контролирует, когда появляется диалоговое окно. "
                + "Show - окно всегда показано. "
                + "Hide — окно показывается, только когда представлен диалог. "
                + "Auto - окно скрыто до появления оператора scene и показывается при появлении диалога.")]
    public SelectableList<string> DialogWindow { get; set; } = new(["show", "hide", "auto"], "auto");

    [Category("Текст")]
    [DisplayName("Скорость текста (CPS)")]
    public int TextCps
    {
        get => _textCps;
        set => SetProperty(ref _textCps, value);
    }

    [Category("Текст")]
    [DisplayName("Время авто-просмотра (в секундах)")]
    [Description("Стандартная задержка авточтения. Большие значения означают долгие ожидания, а "
                + "от 0 до 30 — вполне допустимый диапазон")]
    public int AfmTime
    {
        get => _afmTime;
        set => SetProperty(ref _afmTime, value);
    }

    [Category("Дополнительно")]
    [DisplayName("Включить покупки")]
    [ConditionTarget]
    public bool EnablePurchases { get; set; } = false;

    [Category("Дополнительно")]
    [DisplayName("Ключ Google Play")]
    [Description("Ключ приложения Google Play для управления покупками в приложении.")]
    //[PropertyVisibilityCondition(nameof(EnablePurchases), true)]
    public string GooglePlayKey
    {
        get => _googlePlayKey;
        set => SetProperty(ref _googlePlayKey, value);
    }

    [Category("Дополнительно")]
    [DisplayName("Itch.io project")]
    [Description("Имя пользователя и название проекта, ассоциированные с проектом на itch.io")]
    public string ItchProject
    {
        get => _itchProject;
        set => SetProperty(ref _itchProject, value);
    }
}
