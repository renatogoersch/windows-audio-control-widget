using System.Text.Json;
using SlotMixer;
using Xunit;

namespace SlotMixer.Tests;

public class UpdaterTests
{
    const string Repo = "https://github.com/renatogoersch/windows-audio-control-widget/releases/download";

    static string Feed(string tag, string host = Repo, bool digest = true) => $$"""
        {
          "tag_name": "{{tag}}",
          "assets": [
            { "name": "SlotMixer-autonomo.zip", "size": 60000000, "browser_download_url": "{{host}}/{{tag}}/SlotMixer-autonomo.zip"{{(digest ? ", \"digest\": \"sha256:AABB\"" : "")}} },
            { "name": "SlotMixer-requer-dotnet.zip", "size": 100000, "browser_download_url": "{{host}}/{{tag}}/SlotMixer-requer-dotnet.zip"{{(digest ? ", \"digest\": \"sha256:CCDD\"" : "")}} }
          ]
        }
        """;

    static readonly Version Current = new(1, 6, 0, 0);

    [Fact]
    public void A_newer_release_is_offered_with_the_zip_of_the_same_kind()
    {
        var small = Updater.Parse(Feed("v1.6.1"), Current, selfContained: false, trustAnyUrl: false)!;
        Assert.Equal(new Version(1, 6, 1), small.Version);
        Assert.Equal("SlotMixer-requer-dotnet.zip", small.AssetName);
        Assert.Equal("CCDD", small.Sha256);
        Assert.Equal(100000, small.AssetSize);

        var big = Updater.Parse(Feed("v1.6.1"), Current, selfContained: true, trustAnyUrl: false)!;
        Assert.Equal("SlotMixer-autonomo.zip", big.AssetName);
        Assert.Equal("AABB", big.Sha256);
    }

    [Theory]
    [InlineData("v1.6.0")]      // the same version
    [InlineData("v1.5.9")]      // older
    [InlineData("v1.0.0")]
    public void The_same_or_an_older_release_is_not_an_update(string tag)
    {
        Assert.Null(Updater.Parse(Feed(tag), Current, selfContained: false, trustAnyUrl: false));
    }

    [Theory]
    [InlineData("1.7.0")]
    [InlineData("v1.7.0")]
    [InlineData("V1.7.0")]
    public void The_tag_may_or_may_not_start_with_a_v(string tag)
    {
        Assert.NotNull(Updater.Parse(Feed(tag), Current, selfContained: false, trustAnyUrl: false));
    }

    [Fact]
    public void A_tag_that_is_not_a_version_is_ignored()
    {
        Assert.Null(Updater.Parse(Feed("nightly"), Current, selfContained: false, trustAnyUrl: false));
        Assert.Null(Updater.Parse(Feed("v2.0.0-beta"), Current, selfContained: false, trustAnyUrl: false));
    }

    [Fact]
    public void Downloads_from_anywhere_but_the_projects_own_releases_are_refused()
    {
        var elsewhere = Feed("v1.6.1", host: "https://example.com/files");
        Assert.Null(Updater.Parse(elsewhere, Current, selfContained: false, trustAnyUrl: false));
        Assert.NotNull(Updater.Parse(elsewhere, Current, selfContained: false, trustAnyUrl: true));   // a test feed set on purpose
    }

    [Fact]
    public void A_missing_checksum_is_allowed_but_reported_as_none()
    {
        var info = Updater.Parse(Feed("v1.6.1", digest: false), Current, selfContained: false, trustAnyUrl: false)!;
        Assert.Null(info.Sha256);
    }

    [Fact]
    public void A_release_without_the_wanted_zip_is_not_an_update()
    {
        const string onlyBig = """{ "tag_name": "v1.6.1", "assets": [ { "name": "SlotMixer-autonomo.zip", "size": 1, "browser_download_url": "https://github.com/renatogoersch/windows-audio-control-widget/releases/download/v1.6.1/SlotMixer-autonomo.zip" } ] }""";
        Assert.Null(Updater.Parse(onlyBig, Current, selfContained: false, trustAnyUrl: false));
        Assert.Null(Updater.Parse("""{ "tag_name": "v1.6.1" }""", Current, selfContained: false, trustAnyUrl: false));
    }

    [Fact]
    public void Garbage_from_the_server_is_an_error_the_caller_can_catch()
    {
        Assert.ThrowsAny<JsonException>(() => Updater.Parse("<html>rate limited</html>", Current, selfContained: false, trustAnyUrl: false));
    }
}

public class SettingsTests
{
    [Fact]
    public void An_old_settings_file_gets_the_new_options_with_their_defaults()
    {
        var old = JsonSerializer.Deserialize<Settings>("""{ "Slots": [ null, null, null ], "Profiles": [] }""")!;
        Assert.True(old.AutoUpdate);
        Assert.True(old.ShowActivity);
        Assert.Equal("primary", old.ShowOn);
        Assert.Equal("auto", old.Language);
        Assert.Equal(0, old.OffsetFromTray);
        Assert.NotNull(old.Balance);
        Assert.False(old.Balance.IsReady);
    }

    [Fact]
    public void A_profile_without_triggers_reverts_nothing_by_default_and_keeps_its_data()
    {
        var profile = JsonSerializer.Deserialize<Profile>("""{ "Name": "Game", "Entries": [] }""")!;
        Assert.Null(profile.TriggerApp);
        Assert.Null(profile.TriggerDeviceId);
        Assert.True(profile.RevertOnEnd);
    }

    [Fact]
    public void Settings_survive_a_round_trip()
    {
        var settings = new Settings { Language = "en", ShowOn = "all", OffsetFromTray = 120.5, AutoUpdate = false };
        settings.Balance.GameApp = @"C:\Games\dota2.exe";
        var back = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!;
        Assert.Equal("en", back.Language);
        Assert.Equal("all", back.ShowOn);
        Assert.Equal(120.5, back.OffsetFromTray);
        Assert.False(back.AutoUpdate);
        Assert.Equal(@"C:\Games\dota2.exe", back.Balance.GameApp);
    }
}

public class DiagnosticsTests
{
    [Fact]
    public void The_user_name_is_blanked_out_of_paths_and_text()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var text = Diagnostics.Redact($"error in {profile}\\AppData\\Local\\SlotMixer by {Environment.UserName}");
        Assert.DoesNotContain(Environment.UserName, text.Replace("%USERPROFILE%", "").Replace("<user>", ""), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%USERPROFILE%", text);
    }
}
