using OpenClaw.Shared;

namespace OpenClaw.Connection;

public static class FirstRunSetupCodeConnectionPolicy
{
    public static FirstRunSetupCodeStatus? Project(
        GatewayConnectionSnapshot snapshot,
        string expectedGatewayId)
    {
        if (!string.Equals(snapshot.GatewayId, expectedGatewayId, StringComparison.Ordinal))
            return null;

        if (snapshot.OperatorState == RoleConnectionState.Connected &&
            snapshot.NodeState == RoleConnectionState.Connected &&
            snapshot.NodePairingStatus == PairingStatus.Paired)
        {
            return new FirstRunSetupCodeStatus(FirstRunSetupCodeState.Connected);
        }

        if (snapshot.NodePairingStatus == PairingStatus.Rejected ||
            snapshot.NodeState is RoleConnectionState.Error or RoleConnectionState.PairingRejected ||
            snapshot.OperatorState is RoleConnectionState.Error or RoleConnectionState.PairingRejected)
        {
            return new FirstRunSetupCodeStatus(
                FirstRunSetupCodeState.Error,
                Error: FirstRunSetupCodeError.ConnectionFailed);
        }

        if (snapshot.NodePairingStatus == PairingStatus.Pending ||
            snapshot.NodeState == RoleConnectionState.PairingRequired)
        {
            return new FirstRunSetupCodeStatus(
                FirstRunSetupCodeState.WaitingForApproval,
                snapshot.NodeDeviceId);
        }

        return null;
    }
}
