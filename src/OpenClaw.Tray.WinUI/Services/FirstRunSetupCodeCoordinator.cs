using OpenClaw.Connection;

namespace OpenClawTray.Services;

internal sealed class FirstRunSetupCodeCoordinator
{
    private readonly GatewayConnectionManager _connectionManager;

    public FirstRunSetupCodeCoordinator(GatewayConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task ConnectAsync(
        string setupCode,
        IProgress<FirstRunSetupCodeStatus> progress,
        CancellationToken cancellationToken)
    {
        var decoded = FirstRunSetupCodeDecoder.Decode(setupCode);
        if (!decoded.Success)
        {
            var error = decoded.Error?.Contains("expired", StringComparison.OrdinalIgnoreCase) == true
                ? FirstRunSetupCodeError.ExpiredCode
                : decoded.Error?.Contains("public secure gateway", StringComparison.OrdinalIgnoreCase) == true
                    ? FirstRunSetupCodeError.UnsafeGateway
                    : FirstRunSetupCodeError.InvalidCode;
            progress.Report(new FirstRunSetupCodeStatus(FirstRunSetupCodeState.Error, Error: error));
            return;
        }

        progress.Report(new FirstRunSetupCodeStatus(FirstRunSetupCodeState.Connecting));
        var completion = new TaskCompletionSource<FirstRunSetupCodeStatus>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        string? expectedGatewayId = null;

        void OnStateChanged(object? sender, GatewayConnectionSnapshot snapshot)
        {
            if (expectedGatewayId is null ||
                !string.Equals(snapshot.GatewayId, expectedGatewayId, StringComparison.Ordinal))
            {
                return;
            }

            ObserveSnapshot(snapshot, expectedGatewayId, completion, progress);
        }

        _connectionManager.StateChanged += OnStateChanged;
        try
        {
            var result = await _connectionManager.ApplySetupCodeAsync(setupCode).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.Outcome != SetupCodeOutcome.Success)
            {
                progress.Report(new FirstRunSetupCodeStatus(
                    FirstRunSetupCodeState.Error,
                    Error: FirstRunSetupCodeError.ConnectionFailed));
                return;
            }

            var snapshot = _connectionManager.CurrentSnapshot;
            expectedGatewayId = snapshot.GatewayId;
            if (string.IsNullOrWhiteSpace(expectedGatewayId))
            {
                progress.Report(new FirstRunSetupCodeStatus(
                    FirstRunSetupCodeState.Error,
                    Error: FirstRunSetupCodeError.ConnectionFailed));
                return;
            }

            ObserveSnapshot(snapshot, expectedGatewayId, completion, progress);
            if (completion.Task.IsCompleted)
            {
                progress.Report(await completion.Task.ConfigureAwait(false));
                return;
            }

            using var registration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));
            var finalStatus = await completion.Task.ConfigureAwait(false);
            progress.Report(finalStatus);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            progress.Report(new FirstRunSetupCodeStatus(
                FirstRunSetupCodeState.Error,
                Error: FirstRunSetupCodeError.ConnectionFailed));
        }
        finally
        {
            _connectionManager.StateChanged -= OnStateChanged;
        }
    }

    private static void ObserveSnapshot(
        GatewayConnectionSnapshot snapshot,
        string expectedGatewayId,
        TaskCompletionSource<FirstRunSetupCodeStatus> completion,
        IProgress<FirstRunSetupCodeStatus> progress)
    {
        var status = FirstRunSetupCodeConnectionPolicy.Project(snapshot, expectedGatewayId);
        if (status?.State == FirstRunSetupCodeState.WaitingForApproval)
        {
            progress.Report(status);
        }
        else if (status is not null)
        {
            completion.TrySetResult(status);
        }
    }
}
