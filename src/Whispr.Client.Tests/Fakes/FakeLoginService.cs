using System.Collections.Generic;
using Whispr.Client.Services;
using Whispr.Core.Protocol;

namespace Whispr.Client.Tests.Fakes;

/// <summary>
/// Configurable ILoginService for unit testing.
/// </summary>
public sealed class FakeLoginService : ILoginService
{
    private readonly Queue<ConnectOutcome> _outcomes = new();

    /// <summary>
    /// Set the outcome to return from ConnectAsync.
    /// </summary>
    public void SetOutcome(ConnectOutcome outcome)
    {
        _outcomes.Clear();
        _outcomes.Enqueue(outcome);
    }

    /// <summary>
    /// Add outcomes to return in sequence (e.g. failure then success for retry).
    /// </summary>
    public void AddOutcome(ConnectOutcome outcome) => _outcomes.Enqueue(outcome);

    /// <summary>
    /// Configure to return success with disposable connection and auth.
    /// </summary>
    public void SetSuccess(RoomJoinedResult roomJoined, ServerStatePayload serverState)
    {
        _outcomes.Clear();
        var connection = new ConnectionService();
        var auth = new AuthService(connection);
        _outcomes.Enqueue(new ConnectSuccess(connection, auth, roomJoined, serverState));
    }

    /// <summary>
    /// Configure to return failure.
    /// </summary>
    public void SetFailure(string error, bool isCertificateError = false)
    {
        _outcomes.Clear();
        _outcomes.Enqueue(new ConnectFailed(error, isCertificateError));
    }

    public Task<ConnectOutcome> ConnectAsync(ConnectParams parameters, CancellationToken ct = default)
    {
        if (_outcomes.Count == 0)
            throw new InvalidOperationException("FakeLoginService outcome not set. Call SetOutcome, SetSuccess, SetFailure, or AddOutcome first.");
        return Task.FromResult(_outcomes.Dequeue());
    }
}
