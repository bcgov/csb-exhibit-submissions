using Microsoft.Extensions.Logging;

namespace CES.API.Tests.Fixtures;

/// <summary>
/// Records everything written to it so a test can assert on what was — and, more
/// importantly for the auth path, what was <em>not</em> — logged.
/// </summary>
public class CapturingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = new();

    /// <summary>Every captured message joined, for "this substring appears nowhere" assertions.</summary>
    public string AllText => string.Join(Environment.NewLine, Messages);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));

        if (exception is not null)
            Messages.Add(exception.ToString());
    }
}
