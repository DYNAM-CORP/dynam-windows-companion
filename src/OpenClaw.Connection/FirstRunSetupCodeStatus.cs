namespace OpenClaw.Connection;

public enum FirstRunSetupCodeState
{
    Connecting,
    WaitingForApproval,
    Connected,
    Error,
}

public enum FirstRunSetupCodeError
{
    InvalidCode,
    ExpiredCode,
    UnsafeGateway,
    ConnectionFailed,
}

public sealed record FirstRunSetupCodeStatus(
    FirstRunSetupCodeState State,
    string? DeviceId = null,
    FirstRunSetupCodeError? Error = null);
