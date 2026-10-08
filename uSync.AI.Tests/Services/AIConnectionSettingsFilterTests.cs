using System.Text.Json;
using Umbraco.AI.Core.EditableModels;
using uSync.AI.Sync;
using uSync.AI.Sync.Services;

namespace uSync.AI.Tests.Services;

[TestFixture]
public class AIConnectionSettingsFilterTests
{
    private sealed class FakeProviderSettings
    {
        [AIField(IsSensitive = true)]
        public string? ApiKey { get; set; }

        public string? Endpoint { get; set; }

        public string? Organization { get; set; }
    }

    private static FakeProviderSettings Settings(string apiKey = "ENC:abc123") => new()
    {
        ApiKey = apiKey,
        Endpoint = "https://api.example.com",
        Organization = "jumoo",
    };

    [Test]
    public void Defaults_LeaveOutEncryptedValues()
    {
        var (settings, ignored) = AIConnectionSettingsFilter.Filter(Settings(), typeof(FakeProviderSettings), new uSyncAIConnectionOptions());

        Assert.Multiple(() =>
        {
            Assert.That(settings!.ContainsKey("ApiKey"), Is.False);
            Assert.That(settings["Endpoint"]!.GetValue<string>(), Is.EqualTo("https://api.example.com"));
            Assert.That(ignored, Is.EqualTo(new[] { "ApiKey" }));
            Assert.That(settings.ToJsonString(), Does.Not.Contain("ENC:"));
        });
    }

    [Test]
    public void Defaults_LeaveOutPlainTextSecrets()
    {
        // Umbraco.AI hands out decrypted settings, so this is the case that matters most.
        var (settings, ignored) = AIConnectionSettingsFilter.Filter(
            Settings("sk-live-plain-text"), typeof(FakeProviderSettings), new uSyncAIConnectionOptions());

        Assert.Multiple(() =>
        {
            Assert.That(settings!.ToJsonString(), Does.Not.Contain("sk-live-plain-text"));
            Assert.That(settings.Select(x => x.Key), Is.EquivalentTo(new[] { "Endpoint", "Organization" }));
            Assert.That(ignored, Is.EqualTo(new[] { "ApiKey" }));
        });
    }

    [Test]
    public void Defaults_MatchSensitiveFieldsWhateverTheirCasing()
    {
        // settings posted through the management API arrive camelCased
        var stored = JsonSerializer.SerializeToElement(new { apiKey = "sk-live-plain-text", endpoint = "https://api.example.com" });

        var (settings, ignored) = AIConnectionSettingsFilter.Filter(stored, typeof(FakeProviderSettings), new uSyncAIConnectionOptions());

        Assert.Multiple(() =>
        {
            Assert.That(settings!.Select(x => x.Key), Is.EqualTo(new[] { "endpoint" }));
            Assert.That(ignored, Is.EqualTo(new[] { "apiKey" }));
        });
    }

    [Test]
    public void Defaults_UnknownProvider_TreatsEverySettingAsSensitive()
    {
        var (settings, ignored) = AIConnectionSettingsFilter.Filter(Settings("sk-live-plain-text"), null, new uSyncAIConnectionOptions());

        Assert.Multiple(() =>
        {
            Assert.That(settings!, Is.Empty);
            Assert.That(ignored, Is.EqualTo(new[] { "ApiKey", "Endpoint", "Organization" }));
        });
    }

    [Test]
    public void Defaults_KeepAnEmptySensitiveField()
    {
        var (settings, ignored) = AIConnectionSettingsFilter.Filter(Settings(""), typeof(FakeProviderSettings), new uSyncAIConnectionOptions());

        Assert.Multiple(() =>
        {
            Assert.That(settings!.ContainsKey("ApiKey"), Is.True);
            Assert.That(ignored, Is.Empty);
        });
    }

    [Test]
    public void Defaults_KeepConfigurationReferences()
    {
        var (settings, ignored) = AIConnectionSettingsFilter.Filter(
            Settings("$Umbraco:AI:Secrets:OpenAIApiKey"), typeof(FakeProviderSettings), new uSyncAIConnectionOptions());

        Assert.Multiple(() =>
        {
            Assert.That(settings!["ApiKey"]!.GetValue<string>(), Is.EqualTo("$Umbraco:AI:Secrets:OpenAIApiKey"));
            Assert.That(ignored, Is.Empty);
        });
    }

    [Test]
    public void IgnoreSecretValuesOff_KeepsEverything()
    {
        var (settings, ignored) = AIConnectionSettingsFilter.Filter(
            Settings(), typeof(FakeProviderSettings), new uSyncAIConnectionOptions { IgnoreSecretValues = false });

        Assert.Multiple(() =>
        {
            Assert.That(settings!["ApiKey"]!.GetValue<string>(), Is.EqualTo("ENC:abc123"));
            Assert.That(ignored, Is.Empty);
        });
    }

    [Test]
    public void IgnoreSensitive_LeavesOutSensitiveFieldsEvenWhenTheyAreReferences()
    {
        var (settings, ignored) = AIConnectionSettingsFilter.Filter(
            Settings("$Umbraco:AI:Secrets:OpenAIApiKey"), typeof(FakeProviderSettings), new uSyncAIConnectionOptions { IgnoreSensitive = true });

        Assert.Multiple(() =>
        {
            Assert.That(settings!.ContainsKey("ApiKey"), Is.False);
            Assert.That(settings.ContainsKey("Endpoint"), Is.True);
            Assert.That(ignored, Is.EqualTo(new[] { "ApiKey" }));
        });
    }

    [Test]
    public void IgnoreSettings_AlwaysLeavesOutNamedFields()
    {
        var (settings, ignored) = AIConnectionSettingsFilter.Filter(
            Settings("plain"), typeof(FakeProviderSettings),
            new uSyncAIConnectionOptions { IgnoreSecretValues = false, IgnoreSettings = ["organization", "Endpoint"] });

        Assert.Multiple(() =>
        {
            Assert.That(settings!.Select(x => x.Key), Is.EqualTo(new[] { "ApiKey" }));
            Assert.That(ignored, Is.EqualTo(new[] { "Endpoint", "Organization" }));
        });
    }

    [Test]
    public void Filter_HandlesSettingsStoredAsJson()
    {
        var stored = JsonSerializer.SerializeToElement(Settings());

        var (settings, ignored) = AIConnectionSettingsFilter.Filter(stored, typeof(FakeProviderSettings), new uSyncAIConnectionOptions());

        Assert.Multiple(() =>
        {
            Assert.That(settings!.ContainsKey("ApiKey"), Is.False);
            Assert.That(ignored, Is.EqualTo(new[] { "ApiKey" }));
        });
    }

    [Test]
    public void Filter_NullSettings_ReturnsNothing()
    {
        var (settings, ignored) = AIConnectionSettingsFilter.Filter(null, typeof(FakeProviderSettings), new uSyncAIConnectionOptions());

        Assert.Multiple(() =>
        {
            Assert.That(settings, Is.Null);
            Assert.That(ignored, Is.Empty);
        });
    }

    [Test]
    public void Merge_KeepsTheTargetsValueForIgnoredSettings()
    {
        var incoming = JsonSerializer.SerializeToElement(new { Endpoint = "https://new.example.com" });

        // the name is as the source file spelled it; the casing on the target may differ
        var merged = AIConnectionSettingsFilter.Merge(incoming, ["apiKey"], Settings("ENC:target-key"), out var missing);

        Assert.Multiple(() =>
        {
            Assert.That(merged!["apiKey"]!.ToString(), Is.EqualTo("ENC:target-key"));
            Assert.That(merged["Endpoint"]!.ToString(), Is.EqualTo("https://new.example.com"));
            // not in the file and not ignored, so it was removed at the source
            Assert.That(merged.ContainsKey("Organization"), Is.False);
            Assert.That(missing, Is.Empty);
        });
    }

    [Test]
    public void Merge_NewConnection_GetsAPlaceholderForEachSettingThatWasLeftOut()
    {
        var incoming = JsonSerializer.SerializeToElement(new { Endpoint = "https://new.example.com" });

        var merged = AIConnectionSettingsFilter.Merge(incoming, ["ApiKey"], existing: null, out var missing);

        Assert.Multiple(() =>
        {
            Assert.That(merged!.Keys, Is.EquivalentTo(new[] { "Endpoint", "ApiKey" }));
            Assert.That(merged["ApiKey"]!.ToString(), Is.EqualTo(AIConnectionSettingsFilter.MissingValuePlaceholder));
            Assert.That(missing, Is.EqualTo(new[] { "ApiKey" }));
        });
    }

    [Test]
    public void Merge_TargetWithAnEmptyValue_IsTreatedAsMissing()
    {
        var incoming = JsonSerializer.SerializeToElement(new { Endpoint = "https://new.example.com" });

        var merged = AIConnectionSettingsFilter.Merge(incoming, ["ApiKey"], Settings(""), out var missing);

        Assert.Multiple(() =>
        {
            Assert.That(merged!["ApiKey"]!.ToString(), Is.EqualTo(AIConnectionSettingsFilter.MissingValuePlaceholder));
            Assert.That(missing, Is.EqualTo(new[] { "ApiKey" }));
        });
    }

    [Test]
    public void Merge_NothingAnywhere_IsNull()
        => Assert.That(AIConnectionSettingsFilter.Merge(null, [], null, out _), Is.Null);
}
