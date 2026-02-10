using System;
using Avalonia;
using Avalonia.Svg;


namespace RenPyVisualScriptMVVM
{
    internal sealed class Program
    {
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp()
        {
            GC.KeepAlive(typeof(SvgImageExtension).Assembly);
            GC.KeepAlive(typeof(Avalonia.Svg.Svg).Assembly);
            return AppBuilder.Configure<App>()
                    .UsePlatformDetect()
					.WithInterFont()
                    .LogToTrace();

        }
    }
}
