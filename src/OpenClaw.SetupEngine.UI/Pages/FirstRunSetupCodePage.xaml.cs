using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenClaw.Connection;
using OpenClaw.Shared;

namespace OpenClaw.SetupEngine.UI.Pages;

public sealed partial class FirstRunSetupCodePage : Page
{
    private bool _isConnecting;

    public FirstRunSetupCodePage()
    {
        InitializeComponent();
        SetupCodeBox.Focus(FocusState.Programmatic);
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e) =>
        AsyncEventHandlerGuard.Run(
            ConnectAsync,
            NullLogger.Instance,
            nameof(ConnectButton_Click));

    private async Task ConnectAsync()
    {
        if (_isConnecting)
            return;

        string code = SetupCodeBox.Text?.Trim() ?? string.Empty;
        if (code.Length == 0)
        {
            ShowError(FirstRunSetupCodeError.InvalidCode);
            return;
        }

        var setupWindow = SetupWindow.Active;
        if (setupWindow is null)
            return;

        _isConnecting = true;
        ConnectButton.IsEnabled = false;
        SetupCodeBox.IsEnabled = false;
        ConnectionProgress.IsActive = true;
        ConnectionProgress.Visibility = Visibility.Visible;
        ConnectionStatusText.Visibility = Visibility.Visible;
        ApprovalDetails.Visibility = Visibility.Collapsed;
        ContinueButton.Visibility = Visibility.Collapsed;
        SetStatusText("Onboarding_CodeOnly_Connecting");

        try
        {
            var progress = new Progress<FirstRunSetupCodeStatus>(ApplyStatus);
            await setupWindow.ConnectWithFirstRunSetupCodeAsync(
                code,
                progress,
                setupWindow.LifetimeToken);
        }
        catch (OperationCanceledException)
        {
            // Closing the setup window cancels its connection wait.
        }
        catch
        {
            ApplyStatus(new FirstRunSetupCodeStatus(
                FirstRunSetupCodeState.Error,
                Error: FirstRunSetupCodeError.ConnectionFailed));
        }
        finally
        {
            _isConnecting = false;
            ConnectionProgress.IsActive = false;
            ConnectionProgress.Visibility = Visibility.Collapsed;
            SetupCodeBox.IsEnabled = true;
            ConnectButton.IsEnabled = true;
        }
    }

    private void ApplyStatus(FirstRunSetupCodeStatus status)
    {
        if (!IsLoaded)
            return;

        switch (status.State)
        {
            case FirstRunSetupCodeState.Connecting:
                SetStatusText("Onboarding_CodeOnly_Connecting");
                break;
            case FirstRunSetupCodeState.WaitingForApproval:
                SetStatusText("Onboarding_CodeOnly_WaitingForApproval");
                ApprovalDetails.Visibility = Visibility.Visible;
                DeviceIdText.Text = SetupLocalization.Format(
                    "Onboarding_CodeOnly_DeviceId",
                    status.DeviceId ?? SetupLocalization.GetString("Onboarding_CodeOnly_DeviceIdUnavailable"));
                break;
            case FirstRunSetupCodeState.Connected:
                SetStatusText("Onboarding_CodeOnly_Connected");
                ApprovalDetails.Visibility = Visibility.Collapsed;
                ContinueButton.Visibility = Visibility.Visible;
                ConnectButton.Visibility = Visibility.Collapsed;
                break;
            case FirstRunSetupCodeState.Error:
                ShowError(status.Error ?? FirstRunSetupCodeError.ConnectionFailed);
                break;
        }
    }

    private void ShowError(FirstRunSetupCodeError error)
    {
        string resource = error switch
        {
            FirstRunSetupCodeError.ExpiredCode => "Onboarding_CodeOnly_ExpiredCode",
            FirstRunSetupCodeError.UnsafeGateway => "Onboarding_CodeOnly_UnsafeGateway",
            FirstRunSetupCodeError.InvalidCode => "Onboarding_CodeOnly_InvalidCode",
            _ => "Onboarding_CodeOnly_ConnectionFailed",
        };
        SetStatusText(resource);
        ApprovalDetails.Visibility = Visibility.Collapsed;
        ConnectButton.Visibility = Visibility.Visible;
        ContinueButton.Visibility = Visibility.Collapsed;
    }

    private void SetStatusText(string resourceKey)
    {
        ConnectionStatusText.Text = SetupLocalization.GetString(resourceKey);
        ConnectionStatusText.Visibility = Visibility.Visible;
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e) => SetupWindow.Active?.Close();
}
