using System.Text.Json.Nodes;
using Backend.External.Email;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Settings.Core;
using Backend.Features.Settings.Endpoints.Settings;
using Backend.Tests.Fakes;
using Hangfire;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace Backend.Tests.Features.Settings;

/// <summary>
/// Tests for the <c>Email</c> setting: its default taken from the <c>EmailSettings</c> configuration
/// section, its validation, the SMTP password as a secret - encrypted at rest, never answered, kept when
/// a save leaves it out, cleared by an empty string, ignored when it cannot be decrypted, never inherited
/// apart from its server - and the email job sending with the settings standing when it runs.
/// </summary>
public class EmailSettingsTests(App app) : SettingsTestsBase(app)
{
    private const string Email = EmailSettingsProvider.EmailSettingName;

    private ISettingProvider SettingProvider => Service<ISettingProvider>();

    private RecordingEmailTransport Transport => App.Services.GetRequiredService<RecordingEmailTransport>();

    [Fact]
    public async Task No_Overrides_Resolve_To_The_Configured_Default()
    {
        var configured = ConfiguredEmailSettings();
        var cancellationToken = TestContext.Current.CancellationToken;

        var resolved = await SettingProvider.GetAsync<EmailSettings>(cancellationToken);

        resolved.Should().BeEquivalentTo(configured);

        var client = await SettingsClientAsync();
        var (response, list) = await client.GETAsync<SettingListEndpoint, SettingListResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var email = list.Items.Single(x => x.Name == Email);
        email.Properties.Select(x => x.Name).Should().Equal("smtpServer", "smtpPort", "smtpUsername", "smtpPassword", "senderEmail", "senderName");
        email.Properties.Should().OnlyContain(x => x.Source == "default");
        PropertyOf(email, "smtpServer").Value!.GetValue<string>().Should().Be(configured.SmtpServer);
        PropertyOf(email, "smtpPort").Value!.GetValue<int>().Should().Be(configured.SmtpPort);
        PropertyOf(email, "senderEmail").Value!.GetValue<string>().Should().Be(configured.SenderEmail);
    }

    [Fact]
    public async Task Invalid_Port_Is_Refused_With_Its_Code()
    {
        var client = await SettingsClientAsync();

        var (response, refusal) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = Email, Values = new JsonObject { ["smtpPort"] = 0 } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Select(x => x.Code).Should().Contain(ErrorCodes.EmailSmtpPortInvalid.Value);
        refusal.Errors.Should().Contain(x => x.Name == "values.smtpPort");
        (await StoredValuesAsync(Email)).Should().BeNull();
    }

    [Fact]
    public async Task Malformed_Sender_Email_Is_Refused_With_Its_Code()
    {
        var client = await SettingsClientAsync();

        var (response, refusal) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = Email, Values = new JsonObject { ["senderEmail"] = "not-an-address" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Select(x => x.Code).Should().Contain(ErrorCodes.EmailSenderEmailInvalid.Value);
        refusal.Errors.Should().Contain(x => x.Name == "values.senderEmail");
    }

    [Fact]
    public async Task Configured_Default_Password_Is_Never_Answered()
    {
        var configured = ConfiguredEmailSettings();
        var client = await SettingsClientAsync();

        var (response, list) = await client.GETAsync<SettingListEndpoint, SettingListResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var property = PropertyOf(list, Email, "smtpPassword");
        property.Value.Should().BeNull();
        property.IsSecret.Should().BeTrue();
        property.IsSet.Should().Be(configured.SmtpPassword.Length > 0);
        property.Source.Should().Be("default");
        if (configured.SmtpPassword.Length > 0)
        {
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().NotContain(configured.SmtpPassword);
        }
    }

    [Fact]
    public async Task Stored_Password_Is_Encrypted()
    {
        var client = await SettingsClientAsync();
        var password = NewSecret();

        (await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpPassword"] = password, ["smtpServer"] = "stored.example" } }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var raw = await DbContext.SettingValues.AsNoTracking()
            .Where(x => x.Name == Email)
            .Select(x => x.Values)
            .SingleAsync(TestContext.Current.CancellationToken);
        raw.Should().NotContain(password);

        var stored = JsonNode.Parse(raw)!.AsObject();
        stored["smtpServer"]!.GetValue<string>().Should().Be("stored.example", "only the secret is encrypted");
        var cipher = stored["smtpPassword"]!.GetValue<string>();
        cipher.Should().NotBe(password);
        Service<IDataProtectionProvider>().CreateProtector(SettingValueService.SecretProtectionPurpose, Email, "smtpPassword")
            .Unprotect(cipher).Should().Be(password);
        var otherPurpose = () => Service<IDataProtectionProvider>().CreateProtector(SettingValueService.SecretProtectionPurpose).Unprotect(cipher);
        otherPurpose.Should().Throw<System.Security.Cryptography.CryptographicException>("the purpose is narrowed to the setting and property");

        (await SettingProvider.GetAsync<EmailSettings>(TestContext.Current.CancellationToken)).SmtpPassword.Should().Be(password);
    }

    [Fact]
    public async Task Omitted_Or_Null_Password_Keeps_The_Stored_One_And_Empty_Clears_It()
    {
        var configured = ConfiguredEmailSettings();
        var client = await SettingsClientAsync();
        var password = NewSecret();

        (await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpPassword"] = password } }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var (omittedResponse, omitted) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpServer"] = "omitted.example" } });

        omittedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertMaskedAndSet(PropertyOf(omitted, "smtpPassword"), "override");
        var afterOmitted = await ResolveFreshAsync();
        afterOmitted.SmtpPassword.Should().Be(password, "a save that leaves the secret out keeps it");
        afterOmitted.SmtpServer.Should().Be("omitted.example");

        var (nullResponse, _) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpPassword"] = null, ["senderName"] = "Null Sender" } });

        nullResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterNull = await ResolveFreshAsync();
        afterNull.SmtpPassword.Should().Be(password, "a secret sent as null is kept");
        afterNull.SenderName.Should().Be("Null Sender");
        afterNull.SmtpServer.Should().Be(configured.SmtpServer, "a non-secret property keeps replace semantics");

        var (clearResponse, cleared) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpPassword"] = "", ["senderName"] = "Null Sender" } });

        clearResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        PropertyOf(cleared, "smtpPassword").Source.Should().Be("default");
        PropertyOf(cleared, "smtpPassword").IsSet.Should().Be(configured.SmtpPassword.Length > 0);
        (await ResolveFreshAsync()).SmtpPassword.Should().Be(configured.SmtpPassword, "an empty string clears the override");
        (await StoredValuesAsync(Email))!.ContainsKey("smtpPassword").Should().BeFalse();
    }

    [Fact]
    public async Task Undecryptable_Stored_Password_Is_Ignored()
    {
        var configured = ConfiguredEmailSettings();
        await SetStoredValuesAsync(Email, new JsonObject { ["smtpPassword"] = "not-a-protected-payload", ["senderName"] = "Garbage" });
        var client = await SettingsClientAsync();

        var (response, list) = await client.GETAsync<SettingListEndpoint, SettingListResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PropertyOf(list, Email, "smtpPassword").Source.Should().Be("default", "a secret that does not decrypt follows the default");
        PropertyOf(list, Email, "senderName").Source.Should().Be("override");

        var resolved = await SettingProvider.GetAsync<EmailSettings>(TestContext.Current.CancellationToken);
        resolved.SmtpPassword.Should().Be(configured.SmtpPassword);
        resolved.SenderName.Should().Be("Garbage");
    }

    [Fact]
    public async Task Stored_Password_Is_Never_Answered()
    {
        var client = await SettingsClientAsync();
        var password = NewSecret();

        var (response, updated) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpPassword"] = password } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertMaskedAndSet(PropertyOf(updated, "smtpPassword"), "override");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().NotContain(password);

        var (listResponse, list) = await client.GETAsync<SettingListEndpoint, SettingListResponse>();

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertMaskedAndSet(PropertyOf(list, Email, "smtpPassword"), "override");
        (await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().NotContain(password);
    }

    [Fact]
    public async Task Overriding_The_Server_Alone_Does_Not_Inherit_The_Configured_Password()
    {
        var client = await SettingsClientAsync();
        var server = $"redirect-{Guid.NewGuid():N}.example";

        var (response, updated) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpServer"] = server } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PropertyOf(updated, "smtpPassword").Source.Should().Be("override", "the override that took the server over answers for the password");
        PropertyOf(updated, "smtpPassword").IsSet.Should().BeFalse();
        (await ResolveFreshAsync()).SmtpPassword.Should().BeEmpty("a password is never sent to a server it was not configured for");

        var (renamed, _) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["senderName"] = "Renamed" } });

        renamed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ResolveFreshAsync()).SmtpPassword.Should().Be(ConfiguredEmailSettings().SmtpPassword,
            "an override that leaves the server alone keeps the configured password");
    }

    [Fact]
    public async Task Job_Sends_With_The_Settings_Standing_When_It_Runs()
    {
        var admin = await AdminClientAsync();
        var server = $"stored-{Guid.NewGuid():N}.example";
        var password = NewSecret();
        var recipient = NewRecipient();

        var jobId = Service<IEmailBackgroundJobs>().Enqueue(recipient, "Subject", "Body");

        (await admin.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpServer"] = server, ["smtpPassword"] = password, ["senderName"] = "Stored" } }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        await RunInFreshScopeAsync(LoadJob(jobId));

        var sent = Transport.SentTo(recipient).Should().ContainSingle().Subject;
        sent.Settings.SmtpServer.Should().Be(server, "the settings are read when the job runs, not when it was enqueued");
        sent.Settings.SmtpPassword.Should().Be(password);
        sent.Settings.SenderName.Should().Be("Stored");
    }

    #region Helpers

    /// <summary>The <c>EmailSettings</c> section of the test host's configuration, bound as the catalogue binds it.</summary>
    private EmailSettings ConfiguredEmailSettings()
    {
        var settings = new EmailSettings();
        App.Services.GetRequiredService<IConfiguration>().GetSection(EmailSettingsProvider.ConfigurationSection).Bind(settings);
        return settings;
    }

    /// <summary>Resolves the email settings in a new scope, so no read cached by this test's scope answers.</summary>
    private async Task<EmailSettings> ResolveFreshAsync()
    {
        using var scope = App.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISettingProvider>()
            .GetAsync<EmailSettings>(TestContext.Current.CancellationToken);
    }

    private static void AssertMaskedAndSet(SettingPropertyDto property, string source)
    {
        property.Value.Should().BeNull("a secret's value never leaves the API");
        property.IsSecret.Should().BeTrue();
        property.IsSet.Should().BeTrue();
        property.Source.Should().Be(source);
    }

    private Hangfire.Common.Job LoadJob(string jobId)
    {
        using var connection = App.Services.GetRequiredService<JobStorage>().GetConnection();
        var data = connection.GetJobData(jobId);
        data.Should().NotBeNull();
        data!.LoadException.Should().BeNull();
        data.Job.Method.DeclaringType.Should().Be<IEmailService>();
        return data.Job;
    }

    /// <summary>
    /// Runs a stored job the way the Hangfire worker would: in a new DI scope, with the arguments the job
    /// was stored with.
    /// </summary>
    private async Task RunInFreshScopeAsync(Hangfire.Common.Job job)
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEmailService>();
        await (Task)job.Method.Invoke(service, [.. job.Args])!;
    }

    private static string NewSecret() => $"secret-{Guid.NewGuid():N}";

    private static string NewRecipient() => $"recipient-{Guid.NewGuid():N}@example.com";

    #endregion
}
