namespace Backend.Tests.Features.Settings;

using System.Text.Json.Nodes;
using Backend.External.Email;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Settings.Core;
using Backend.Features.Settings.Endpoints.Settings;
using Backend.Features.Tenancy.Core;
using Backend.Tests.Fakes;
using Hangfire;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Tests for the <c>Email</c> setting: its default taken from the <c>EmailSettings</c> configuration
/// section, its validation, the SMTP password as a secret - encrypted at rest, never answered, kept when
/// a save leaves it out, cleared by an empty string, ignored when it cannot be decrypted - and the email
/// job sending with the settings of the tenant it was enqueued in.
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
        var tenant = await CreateTenantAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        var forPlatform = await SettingProvider.GetAsync<EmailSettings>((Guid?)null, cancellationToken);
        var forTenant = await SettingProvider.GetAsync<EmailSettings>(tenant.Id, cancellationToken);

        forPlatform.Should().BeEquivalentTo(configured);
        forTenant.Should().BeEquivalentTo(configured);

        var client = await TenantClientAsync(tenant.Id);
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
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);

        var (response, refusal) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = Email, Values = new JsonObject { ["smtpPort"] = 0 } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Select(x => x.Code).Should().Contain(ErrorCodes.EmailSmtpPortInvalid.Value);
        refusal.Errors.Should().Contain(x => x.Name == "values.smtpPort");
        (await StoredValuesAsync(Email, tenant.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Malformed_Sender_Email_Is_Refused_With_Its_Code()
    {
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);

        var (response, refusal) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = Email, Values = new JsonObject { ["senderEmail"] = "not-an-address" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Select(x => x.Code).Should().Contain(ErrorCodes.EmailSenderEmailInvalid.Value);
        refusal.Errors.Should().Contain(x => x.Name == "values.senderEmail");
    }

    [Fact]
    public async Task Tenant_Password_Is_Never_Answered()
    {
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);
        var password = NewSecret();

        var (response, updated) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpPassword"] = password } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().NotContain(password);
        AssertMaskedAndSet(PropertyOf(updated, "smtpPassword"), "tenant");
        PropertyOf(updated, "smtpServer").IsSecret.Should().BeFalse();
        PropertyOf(updated, "smtpServer").IsSet.Should().BeNull("isSet speaks only for a secret");

        var (listResponse, list) = await client.GETAsync<SettingListEndpoint, SettingListResponse>();

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().NotContain(password);
        AssertMaskedAndSet(PropertyOf(list, Email, "smtpPassword"), "tenant");
    }

    [Fact]
    public async Task Platform_Password_Is_Never_Answered()
    {
        var platform = await PlatformClientAsync();
        var password = NewSecret();

        var (response, updated) = await platform.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpPassword"] = password } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().NotContain(password);
        AssertMaskedAndSet(PropertyOf(updated, "smtpPassword"), "platform");

        var tenant = await CreateTenantAsync();
        var tenantClient = await TenantClientAsync(tenant.Id);
        var (listResponse, list) = await tenantClient.GETAsync<SettingListEndpoint, SettingListResponse>();

        (await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().NotContain(password);
        AssertMaskedAndSet(PropertyOf(list, Email, "smtpPassword"), "platform");
    }

    [Fact]
    public async Task Configured_Default_Password_Is_Never_Answered()
    {
        var configured = ConfiguredEmailSettings();
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);

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
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);
        var password = NewSecret();

        (await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpPassword"] = password, ["smtpServer"] = "stored.example" } }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var raw = await DbContext.SettingValues.AsNoTracking().AcrossAllTenants()
            .Where(x => x.Name == Email && x.TenantId == tenant.Id)
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

        (await SettingProvider.GetAsync<EmailSettings>(tenant.Id, TestContext.Current.CancellationToken)).SmtpPassword.Should().Be(password);
    }

    [Fact]
    public async Task Omitted_Or_Null_Password_Keeps_The_Stored_One_And_Empty_Clears_It()
    {
        var configured = ConfiguredEmailSettings();
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);
        var password = NewSecret();

        (await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpPassword"] = password } }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var (omittedResponse, omitted) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpServer"] = "omitted.example" } });

        omittedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertMaskedAndSet(PropertyOf(omitted, "smtpPassword"), "tenant");
        var afterOmitted = await ResolveFreshAsync(tenant.Id);
        afterOmitted.SmtpPassword.Should().Be(password, "a save that leaves the secret out keeps it");
        afterOmitted.SmtpServer.Should().Be("omitted.example");

        var (nullResponse, _) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpPassword"] = null, ["senderName"] = "Null Sender" } });

        nullResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterNull = await ResolveFreshAsync(tenant.Id);
        afterNull.SmtpPassword.Should().Be(password, "a secret sent as null is kept");
        afterNull.SenderName.Should().Be("Null Sender");
        afterNull.SmtpServer.Should().Be(configured.SmtpServer, "a non-secret property keeps replace semantics");

        var (clearResponse, cleared) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpPassword"] = "", ["senderName"] = "Null Sender" } });

        clearResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        PropertyOf(cleared, "smtpPassword").Source.Should().Be("default");
        PropertyOf(cleared, "smtpPassword").IsSet.Should().Be(configured.SmtpPassword.Length > 0);
        (await ResolveFreshAsync(tenant.Id)).SmtpPassword.Should().Be(configured.SmtpPassword, "an empty string clears this scope's override");
        (await StoredValuesAsync(Email, tenant.Id))!.ContainsKey("smtpPassword").Should().BeFalse();
    }

    [Fact]
    public async Task Undecryptable_Stored_Password_Is_Ignored()
    {
        var configured = ConfiguredEmailSettings();
        var tenant = await CreateTenantAsync();
        await SetTenantValuesAsync(tenant.Id, Email, new JsonObject { ["smtpPassword"] = "not-a-protected-payload", ["senderName"] = "Garbage" });
        var client = await TenantClientAsync(tenant.Id);

        var (response, list) = await client.GETAsync<SettingListEndpoint, SettingListResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PropertyOf(list, Email, "smtpPassword").Source.Should().Be("default", "a secret that does not decrypt follows the layer below");
        PropertyOf(list, Email, "senderName").Source.Should().Be("tenant");

        var resolved = await SettingProvider.GetAsync<EmailSettings>(tenant.Id, TestContext.Current.CancellationToken);
        resolved.SmtpPassword.Should().Be(configured.SmtpPassword);
        resolved.SenderName.Should().Be("Garbage");
    }

    [Fact]
    public async Task Undecryptable_Password_Beside_A_Tenant_Server_Is_Not_Replaced_By_The_Inherited_One()
    {
        var tenant = await CreateTenantAsync();
        await SetTenantValuesAsync(tenant.Id, Email, new JsonObject { ["smtpPassword"] = "not-a-protected-payload", ["smtpServer"] = "garbage.example" });
        var client = await TenantClientAsync(tenant.Id);

        var (response, list) = await client.GETAsync<SettingListEndpoint, SettingListResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PropertyOf(list, Email, "smtpPassword").Source.Should().Be("tenant");
        PropertyOf(list, Email, "smtpPassword").IsSet.Should().BeFalse();

        var resolved = await SettingProvider.GetAsync<EmailSettings>(tenant.Id, TestContext.Current.CancellationToken);
        resolved.SmtpPassword.Should().BeEmpty("the tenant's server must never receive a password from a layer below it");
        resolved.SmtpServer.Should().Be("garbage.example");
    }

    [Fact]
    public async Task Tenant_Overriding_The_Server_Alone_Does_Not_Inherit_The_Platform_Password()
    {
        var platform = await PlatformClientAsync();
        var platformPassword = NewSecret();
        (await platform.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpPassword"] = platformPassword } }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var redirecting = await CreateTenantAsync();
        var redirectingClient = await TenantClientAsync(redirecting.Id);
        var server = $"attacker-{Guid.NewGuid():N}.example";
        var (response, updated) = await redirectingClient.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpServer"] = server } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PropertyOf(updated, "smtpPassword").IsSet.Should().BeFalse();
        PropertyOf(updated, "smtpPassword").Source.Should().Be("tenant", "the layer that took the server over answers for the password");
        (await ResolveFreshAsync(redirecting.Id)).SmtpPassword.Should().BeEmpty();

        var (_, list) = await redirectingClient.GETAsync<SettingListEndpoint, SettingListResponse>();
        PropertyOf(list, Email, "smtpPassword").IsSet.Should().BeFalse();

        var recipient = NewRecipient();
        string jobId;
        using (TenantContext.BeginTenant(redirecting.Id))
        {
            jobId = Service<IEmailBackgroundJobs>().Enqueue(recipient, "Subject", "Body");
        }

        await RunInFreshScopeAsync(LoadJob(jobId));
        var sent = Transport.SentTo(recipient).Should().ContainSingle().Subject;
        sent.Settings.SmtpServer.Should().Be(server);
        sent.Settings.SmtpPassword.Should().BeEmpty("the platform's password never reaches the tenant's server");

        var renaming = await CreateTenantAsync();
        var renamingClient = await TenantClientAsync(renaming.Id);
        (await renamingClient.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["senderName"] = "Renamed" } }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var renamed = await ResolveFreshAsync(renaming.Id);
        renamed.SmtpPassword.Should().Be(platformPassword, "the sender name does not decide where the password is sent");
        renamed.SmtpServer.Should().Be(ConfiguredEmailSettings().SmtpServer);
        renamed.SenderName.Should().Be("Renamed");
    }

    [Fact]
    public async Task Account_Email_Requested_From_Inside_A_Tenant_Is_Sent_With_The_Platforms_Settings()
    {
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);
        var tenantServer = $"tenant-{Guid.NewGuid():N}.example";
        (await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpServer"] = tenantServer } }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var account = await CreateTenantUserAsync(tenant.Id);

        var response = await client.POSTAsync<ForgetPasswordEndpoint, ForgetPasswordRequest>(new() { Email = account.Email });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var job = FindEnqueuedJobTo(account.Email);
        job.Args[0].Should().BeNull("an account's mail is never sent through the caller's tenant's server");

        await RunInFreshScopeAsync(job);
        var sent = Transport.SentTo(account.Email).Should().ContainSingle().Subject;
        sent.Settings.SmtpServer.Should().Be(ConfiguredEmailSettings().SmtpServer).And.NotBe(tenantServer);
    }

    [Fact]
    public async Task Job_Enqueued_In_A_Tenant_Sends_With_That_Tenants_Settings()
    {
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);
        var server = $"tenant-{Guid.NewGuid():N}.example";
        var password = NewSecret();
        (await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpServer"] = server, ["smtpPassword"] = password } }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var recipient = NewRecipient();

        string jobId;
        using (TenantContext.BeginTenant(tenant.Id))
        {
            jobId = Service<IEmailBackgroundJobs>().Enqueue(recipient, "Subject", "Body", isHtml: true);
        }

        var job = LoadJob(jobId);
        job.Args[0].Should().Be(tenant.Id, "the tenant travels as the job's first argument");

        await RunInFreshScopeAsync(job);

        var sent = Transport.SentTo(recipient).Should().ContainSingle().Subject;
        sent.Settings.SmtpServer.Should().Be(server);
        sent.Settings.SmtpPassword.Should().Be(password);
        sent.Message.Should().Be(new EmailMessage(recipient, "Subject", "Body", true));
    }

    [Fact]
    public async Task Job_Enqueued_In_Platform_Scope_Or_No_Scope_Sends_With_The_Platforms_Settings()
    {
        var platform = await PlatformClientAsync();
        var server = $"platform-{Guid.NewGuid():N}.example";
        var password = NewSecret();
        (await platform.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = Email, Values = new JsonObject { ["smtpServer"] = server, ["smtpPassword"] = password, ["senderName"] = "Platform" } }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var platformRecipient = NewRecipient();
        string platformJobId;
        using (TenantContext.BeginPlatformScope())
        {
            platformJobId = Service<IEmailBackgroundJobs>().Enqueue(platformRecipient, "Subject", "Body");
        }

        var unscopedRecipient = NewRecipient();
        string unscopedJobId;
        using (var scope = App.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().IsResolved.Should().BeFalse();
            unscopedJobId = scope.ServiceProvider.GetRequiredService<IEmailBackgroundJobs>().Enqueue(unscopedRecipient, "Subject", "Body");
        }

        foreach (var (jobId, recipient) in new[] { (platformJobId, platformRecipient), (unscopedJobId, unscopedRecipient) })
        {
            var job = LoadJob(jobId);
            job.Args[0].Should().BeNull("a message enqueued outside any tenant is sent with the platform's settings");

            await RunInFreshScopeAsync(job);

            var sent = Transport.SentTo(recipient).Should().ContainSingle().Subject;
            sent.Settings.SmtpServer.Should().Be(server);
            sent.Settings.SmtpPassword.Should().Be(password);
            sent.Settings.SenderName.Should().Be("Platform");
        }
    }

    #region Helpers

    /// <summary>The <c>EmailSettings</c> section of the test host's configuration, bound as the catalogue binds it.</summary>
    private EmailSettings ConfiguredEmailSettings()
    {
        var settings = new EmailSettings();
        App.Services.GetRequiredService<IConfiguration>().GetSection(EmailSettingsProvider.ConfigurationSection).Bind(settings);
        return settings;
    }

    /// <summary>Resolves the tenant's email settings in a new scope, so no read cached by this test's scope answers.</summary>
    private async Task<EmailSettings> ResolveFreshAsync(Guid tenantId)
    {
        using var scope = App.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISettingProvider>()
            .GetAsync<EmailSettings>(tenantId, TestContext.Current.CancellationToken);
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

    /// <summary>The single enqueued email job addressed to <paramref name="recipient"/>.</summary>
    private Hangfire.Common.Job FindEnqueuedJobTo(string recipient)
    {
        var monitoring = App.Services.GetRequiredService<JobStorage>().GetMonitoringApi();
        var jobs = monitoring.EnqueuedJobs("default", 0, int.MaxValue)
            .Select(entry => entry.Value.Job)
            .Where(job => job?.Method.DeclaringType == typeof(IEmailService) && job.Args.Contains(recipient))
            .ToList();
        jobs.Should().ContainSingle();
        return jobs[0];
    }

    /// <summary>
    /// Runs a stored job the way the Hangfire worker would: in a new DI scope whose tenant context has
    /// not been established, with the arguments the job was stored with.
    /// </summary>
    private async Task RunInFreshScopeAsync(Hangfire.Common.Job job)
    {
        using var scope = App.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().IsResolved.Should().BeFalse();
        var service = scope.ServiceProvider.GetRequiredService<IEmailService>();
        await (Task)job.Method.Invoke(service, [.. job.Args])!;
    }

    private static string NewSecret() => $"secret-{Guid.NewGuid():N}";

    private static string NewRecipient() => $"recipient-{Guid.NewGuid():N}@example.com";

    #endregion
}
