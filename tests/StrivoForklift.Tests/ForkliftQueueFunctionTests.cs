using Microsoft.Extensions.Logging;
using Xunit;

namespace StrivoForklift.Tests;

/// <summary>
/// Minimal ILogger implementation that captures log entries so tests can assert on them.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public readonly List<(LogLevel Level, string Message)> Entries = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception)));
}

public class ForkliftQueueFunctionTests
{
    /// <summary>Builds a valid 3-line raw queue message string.</summary>
    private static string BuildRawMessage(Guid transactionId, string source, string accountId, string message, string timestamp)
        => $"{transactionId}\n{{\"source\":\"{source}\",\"Id\":\"{accountId}\",\"Message\":\"{message}\"}}\n{timestamp}";

    [Fact]
    public async Task Run_ValidMessage_LogsDequeueDetails()
    {
        // Arrange
        var logger = new CapturingLogger<ForkliftQueueFunction>();
        var function = new ForkliftQueueFunction(logger);
        var guid = Guid.NewGuid();
        var rawMessage = BuildRawMessage(guid, "fake_bank_transactions_1000.csv", "tx0001",
            "Direct debit SEK 97.77 (Internet subscription)", "3/17/2026, 12:42:55 PM");

        // Act
        await function.Run(rawMessage);

        // Assert – a dequeue log entry containing the transaction id must exist
        Assert.Contains(logger.Entries,
            e => e.Level == LogLevel.Information && e.Message.Contains(guid.ToString()));
        // No warnings should have been emitted for a well-formed message
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Run_MultipleDistinctMessages_EachLogged()
    {
        // Arrange
        var logger = new CapturingLogger<ForkliftQueueFunction>();
        var function = new ForkliftQueueFunction(logger);
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();

        // Act
        await function.Run(BuildRawMessage(guid1, "test.csv", "tx0001", "Payment A", "3/17/2026, 12:42:55 PM"));
        await function.Run(BuildRawMessage(guid2, "test.csv", "tx0002", "Payment B", "3/17/2026, 1:00:00 PM"));

        // Assert – both transaction ids appear in log output
        Assert.Contains(logger.Entries,
            e => e.Level == LogLevel.Information && e.Message.Contains(guid1.ToString()));
        Assert.Contains(logger.Entries,
            e => e.Level == LogLevel.Information && e.Message.Contains(guid2.ToString()));
    }

    [Fact]
    public async Task Run_DuplicateTransactionId_BothLogged()
    {
        // Arrange – with DB ops disabled, the same message is simply logged twice
        var logger = new CapturingLogger<ForkliftQueueFunction>();
        var function = new ForkliftQueueFunction(logger);
        var guid = Guid.NewGuid();
        var rawMessage = BuildRawMessage(guid, "test.csv", "tx0001", "Payment A", "3/17/2026, 12:42:55 PM");

        // Act – send the same GUID twice
        await function.Run(rawMessage);
        await function.Run(rawMessage);

        // Assert – the dequeue detail log (the structured "Dequeued message —" line) appears once per invocation
        var detailLogs = logger.Entries
            .Where(e => e.Level == LogLevel.Information && e.Message.StartsWith("Dequeued message"))
            .ToList();
        Assert.Equal(2, detailLogs.Count);
    }

    [Fact]
    public async Task Run_MalformedMessage_LogsWarningAndSkips()
    {
        // Arrange
        var logger = new CapturingLogger<ForkliftQueueFunction>();
        var function = new ForkliftQueueFunction(logger);

        // Act – only 1 line, not 3
        await function.Run("only-one-line");

        // Assert – a warning is logged and no informational dequeue entry is present
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.DoesNotContain(logger.Entries,
            e => e.Level == LogLevel.Information && e.Message.Contains("Dequeued message"));
    }

    [Fact]
    public async Task Run_InvalidGuid_LogsWarningAndSkips()
    {
        // Arrange
        var logger = new CapturingLogger<ForkliftQueueFunction>();
        var function = new ForkliftQueueFunction(logger);
        var rawMessage = "not-a-guid\n{\"source\":\"test.csv\",\"Id\":\"tx0001\",\"Message\":\"Payment\"}\n3/17/2026, 12:42:55 PM";

        // Act
        await function.Run(rawMessage);

        // Assert
        Assert.Contains(logger.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("not-a-guid"));
    }
}
