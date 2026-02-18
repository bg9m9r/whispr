using Whispr.Core.Protocol;

namespace Whispr.Client.Services;

/// <summary>
/// Result of a connect attempt.
/// </summary>
public abstract record ConnectOutcome;

/// <summary>
/// Connect succeeded. Caller owns ConnectionService and must dispose it.
/// </summary>
public sealed record ConnectSuccess(
    ConnectionService Connection,
    AuthService Auth,
    RoomJoinedResult RoomJoined,
    ServerStatePayload ServerState) : ConnectOutcome;

/// <summary>
/// Connect failed with an error message.
/// </summary>
/// <param name="Error">Error message to display.</param>
/// <param name="IsCertificateError">When true, user may retry with acceptUnverifiedCert.</param>
public sealed record ConnectFailed(string Error, bool IsCertificateError = false) : ConnectOutcome;
