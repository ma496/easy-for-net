namespace Backend.Tests.Fakes;

using System.Collections.Concurrent;
using Backend.Features.Identity.Core.Sessions;

/// <summary>
/// Which ended sessions the test host's <see cref="FaultingSessionEndedHandler"/> should fail for. A test
/// names only sessions it created, so the handler throws for that test alone.
/// </summary>
public sealed class SessionEndedHandlerFaults
{
    private readonly ConcurrentDictionary<string, byte> _failing = new(StringComparer.Ordinal);

    /// <summary>Makes the faulting handler throw whenever the session named is announced as ended.</summary>
    /// <param name="sessionId">The session.</param>
    public void FailFor(string sessionId) => _failing[sessionId] = 0;

    internal bool AnyFailing(IEnumerable<string> sessionIds) => sessionIds.Any(_failing.ContainsKey);
}

/// <summary>
/// An <see cref="ISessionEndedHandler"/> registered in the test host beside the real ones, which throws -
/// synchronously, the harsher case - when a session <see cref="SessionEndedHandlerFaults"/> names is
/// announced, so a test can show that a failing handler neither fails what ended the session nor stops the
/// other handlers.
/// </summary>
/// <param name="faults">Which sessions to fail for.</param>
public sealed class FaultingSessionEndedHandler(SessionEndedHandlerFaults faults) : ISessionEndedHandler
{
    /// <inheritdoc />
    public Task OnSessionsEndedAsync(IReadOnlyCollection<string> sessionIds, CancellationToken cancellationToken = default)
        => faults.AnyFailing(sessionIds)
            ? throw new InvalidOperationException("Session-ended handler failure injected by the test host.")
            : Task.CompletedTask;
}
