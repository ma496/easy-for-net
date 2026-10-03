namespace Backend.Middleware;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

/// <summary>
/// The development console format: an Information line is its message alone, as the web app's dev
/// server prints a request; a warning or error keeps its level, category and exception so it stands out.
/// </summary>
public sealed class DevelopmentConsoleFormatter() : ConsoleFormatter(FormatterName)
{
    /// <summary>The name the formatter is registered and selected under.</summary>
    public const string FormatterName = "development";

    /// <inheritdoc />
    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        if (string.IsNullOrEmpty(message) && logEntry.Exception == null)
            return;

        if (logEntry.LogLevel < LogLevel.Warning)
        {
            textWriter.WriteLine(message);
            return;
        }

        var level = logEntry.LogLevel switch
        {
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            _ => "crit",
        };
        textWriter.WriteLine($"{level}: {logEntry.Category}: {message}");
        if (logEntry.Exception != null)
            textWriter.WriteLine(logEntry.Exception.ToString());
    }
}
