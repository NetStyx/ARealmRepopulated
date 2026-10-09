using Dalamud.Plugin.Services;
using System;
using System.Reflection;

namespace ARealmRepopulated.Tests.Infrastructure;

internal class NullPluginLog : DispatchProxy {
    public static readonly IPluginLog Instance = Create<IPluginLog, NullPluginLog>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        => targetMethod!.ReturnType.IsValueType && targetMethod.ReturnType != typeof(void)
            ? Activator.CreateInstance(targetMethod.ReturnType)
            : null;
}
