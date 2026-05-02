using System;
using Avalonia;
using Avalonia.Controls;

namespace RenPyVisualScriptMVVM.Core.Native;

public class DwmShadowBehavior : AvaloniaObject
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<DwmShadowBehavior, Window, bool>(
            "IsEnabled",
            defaultValue: false);

    static DwmShadowBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<Window>((window, _) =>
        {
            OnIsEnabledChanged(window);
        });
    }

    public static bool GetIsEnabled(Window window)
    {
        return window.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(Window window, bool value)
    {
        window.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(Window window)
    {
        window.Opened -= Window_OnOpened;

        if (GetIsEnabled(window))
        {
            window.Opened += Window_OnOpened;

            // Если окно уже создано, пробуем применить сразу.
            WindowsDwmShadow.Apply(window);
        }
    }

    private static void Window_OnOpened(object? sender, EventArgs e)
    {
        if (sender is Window window)
            WindowsDwmShadow.Apply(window);
    }
}