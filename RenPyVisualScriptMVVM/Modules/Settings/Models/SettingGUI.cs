using PropertyModels.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RenPyVisualScriptMVVM.Modules.Settings.Models;
public partial class GUISettings : ObservableObject
{
    //Цвета
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

    //Шрифты и их размеры
    private string _text_font = "DejaVuSans.ttf";
    private string _name_text_font = "DejaVuSans.ttf";
    private string _interface_text_font = "DejaVuSans.ttf";
    private int _text_size = 33;
    private int _name_text_size = 45;
    private int _interface_text_size = 33;
    private int _label_text_size = 36;
    private int _notify_text_size = 24;
    private int _title_text_size = 75;

    //Главное и игровое меню.
    private string _main_menu_background = "gui/main_menu.png";
    private string _game_menu_background = "gui/game_menu.png";

    //Диалог


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

    [Category("Цвет"), DisplayName("Small_color")]
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

    [Category("Цвет"), DisplayName("Цвет неактивной кнопкиц")]
    public Color Insensitive_color
    {
        get => _insensitive_color;
        set => SetProperty(ref _insensitive_color, value);
    }

    [Category("Цвет"), DisplayName("muted_color")]
    public Color Muted_color
    {
        get => _muted_color;
        set => SetProperty(ref _muted_color, value);
    }

    [Category("Цвет"), DisplayName("hover_muted_color")]
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

    [Category("Цвет"), DisplayName("Акцентный цвет")]
    public Color Interface_text_color
    {
        get => _interface_text_color;
        set => SetProperty(ref _interface_text_color, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Внутриигровой текст")]
    public string TextFont
    {
        get => _text_font;
        set => SetProperty(ref _text_font, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Внутриигровой текст")]
    public string NameTextFont
    {
        get => _name_text_font;
        set => SetProperty(ref _name_text_font, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Внутриигровой текст")]
    public string InterfaceTextFont
    {
        get => _interface_text_font;
        set => SetProperty(ref _interface_text_font, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Внутриигровой текст")]
    public int TextSize
    {
        get => _text_size;
        set => SetProperty(ref _text_size, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Внутриигровой текст")]
    public int NameTextSize
    {
        get => _name_text_size;
        set => SetProperty(ref _name_text_size, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Внутриигровой текст")]
    public int InterfaceTextSize
    {
        get => _interface_text_size;
        set => SetProperty(ref _interface_text_size, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Внутриигровой текст")]
    public int LableTextSize
    {
        get => _label_text_size;
        set => SetProperty(ref _label_text_size, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Внутриигровой текст")]
    public int NotifyTextSize
    {
        get => _notify_text_size;
        set => SetProperty(ref _notify_text_size, value);
    }

    [Category("Шрифты и их размеры"), DisplayName("Внутриигровой текст")]
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
}

