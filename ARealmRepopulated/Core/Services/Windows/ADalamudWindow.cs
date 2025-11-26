using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARealmRepopulated.Core.Services.Windows;

public abstract class ADalamudWindow : Window
{

    public event Action? OnWindowClosed;
    public event Action? OnWindowOpened;
    public event Action? OnWindowSafeToRemove;

    public ADalamudWindow(string windowName) : base(windowName)
    {
        this.SetWindowOptions();
    }

    public override void OnSafeToRemove()
    {
        base.OnSafeToRemove();
        OnWindowSafeToRemove?.Invoke();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        OnWindowOpened?.Invoke();
    }

    public override void OnClose()
    {
        base.OnClose();
        OnWindowClosed?.Invoke();
    }

    protected abstract void SetWindowOptions();
}

public static class DalamudWindowExtension
{
    public static IServiceCollection AddWindow<T>(this IServiceCollection collection) where T : ADalamudWindow
    {        
        collection.AddSingleton((sp) => {            
            var window = ActivatorUtilities.CreateInstance<T>(sp);
            sp.GetRequiredService<WindowSystem>().AddWindow(window);
            return window;
        });

        return collection;
    }

    public static IServiceCollection AddTransientWindow<T>(this IServiceCollection collection) where T : ADalamudWindow
    {        
        collection.AddTransient((sp) =>
        {
            var window = ActivatorUtilities.CreateInstance<T>(sp);
            var windowSystem = sp.GetRequiredService<WindowSystem>();
            window.OnWindowSafeToRemove += () =>
            {
                windowSystem.RemoveWindow(window);
            };
            windowSystem.AddWindow(window);
            return window;
        });
        return collection;

    }
}
