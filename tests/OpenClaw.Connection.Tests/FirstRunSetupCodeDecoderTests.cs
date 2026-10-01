using System.Text;
using OpenClaw.Connection;

namespace OpenClaw.Connection.Tests;

public sealed class FirstRunSetupCodeDecoderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000);

    [Fact]
    public void Decode_AcceptsShortLivedPublicWssBootstrapCode()
    {
        var decoded = FirstRunSetupCodeDecoder.Decode(
            BuildCode("wss://companion-main.dynamconsulting.com", "bootstrap-secret", Now.AddMinutes(10)),
            Now);

        Assert.True(decoded.Success);
        Assert.Equal("wss://companion-main.dynamconsulting.com", decoded.Url);
        Assert.Equal("bootstrap-secret", decoded.BootstrapToken);
        Assert.Equal(Now.AddMinutes(10), decoded.ExpiresAt);
    }

    [Theory]
    [InlineData("ws://companion-main.dynamconsulting.com")]
    [InlineData("wss://localhost:18789")]
    [InlineData("wss://127.0.0.1:18789")]
    [InlineData("wss://host.docker.internal:18789")]
    [InlineData("wss://gateway.docker.internal:18789")]
    [InlineData("wss://gateway.local:18789")]
    [InlineData("wss://gateway.home.arpa:18789")]
    [InlineData("wss://gateway.lan:18789")]
    public void Decode_RejectsInsecureOrPrivateGatewayAddresses(string url)
    {
        var decoded = FirstRunSetupCodeDecoder.Decode(
            BuildCode(url, "bootstrap-secret", Now.AddMinutes(10)),
            Now);

        Assert.False(decoded.Success);
        Assert.Null(decoded.BootstrapToken);
    }

    [Fact]
    public void Decode_RejectsExpiredCode()
    {
        var decoded = FirstRunSetupCodeDecoder.Decode(
            BuildCode("wss://companion-main.dynamconsulting.com", "bootstrap-secret", Now),
            Now);

        Assert.False(decoded.Success);
        Assert.Contains("expired", decoded.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decode_RejectsExpiryBeyondShortLifetime()
    {
        var decoded = FirstRunSetupCodeDecoder.Decode(
            BuildCode("wss://companion-main.dynamconsulting.com", "bootstrap-secret", Now.AddHours(1)),
            Now);

        Assert.False(decoded.Success);
    }

    [Fact]
    public void Decode_RejectsSharedTokenFieldsAndUnknownProperties()
    {
        var code = Encode($"{{\"url\":\"wss://companion-main.dynamconsulting.com\",\"bootstrapToken\":\"boot\",\"expiresAtMs\":{Now.AddMinutes(5).ToUnixTimeMilliseconds()},\"token\":\"shared\"}}");

        var decoded = FirstRunSetupCodeDecoder.Decode(code, Now);

        Assert.False(decoded.Success);
        Assert.Null(decoded.BootstrapToken);
    }

    private static string BuildCode(string url, string token, DateTimeOffset expiresAt) => Encode(
        $"{{\"url\":{System.Text.Json.JsonSerializer.Serialize(url)},\"bootstrapToken\":{System.Text.Json.JsonSerializer.Serialize(token)},\"expiresAtMs\":{expiresAt.ToUnixTimeMilliseconds()}}}");

    private static string Encode(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
