using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace RenPyVisualScriptMVVM.Modules.Settings.Models;

/// <summary>
/// Настройки gui.rpy. Содержит как явно описанные свойства (часть GUI),
/// так и динамические свойства, распарсенные из gui.rpy (define/default ... = ...).
/// Динамические свойства отображаются в PropertyGrid через ICustomTypeDescriptor.
/// </summary>
public partial class GUISettings : ObservableObject, ICustomTypeDescriptor
{
    // -----------------------------
    // Явные (старые) свойства
    // -----------------------------

    // Цвета
    private Color _acent_color = Color.Parse("#0066cc");
    private Color _idle_color = Color.Parse("#888888");
    private Color _idle_small_color = Color.Parse("#aaaaaa");
    private Color _hover_color = Color.Parse("#66a3e0");
    private Color _selected_color = Color.Parse("#ffffff");
    private Color _insensitive_color = Color.Parse("#ffffff");
    private Color _muted_color = Color.Parse("#002851");
    private Color _hover_muted_color = Color.Parse("#003d7a");
    private Color _text_color = Color.Parse("#ffffff");
    private Color _interface_text_color = Color.Parse("#ffffff");

    // Шрифты и их размеры
    private string _text_font = "DejaVuSans.ttf";
    private string _name_text_font = "DejaVuSans.ttf";
    private string _interface_text_font = "DejaVuSans.ttf";
    private int _text_size = 33;
    private int _name_text_size = 45;
    private int _interface_text_size = 33;
    private int _label_text_size = 36;
    private int _notify_text_size = 24;
    private int _title_text_size = 75;

    // Главное и игровое меню.
    private string _main_menu_background = "gui/main_menu.png";
    private string _game_menu_background = "gui/game_menu.png";

    [Category("Цвет"), DisplayName("Акцентный цвет")]
    public Color Acent_color
    {
        get => _acent_color;
        set => SetProperty(ref _acent_color, value);
    }

    [Category("Цвет"), DisplayName("Цвет текстовой кнопки")]
    public Color Idle_color
    {
        get => _idle_color;
        set => SetProperty(ref _idle_color, value);
    }

    [Category("Цвет"), DisplayName("Idle small color")]
    public Color Idle_small_color
    {
        get => _idle_small_color;
        set => SetProperty(ref _idle_small_color, value);
    }

    [Category("Цвет"), DisplayName("Цвет кнопки при наведении")]
    public Color Hover_color
    {
        get => _hover_color;
        set => SetProperty(ref _hover_color, value);
    }

    [Category("Цвет"), DisplayName("Цвет активной кнопки")]
    public Color Selected_color
    {
        get => _selected_color;
        set => SetProperty(ref _selected_color, value);
    }

    [Category("Цвет"), DisplayName("Цвет неактивной кнопки")]
    public Color Insensitive_color
    {
        get => _insensitive_color;
        set => SetProperty(ref _insensitive_color, value);
    }

    [Category("Цвет"), DisplayName("Muted color")]
    public Color Muted_color
    {
        get => _muted_color;
        set => SetProperty(ref _muted_color, value);
    }

    [Category("Цвет"), DisplayName("Hover muted color")]
    public Color Hover_muted_color
    {
        get => _hover_muted_color;
        set => SetProperty(ref _hover_muted_color, value);
    }

    [Category("Цвет"), DisplayName("Цвет текста диалогов")]
    public Color Text_color
    {
        get => _text_color;
        set => SetProperty(ref _text_color, value);
    }

    [Category("Цвет"), DisplayName("Цвет интерфейсного текста")]
    public Color Interface_text_color
    {
        get => _interface_text_color;
        set => SetProperty(ref _interface_text_color, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Шрифт текста")]
    public string TextFont
    {
        get => _text_font;
        set => SetProperty(ref _text_font, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Шрифт имени")]
    public string NameTextFont
    {
        get => _name_text_font;
        set => SetProperty(ref _name_text_font, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Шрифт интерфейса")]
    public string InterfaceTextFont
    {
        get => _interface_text_font;
        set => SetProperty(ref _interface_text_font, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Размер текста")]
    public int TextSize
    {
        get => _text_size;
        set => SetProperty(ref _text_size, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Размер имени")]
    public int NameTextSize
    {
        get => _name_text_size;
        set => SetProperty(ref _name_text_size, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Размер интерфейса")]
    public int InterfaceTextSize
    {
        get => _interface_text_size;
        set => SetProperty(ref _interface_text_size, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Размер label")]
    public int LableTextSize
    {
        get => _label_text_size;
        set => SetProperty(ref _label_text_size, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Размер notify")]
    public int NotifyTextSize
    {
        get => _notify_text_size;
        set => SetProperty(ref _notify_text_size, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Размер заголовка")]
    public int TitleTextSize
    {
        get => _title_text_size;
        set => SetProperty(ref _title_text_size, value);
    }

    [Category("Главное и игровое меню"), DisplayName("Задний фон главного меню")]
    public string MainMenuBackground
    {
        get => _main_menu_background;
        set => SetProperty(ref _main_menu_background, value);
    }

    [Category("Главное и игровое меню"), DisplayName("Задний фон игрового меню")]
    public string GameMenuBackground
    {
        get => _game_menu_background;
        set => SetProperty(ref _game_menu_background, value);
    }

    // -----------------------------
    // Динамические свойства gui.rpy
    // -----------------------------

    internal readonly Dictionary<string, GuiRpyEntry> Dynamic = new(StringComparer.Ordinal);

    internal static readonly HashSet<string> KnownKeys = new(StringComparer.Ordinal)
    {
        "gui.accent_color",
        "gui.idle_color",
        "gui.idle_small_color",
        "gui.hover_color",
        "gui.selected_color",
        "gui.insensitive_color",
        "gui.muted_color",
        "gui.hover_muted_color",
        "gui.text_color",
        "gui.interface_text_color",
        "gui.text_font",
        "gui.name_text_font",
        "gui.interface_text_font",
        "gui.text_size",
        "gui.name_text_size",
        "gui.interface_text_size",
        "gui.label_text_size",
        "gui.notify_text_size",
        "gui.title_text_size",
        "gui.main_menu_background",
        "gui.game_menu_background",
    };

    internal sealed class GuiRpyEntry
    {
        public required string Key { get; init; }
        /// <summary>Раздел (категория в PropertyGrid). Берётся из заголовка секции в gui.rpy.</summary>
        public string Category { get; set; } = "gui.rpy";

        /// <summary>Порядок появления в файле внутри раздела.</summary>
        public int Order { get; set; }

        /// <summary>Текст заголовка (из комментария), который будет показан как имя поля.</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>Подсказка при наведении (многострочный комментарий).</summary>
        public string Description { get; set; } = "";

        /// <summary>Тип редактируемого значения.</summary>
        /// <remarks>
        /// Должен быть изменяемым: при парсинге мы сначала создаём запись,
        /// затем уточняем тип по литералу (bool/int/double/Color/строка/сырое выражение).
        /// </remarks>
        public Type ValueType { get; set; } = typeof(string);

        /// <summary>Текущее значение (для PropertyGrid).</summary>
        public object? Value { get; set; }

        /// <summary>Если true, значение сохраняется как выражение без кавычек (например None, Dissolve(.2), (10, 20)).</summary>
        public bool SaveAsRawExpression { get; set; }

        /// <summary>Оригинальный литерал из файла (для информации/отладки).</summary>
        public string OriginalLiteral { get; set; } = "";
    }

    // -----------------------------
    // ICustomTypeDescriptor (для PropertyGrid)
    // -----------------------------

    AttributeCollection ICustomTypeDescriptor.GetAttributes() => TypeDescriptor.GetAttributes(this, true);
    // Заголовок в PropertyGrid.
    string? ICustomTypeDescriptor.GetClassName() => "Оглавление";
    string? ICustomTypeDescriptor.GetComponentName() => TypeDescriptor.GetComponentName(this, true);
    TypeConverter ICustomTypeDescriptor.GetConverter() => TypeDescriptor.GetConverter(this, true);
    EventDescriptor? ICustomTypeDescriptor.GetDefaultEvent() => TypeDescriptor.GetDefaultEvent(this, true);
    PropertyDescriptor? ICustomTypeDescriptor.GetDefaultProperty() => TypeDescriptor.GetDefaultProperty(this, true);
    object? ICustomTypeDescriptor.GetEditor(Type editorBaseType) => TypeDescriptor.GetEditor(this, editorBaseType, true);
    EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[]? attributes) => TypeDescriptor.GetEvents(this, attributes, true);
    EventDescriptorCollection ICustomTypeDescriptor.GetEvents() => TypeDescriptor.GetEvents(this, true);

    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[]? attributes)
    {
        var baseProps = TypeDescriptor.GetProperties(this, attributes, true)
            .Cast<PropertyDescriptor>()
            .Where(p => p.Name != nameof(Dynamic)) // внутренние поля не показываем
            .ToList();

        var dynamicProps = Dynamic.Values
            .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Order)
            .ThenBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .Select(e => (PropertyDescriptor)new GuiRpyPropertyDescriptor(e))
            .ToList();

        var all = baseProps.Concat(dynamicProps).ToArray();
        return new PropertyDescriptorCollection(all);
    }

    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties() => ((ICustomTypeDescriptor)this).GetProperties(null);
    object? ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor? pd) => this;

    private sealed class GuiRpyPropertyDescriptor : PropertyDescriptor
    {
        private readonly GuiRpyEntry _e;

        public GuiRpyPropertyDescriptor(GuiRpyEntry e)
            : base(e.Key, BuildAttributes(e))
        {
            _e = e;
        }

        public override bool CanResetValue(object? component) => false;
        public override Type ComponentType => typeof(GUISettings);
        public override object? GetValue(object? component) => _e.Value;
        public override bool IsReadOnly => false;
        public override Type PropertyType => _e.ValueType;
        public override void ResetValue(object? component) { }
        public override void SetValue(object? component, object? value)
        {
            _e.Value = value;
            // Уведомляем PropertyGrid, если он подписан.
            OnValueChanged(component, EventArgs.Empty);
        }
        public override bool ShouldSerializeValue(object? component) => true;

        private static Attribute[] BuildAttributes(GuiRpyEntry e)
        {
            var list = new List<Attribute>
            {
                new CategoryAttribute(string.IsNullOrWhiteSpace(e.Category) ? "gui.rpy" : e.Category),
                new DisplayNameAttribute(string.IsNullOrWhiteSpace(e.DisplayName) ? e.Key : e.DisplayName),
            };
            if (!string.IsNullOrWhiteSpace(e.Description))
                list.Add(new DescriptionAttribute(e.Description));
            return list.ToArray();
        }
    }
}
