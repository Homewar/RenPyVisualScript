# RenPy Visual Script MVVM

## English

RenPy Visual Script MVVM is a desktop editor for Ren'Py projects that shifts the focus from manually writing scripts to visual development using separate tools: a code editor, a story graph view, a dialogue/story text editor, and project settings.

The project is in an early stage of development. Some interfaces and service file formats may change.

## Features

- Create a new Ren'Py project through a connected Ren'Py SDK.
- Open and import existing Ren'Py projects.
- Edit `.rpy` scripts with syntax highlighting, autocompletion, and label selection for launch/debugging.
- Launch the project through the Ren'Py SDK from the application interface.
- View the story structure as a graph based on label transitions.
- Visually edit the graph structure and synchronize changes back to `.rpy` files.
- Edit story text separately without directly editing code.
- Browse the project file tree.
- Preview images, animations, videos, audio files, and fonts in the project resources.
- Edit IDE settings, project settings, and Ren'Py GUI settings.
- Use the database log window to diagnose indexing issues.

## Project status

The project is not a stable release yet. The story graph view and the story editor separated from the code editor are already implemented, but the behavior of some tools, service data formats, and UI may change.

## Requirements

For running the portable release:

- Windows x64.
- Installed Ren'Py SDK. The path to the SDK must be specified on first launch.

For building from source:

- .NET 8 SDK.
- Ren'Py SDK for creating and launching projects from the application.

## Installation

1. Download the archive from the Releases section.
2. Extract the archive to any convenient folder.
3. Run `RenPyVisualScript.exe`.
4. On first launch, specify the path to the Ren'Py SDK.

If the release is built as self-contained, installing the .NET Runtime separately is not required.

## Created projects and user data

### New projects

New projects are created next to the application:

```text
<AppDirectory>/Project/<ProjectName>/
```

Example portable structure:

```text
RenPyVisualScriptMVVM/
  RenPyVisualScriptMVVM.exe
  Project/
    MyNovel/
      game/
      ...
```

### Imported projects

The application supports two scenarios:

- open an existing Ren'Py project without copying it;
- copy an existing project into the application folder.

When copying, the project is saved here:

```text
<AppDirectory>/Project/<ImportedProjectName>/
```

If the name is already taken, a folder with a suffix is created:

```text
<AppDirectory>/Project/<ImportedProjectName>_1/
```

### Project settings

Project-specific settings are stored inside the project folder:

```text
<ProjectRoot>/.projectSettings/settings.json
```

The graph editor state is stored here:

```text
<ProjectRoot>/.projectSettings/graph-view-state.json
```

### Story index

The local story indexing database is stored inside the project:

```text
<ProjectRoot>/.rvsmvm/story-index.db
```

This database is used by the story editor and graph tools. It can be recreated by re-indexing the project.

### Global IDE settings

The Ren'Py SDK path and IDE settings are stored in the user profile:

```text
%APPDATA%/RenPyVisualScriptMVVM/ide-settings.json
```

On Windows, this is usually:

```text
C:/Users/<UserName>/AppData/Roaming/RenPyVisualScriptMVVM/ide-settings.json
```

If no project is selected, general application settings are saved here:

```text
%APPDATA%/RenPyVisualScriptMVVM/settings.json
```

## Building from source

Debug build:

```powershell
dotnet build .\RenPyVisualScriptMVVM.csproj
```

Release publish for portable Windows x64:

```powershell
dotnet publish .\RenPyVisualScriptMVVM.csproj -c Release -r win-x64 --self-contained true -p:UseAppHost=true -o .\publish\win-x64
```

Single-file publish:

```powershell
dotnet publish .\RenPyVisualScriptMVVM.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:UseAppHost=true -o .\publish\win-x64
```

The generated artifacts are placed in the `publish/` folder.

## Roadmap

Planned future improvements:

- Add translation tools to the story editor so story text can be translated directly inside the application.
- Add spell checking and spelling issue highlighting.
- Improve the code editor with diagnostics, including warnings and errors, more advanced syntax highlighting, and an expanded snippet set.
- Add a scene and Ren'Py screen/window builder with preview support, making it easier to see how scenes and UI windows will look in-game.
- Expand the story graph to show where global variables are changed and which labels use them.
- Improve the graph parser so it can detect more non-standard and unusual transition patterns.


## Technologies

- C# / .NET 8
- Avalonia UI
- CommunityToolkit.Mvvm
- EF Core + SQLite
- AvaloniaEdit + TextMate grammars
- Magick.NET
- NAudio
- Ren'Py SDK integration

## Third-party libraries

This project uses third-party open-source libraries.
See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

---

## Русский

RenPy Visual Script MVVM - настольный редактор для Ren'Py-проектов, который смещает фокус с ручного написания скриптов на визуальную разработку при помощи отдельных инструментов: редактора кода, графового представления сюжета, редактора реплик и настроек проекта.

Проект находится на стадии ранней разработки. Часть интерфейсов и форматов служебных файлов может меняться.

## Возможности

- Создание нового Ren'Py-проекта через подключенный Ren'Py SDK.
- Открытие и импорт существующих Ren'Py-проектов.
- Редактор `.rpy`-скриптов с подсветкой синтаксиса, автодополнением и выбором лейбла для запуска/отладки.
- Запуск проекта через Ren'Py SDK из интерфейса приложения.
- Графовое представление сюжета по label-переходам.
- Визуальное редактирование структуры графа и синхронизация изменений обратно в `.rpy`-файлы.
- Отдельный редактор текста сюжета без прямого редактирования кода.
- Просмотр дерева файлов проекта.
- Предпросмотр изображений, анимаций, видео, аудио и шрифтов в ресурсах проекта.
- Редактирование IDE-настроек, project settings и GUI-настроек Ren'Py.
- Окно логов базы данных для диагностики индексации.

## Статус проекта

Проект пока не является стабильным релизом. Уже реализованы графовое представление сюжета и редактор сюжета отдельно от кода, но поведение некоторых инструментов, формат служебных данных и UI могут изменяться.

## Требования

Для запуска готового portable-релиза:

- Windows x64.
- Установленный Ren'Py SDK, путь к которому нужно указать при первом запуске.

Для сборки из исходников:

- .NET 8 SDK.
- Ren'Py SDK для создания и запуска проектов из приложения.

## Установка

1. Скачать архив из раздела Releases.
2. Распаковать архив в удобную папку.
3. Запустить `RenPyVisualScript.exe`.
4. При первом запуске указать путь к Ren'Py SDK.

Если релиз собран как self-contained, устанавливать .NET Runtime отдельно не требуется.

## Созданные проекты и пользовательские данные

### Новые проекты

Новые проекты создаются рядом с приложением:

```text
<AppDirectory>/Project/<ProjectName>/
```

Пример portable-структуры:

```text
RenPyVisualScriptMVVM/
  RenPyVisualScriptMVVM.exe
  Project/
    MyNovel/
      game/
      ...
```

### Импортированные проекты

Приложение поддерживает два сценария:

- открыть существующий Ren'Py-проект без копирования;
- скопировать существующий проект внутрь папки приложения.

При копировании проект сохраняется сюда:

```text
<AppDirectory>/Project/<ImportedProjectName>/
```

Если имя уже занято, создается папка с суффиксом:

```text
<AppDirectory>/Project/<ImportedProjectName>_1/
```

### Настройки проекта

Настройки конкретного проекта хранятся внутри его папки:

```text
<ProjectRoot>/.projectSettings/settings.json
```

Состояние графового редактора хранится здесь:

```text
<ProjectRoot>/.projectSettings/graph-view-state.json
```

### Индекс сюжета

Локальная база индексации сюжета хранится внутри проекта:

```text
<ProjectRoot>/.rvsmvm/story-index.db
```

Эта база используется редактором сюжета и графовыми инструментами. Ее можно пересоздать повторной индексацией проекта.

### Глобальные настройки IDE

Путь к Ren'Py SDK и IDE-настройки хранятся в профиле пользователя:

```text
%APPDATA%/RenPyVisualScriptMVVM/ide-settings.json
```

На Windows это обычно:

```text
C:/Users/<UserName>/AppData/Roaming/RenPyVisualScriptMVVM/ide-settings.json
```

Если проект не выбран, общие настройки приложения сохраняются сюда:

```text
%APPDATA%/RenPyVisualScriptMVVM/settings.json
```

## Сборка из исходников

Debug-сборка:

```powershell
dotnet build .\RenPyVisualScriptMVVM.csproj
```

Release publish для portable Windows x64:

```powershell
dotnet publish .\RenPyVisualScriptMVVM.csproj -c Release -r win-x64 --self-contained true -p:UseAppHost=true -o .\publish\win-x64
```

Single-file publish:

```powershell
dotnet publish .\RenPyVisualScriptMVVM.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:UseAppHost=true -o .\publish\win-x64
```

Готовые артефакты попадают в папку `publish/`.

## Roadmap

В планах:

- Добавить в редактор сюжета инструменты перевода текста прямо внутри приложения.
- Добавить проверку орфографии и подсветку орфографических ошибок.
- Развить редактор кода: диагностика предупреждений и ошибок, более продвинутая подсветка синтаксиса и расширенный набор сниппетов.
- Добавить окно сборки сцен и Ren'Py screens/windows с предпросмотром, чтобы заранее видеть, как сцена или окно будет выглядеть в игре.
- Расширить возможности графа: показывать, где изменяются глобальные переменные и в каких лейблах они используются.
- Улучшить парсер графа, чтобы он распознавал больше нестандартных и экзотических способов перехода.


## Технологии

- C# / .NET 8
- Avalonia UI
- CommunityToolkit.Mvvm
- EF Core + SQLite
- AvaloniaEdit + TextMate grammars
- Magick.NET
- NAudio
- Ren'Py SDK integration

## Third-party libraries

This project uses third-party open-source libraries.
See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
