namespace OpenClaw.SetupEngine.UI.Pages;

/// <summary>Locally selected Windows node capabilities for first-run enrollment.</summary>
public sealed record FirstRunNodeCapabilities(
    bool Canvas,
    bool SystemRun,
    bool Screen,
    bool Camera,
    bool Location,
    bool TextToSpeech,
    bool SpeechToText,
    bool LocalModelAccess);
