using Avalonia.Data.Converters;
using RenPyVisualScriptMVVM.Modules.Editors.Services.Preview;
using System;
using System.Globalization;
using System.IO;

namespace RenPyVisualScriptMVVM.Modules.Editors.Converters;

public sealed class VideoPreviewConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        return CrossPlatformPreviewProvider.GetVideoThumbnail(path, 400, 225);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
