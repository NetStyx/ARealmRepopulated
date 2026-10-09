using Dalamud.Plugin.Services;
using Serilog;
using Serilog.Events;
using System;

namespace ARealmRepopulated.Tests.Infrastructure;

/// <summary>
/// Swallows every log call.
/// </summary>
internal class NullPluginLog : IPluginLog {
    public static readonly IPluginLog Instance = new NullPluginLog();

    public ILogger Logger => Serilog.Core.Logger.None;
    public LogEventLevel MinimumLogLevel { get; set; } = LogEventLevel.Fatal;

    public void Fatal(string messageTemplate, params object[] values) { }
    public void Fatal(Exception? exception, string messageTemplate, params object[] values) { }
    public void Error(string messageTemplate, params object[] values) { }
    public void Error(Exception? exception, string messageTemplate, params object[] values) { }
    public void Warning(string messageTemplate, params object[] values) { }
    public void Warning(Exception? exception, string messageTemplate, params object[] values) { }
    public void Information(string messageTemplate, params object[] values) { }
    public void Information(Exception? exception, string messageTemplate, params object[] values) { }
    public void Info(string messageTemplate, params object[] values) { }
    public void Info(Exception? exception, string messageTemplate, params object[] values) { }
    public void Debug(string messageTemplate, params object[] values) { }
    public void Debug(Exception? exception, string messageTemplate, params object[] values) { }
    public void Verbose(string messageTemplate, params object[] values) { }
    public void Verbose(Exception? exception, string messageTemplate, params object[] values) { }
    public void Write(LogEventLevel level, Exception? exception, string messageTemplate, params object[] values) { }
}
