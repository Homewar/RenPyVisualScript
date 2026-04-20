using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using RenPyVisualScriptMVVM.Modules.Editors.Services.Preview;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace RenPyVisualScriptMVVM.Modules.Editors.Converters;

public sealed class FontPreviewConverter : IValueConverter
{
    private static readonly Dictionary<string, Bitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private const string PreviewText = "Aa Bb Cc 123 Sample";
    private const int PreviewWidth = 360;
    private const int PreviewHeight = 52;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        if (Cache.TryGetValue(path, out var cached))
            return cached;

        var preview = CrossPlatformPreviewProvider.GetFontPreview(path, PreviewText, PreviewWidth, PreviewHeight);
        Cache[path] = preview;
        return preview;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
