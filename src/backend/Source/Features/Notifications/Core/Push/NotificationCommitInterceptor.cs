namespace Backend.Features.Notifications.Core.Push;

using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Holds pushes raised inside an open <see cref="AppDbContext"/> transaction until that transaction
/// commits, and drops them when it rolls back or fails, so a connection is never told about a notification
/// the database does not hold.
/// </summary>
/// <remarks>
/// <para>
/// It is one singleton interceptor added to every <see cref="AppDbContext"/> (Program.cs adds every
/// registered <see cref="IInterceptor"/>), so the pending sends are kept per context in a
/// <see cref="ConditionalWeakTable{TKey,TValue}"/> and tagged with the transaction they were raised in.
/// Only a commit of that same transaction sends them. A transaction disposed without committing fires no
/// commit and leaves its sends behind, where the next transaction on the context replaces them and the
/// context's collection releases them - so "disposed without commit" drops them exactly as a rollback does.
/// </para>
/// <para>
/// What is deferred is a ready-built send over the singleton <see cref="INotificationHubSender"/>, never
/// work on the scoped context: the commit hook may run after the request that raised it has moved on, and
/// the synchronous <c>Commit()</c> hook starts the sends without waiting for them. Two cases are outside
/// what EF tells an interceptor and are not covered: a <c>System.Transactions</c> ambient scope (no EF
/// transaction is open, so the push goes out straight after the save), and a rollback to a savepoint
/// inside a transaction that then commits (the push goes out on the commit). A context that joins another
/// context's transaction through <c>UseTransaction</c> has its pushes dropped - raise on the context that
/// owns the transaction.
/// </para>
/// </remarks>
/// <param name="logger">Logs a deferred send that fails; a failure never reaches the commit.</param>
public sealed class NotificationCommitInterceptor(ILogger<NotificationCommitInterceptor> logger) : DbTransactionInterceptor
{
    private readonly ConditionalWeakTable<DbContext, PendingSends> _pending = new();

    /// <summary>
    /// Queues a send until the named transaction on the context commits.
    /// </summary>
    /// <param name="context">The context whose transaction is open.</param>
    /// <param name="transactionId">EF's identifier of that transaction.</param>
    /// <param name="send">The send, which must reach no scoped service.</param>
    internal void Defer(DbContext context, Guid transactionId, Func<Task> send)
    {
        var pending = _pending.GetOrCreateValue(context);
        lock (pending)
        {
            // Sends left from an earlier transaction on this context were never committed - it was disposed
            // without a commit - so they are discarded here rather than sent with this one.
            if (pending.TransactionId != transactionId)
            {
                pending.Sends.Clear();
                pending.TransactionId = transactionId;
            }

            pending.Sends.Add(send);
        }
    }

    /// <inheritdoc />
    public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        foreach (var send in Take(eventData))
        {
            await RunAsync(send);
        }
    }

    /// <inheritdoc />
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        // A synchronous commit cannot await a network send without blocking a thread on it, so the sends
        // are started and left to finish; each logs its own failure.
        foreach (var send in Take(eventData))
        {
            _ = RunAsync(send);
        }
    }

    /// <inheritdoc />
    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        => Take(eventData);

    /// <inheritdoc />
    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Take(eventData);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
        => Take(eventData);

    /// <inheritdoc />
    public override Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Take(eventData);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes and returns the sends queued against the transaction that just ended.
    /// </summary>
    private List<Func<Task>> Take(TransactionEventData eventData)
    {
        if (eventData.Context is not { } context || !_pending.TryGetValue(context, out var pending))
        {
            return [];
        }

        lock (pending)
        {
            if (pending.TransactionId != eventData.TransactionId)
            {
                return [];
            }

            var sends = pending.Sends.ToList();
            pending.Sends.Clear();
            return sends;
        }
    }

    /// <summary>
    /// Runs one deferred send, logging rather than throwing: the transaction has already committed, and a
    /// push that could not be delivered must not turn a committed change into a failed request.
    /// </summary>
    private async Task RunAsync(Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "A notification push deferred until commit could not be sent.");
        }
    }

    /// <summary>
    /// The sends one context holds for its open transaction.
    /// </summary>
    private sealed class PendingSends
    {
        public Guid TransactionId { get; set; }

        public List<Func<Task>> Sends { get; } = [];
    }
}
