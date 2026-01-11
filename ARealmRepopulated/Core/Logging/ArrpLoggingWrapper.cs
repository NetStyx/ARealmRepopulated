using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace ARealmRepopulated.Core.Logging;

// for now, i only need it for the localization system. so .. lets keep it simple.

public class ArrpLogProvider(IPluginLog pluginLog) : ILoggerProvider {
    public ILogger CreateLogger(string _) => new ArrpLogWrapper(pluginLog);
    public void Dispose() => GC.SuppressFinalize(this);
}

public class ArrpLogWrapper(IPluginLog pluginLog) : ILogger {

    public bool IsEnabled(LogLevel logLevel) => true;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        var msg = formatter(state, exception);
        switch (logLevel) {
            default:
            case LogLevel.None:
            case LogLevel.Trace:
                pluginLog.Verbose(msg);
                break;
            case LogLevel.Debug:
                pluginLog.Debug(msg);
                break;
            case LogLevel.Information:
                pluginLog.Information(msg);
                break;
            case LogLevel.Warning:
                pluginLog.Warning(msg);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                pluginLog.Error(msg);
                break;
        }
    }
}

