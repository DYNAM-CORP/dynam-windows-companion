using OpenClaw.Connection;
using OpenClaw.Shared;

namespace OpenClaw.Connection.Tests;

public sealed class FirstRunSetupCodeConnectionPolicyTests
{
    [Fact]
    public void Project_PendingSnapshot_UsesDeviceIdFromMatchingGateway()
    {
        var snapshot = new GatewayConnectionSnapshot
        {
            GatewayId = "new-gateway",
            NodeState = RoleConnectionState.PairingRequired,
            NodePairingStatus = PairingStatus.Pending,
            NodeDeviceId = "public-device-id-for-new-gateway",
        };

        var status = FirstRunSetupCodeConnectionPolicy.Project(snapshot, "new-gateway");

        Assert.NotNull(status);
        Assert.Equal(FirstRunSetupCodeState.WaitingForApproval, status.State);
        Assert.Equal("public-device-id-for-new-gateway", status.DeviceId);
    }

    [Fact]
    public void Project_IgnoresSnapshotFromPreviouslyActiveGateway()
    {
        var snapshot = new GatewayConnectionSnapshot
        {
            GatewayId = "previous-gateway",
            NodeState = RoleConnectionState.PairingRequired,
            NodePairingStatus = PairingStatus.Pending,
            NodeDeviceId = "legacy-debug-page-id",
        };

        var status = FirstRunSetupCodeConnectionPolicy.Project(snapshot, "new-gateway");

        Assert.Null(status);
    }

    [Fact]
    public void Project_PairedSnapshotCompletesAndRejectedSnapshotFails()
    {
        var paired = new GatewayConnectionSnapshot
        {
            GatewayId = "new-gateway",
            NodeState = RoleConnectionState.Connected,
            NodePairingStatus = PairingStatus.Paired,
        };
        var rejected = paired with
        {
            NodeState = RoleConnectionState.PairingRejected,
            NodePairingStatus = PairingStatus.Rejected,
        };

        Assert.Equal(
            FirstRunSetupCodeState.Connected,
            FirstRunSetupCodeConnectionPolicy.Project(paired, "new-gateway")?.State);
        Assert.Equal(
            FirstRunSetupCodeState.Error,
            FirstRunSetupCodeConnectionPolicy.Project(rejected, "new-gateway")?.State);
    }
}
