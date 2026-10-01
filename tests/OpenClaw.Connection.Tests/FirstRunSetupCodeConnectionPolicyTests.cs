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
            OperatorState = RoleConnectionState.Connected,
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

    [Theory]
    [InlineData(RoleConnectionState.Idle)]
    [InlineData(RoleConnectionState.Connecting)]
    [InlineData(RoleConnectionState.RateLimited)]
    public void Project_PairedNodeWaitsForOperatorConnection(RoleConnectionState operatorState)
    {
        var snapshot = new GatewayConnectionSnapshot
        {
            GatewayId = "new-gateway",
            OperatorState = operatorState,
            NodeState = RoleConnectionState.Connected,
            NodePairingStatus = PairingStatus.Paired,
        };

        Assert.Null(FirstRunSetupCodeConnectionPolicy.Project(snapshot, "new-gateway"));
        Assert.Equal(
            FirstRunSetupCodeState.Connected,
            FirstRunSetupCodeConnectionPolicy.Project(
                snapshot with { OperatorState = RoleConnectionState.Connected }, "new-gateway")?.State);
    }

    [Theory]
    [InlineData(RoleConnectionState.Error)]
    [InlineData(RoleConnectionState.PairingRejected)]
    public void Project_PairedNodeDoesNotHideOperatorFailure(RoleConnectionState operatorState)
    {
        var snapshot = new GatewayConnectionSnapshot
        {
            GatewayId = "new-gateway",
            OperatorState = operatorState,
            NodeState = RoleConnectionState.Connected,
            NodePairingStatus = PairingStatus.Paired,
        };

        var status = FirstRunSetupCodeConnectionPolicy.Project(snapshot, "new-gateway");

        Assert.Equal(FirstRunSetupCodeState.Error, status?.State);
        Assert.Equal(FirstRunSetupCodeError.ConnectionFailed, status?.Error);
    }
}
