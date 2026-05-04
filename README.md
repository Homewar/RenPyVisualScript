# RenPy Visual Script MVVM

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
