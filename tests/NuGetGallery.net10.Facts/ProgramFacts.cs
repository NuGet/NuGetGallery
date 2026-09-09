// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NuGet.Services.Configuration;
using NuGetGallery.Authentication;
using Yarp.ReverseProxy.Forwarder;
using Xunit;

namespace NuGetGallery;

public class ProgramFacts
{
    [Fact]
    public async Task StartsWithoutAzureOrAspireAndServesReservedEndpoints()
    {
        string storagePath = CreateStoragePath();
        await using WebApplication application = CreateApplication(storagePath);

        await application.StartAsync();
        try
        {
            HttpClient client = application.GetTestClient();

            Assert.Equal("NuGetGallery.net10", await client.GetStringAsync("/_local"));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/_health")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/_ready")).StatusCode);
        }
        finally
        {
            await application.StopAsync();
            DeleteStoragePath(storagePath);
        }
    }

    [Fact]
    public async Task RegistersSharedDataProtectionAndReaderOnlyCookieAuthentication()
    {
        string storagePath = CreateStoragePath();
        await using WebApplication application = CreateApplication(storagePath);

        await application.StartAsync();
        try
        {
            IDataProtector protector = application.Services
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("host-test");
            string protectedValue = protector.Protect("value");
            Assert.Equal("value", protector.Unprotect(protectedValue));

            var schemeProvider = application.Services.GetRequiredService<IAuthenticationSchemeProvider>();
            AuthenticationScheme scheme = await schemeProvider.GetSchemeAsync(SharedCookieConstants.AuthenticationScheme);
            Assert.NotNull(scheme);
            Assert.Equal(SharedCookieConstants.AuthenticationScheme, scheme.Name);

            CookieAuthenticationOptions cookieOptions = application.Services
                .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(SharedCookieConstants.AuthenticationScheme);
            Assert.Equal(SharedCookieConstants.CookieName, cookieOptions.Cookie.Name);
            Assert.Equal(SharedCookieConstants.CookiePath, cookieOptions.Cookie.Path);
            Assert.True(cookieOptions.Cookie.HttpOnly);
            Assert.Equal(CookieSecurePolicy.SameAsRequest, cookieOptions.Cookie.SecurePolicy);
            Assert.Equal(SharedCookieConstants.Expiration, cookieOptions.ExpireTimeSpan);
            Assert.False(cookieOptions.SlidingExpiration);
            Assert.NotNull(application.Services.GetRequiredService<IHttpForwarder>());

            LegacyProxyOptions proxyOptions = application.Services
                .GetRequiredService<IOptions<LegacyProxyOptions>>()
                .Value;
            Assert.Equal("https://localhost", proxyOptions.Origin);
            Assert.True(
                proxyOptions.ActivityTimeout >= LegacyProxyOptions.LegacyExecutionTimeout);
            Assert.True(
                proxyOptions.MaximumRequestBodySize >= LegacyProxyOptions.LegacyMaximumRequestBodySize);

            IISServerOptions iisOptions = application.Services
                .GetRequiredService<IOptions<IISServerOptions>>()
                .Value;
            Assert.Equal(
                LegacyProxyOptions.LegacyMaximumRequestBodySize,
                iisOptions.MaxRequestBodySize);
        }
        finally
        {
            await application.StopAsync();
            DeleteStoragePath(storagePath);
        }
    }

    [Fact]
    public async Task SharedCookieClaimsEnforceRoleAndClaimAuthorization()
    {
        string storagePath = CreateStoragePath();
        await using WebApplication application = CreateApplication(
            storagePath,
            builder => builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(
                    "package-owner",
                    policy => policy
                        .RequireRole("PackageOwners")
                        .RequireClaim(
                            SharedCookieConstants.PasswordLoginClaimType,
                            bool.TrueString));
            }));
        application
            .MapGet("/_role-only", () => Results.Ok())
            .RequireAuthorization(new AuthorizeAttribute { Roles = "Administrators" });
        application
            .MapGet("/_role-and-claim", () => Results.Ok())
            .RequireAuthorization("package-owner");

        await application.StartAsync();
        try
        {
            HttpClient client = application.GetTestClient();
            using HttpResponseMessage unauthenticatedResponse =
                await client.GetAsync("/_role-only");
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedResponse.StatusCode);

            string noRoleCookie = CreateCookie(
                application,
                new Claim(ClaimTypes.Name, "interop-user"));
            using HttpResponseMessage forbiddenResponse =
                await SendWithCookieAsync(client, "/_role-only", noRoleCookie);
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

            string authorizedCookie = CreateCookie(
                application,
                new Claim(ClaimTypes.Name, "interop-user"),
                new Claim(SharedCookieConstants.RoleClaimType, "Administrators"),
                new Claim(SharedCookieConstants.RoleClaimType, "PackageOwners"),
                new Claim(
                    SharedCookieConstants.PasswordLoginClaimType,
                    bool.TrueString));
            using HttpResponseMessage roleResponse =
                await SendWithCookieAsync(client, "/_role-only", authorizedCookie);
            Assert.Equal(HttpStatusCode.OK, roleResponse.StatusCode);
            using HttpResponseMessage claimResponse =
                await SendWithCookieAsync(client, "/_role-and-claim", authorizedCookie);
            Assert.Equal(HttpStatusCode.OK, claimResponse.StatusCode);
        }
        finally
        {
            await application.StopAsync();
            DeleteStoragePath(storagePath);
        }
    }

    [Fact]
    public void IisHostingArtifactAppliesTheLegacyNativeRequestLimit()
    {
        string repositoryRoot = FindRepositoryRoot();
        string webConfigPath = Path.Combine(
            repositoryRoot,
            "src",
            "NuGetGallery.net10",
            "web.config");
        XDocument webConfig = XDocument.Load(webConfigPath);

        XElement requestLimits = webConfig
            .Descendants("requestLimits")
            .Single();
        XElement aspNetCore = webConfig
            .Descendants("aspNetCore")
            .Single();

        Assert.Equal(
            LegacyProxyOptions.NativeIisMaximumRequestBodySize.ToString(),
            requestLimits.Attribute("maxAllowedContentLength")?.Value);
        Assert.Equal("inprocess", aspNetCore.Attribute("hostingModel")?.Value);
    }

    [Fact]
    public async Task AppliesAConfiguredLimitAboveTheLegacyDefaultToManagedServers()
    {
        const long configuredLimit = 500_000_000;
        string storagePath = CreateStoragePath();
        await using WebApplication application = CreateApplication(
            storagePath,
            "--LegacyProxy:MaximumRequestBodySize",
            configuredLimit.ToString());

        await application.StartAsync();
        try
        {
            Assert.Equal(
                configuredLimit,
                application.Services
                    .GetRequiredService<IOptions<LegacyProxyOptions>>()
                    .Value
                    .MaximumRequestBodySize);
            Assert.Equal(
                configuredLimit,
                application.Services
                    .GetRequiredService<IOptions<IISServerOptions>>()
                    .Value
                    .MaxRequestBodySize);
            Assert.Equal(
                configuredLimit,
                application.Services
                    .GetRequiredService<IOptions<KestrelServerOptions>>()
                    .Value
                    .Limits
                    .MaxRequestBodySize);
        }
        finally
        {
            await application.StopAsync();
            DeleteStoragePath(storagePath);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(LegacyProxyOptions.LegacyMaximumRequestBodySize - 1)]
    [InlineData(4_294_967_296)]
    public async Task RejectsUnsupportedRequestLimits(long configuredLimit)
    {
        string storagePath = CreateStoragePath();
        await using WebApplication application = CreateApplication(
            storagePath,
            "--LegacyProxy:MaximumRequestBodySize",
            configuredLimit.ToString());

        try
        {
            OptionsValidationException exception = await Assert.ThrowsAsync<OptionsValidationException>(
                () => application.StartAsync());
            Assert.Contains("Unlimited request bodies are not supported", exception.Message);
        }
        finally
        {
            DeleteStoragePath(storagePath);
        }
    }

    [Fact]
    public void ProductionDefaultsToManagedIdentityForAzureStorage()
    {
        IConfiguration configuration = CreateConfiguration(Environments.Production);

        Assert.False(configuration.GetValue<bool>(Constants.ConfigureForLocalDevelopment));
        Assert.Equal(
            SharedDataProtectionServiceCollectionExtensions.StorageCredentialMode.ManagedIdentity,
            GetSystemAssignedManagedIdentityMode(configuration));
    }

    [Fact]
    public void DevelopmentOptsIntoDefaultAzureCredentialForAzureStorage()
    {
        IConfiguration configuration = CreateConfiguration(Environments.Development);

        Assert.True(configuration.GetValue<bool>(Constants.ConfigureForLocalDevelopment));
        Assert.Equal(
            SharedDataProtectionServiceCollectionExtensions.StorageCredentialMode.DefaultAzureCredential,
            GetSystemAssignedManagedIdentityMode(configuration));
    }

    [Theory]
    [InlineData(
        "DataProtection:StorageType",
        "invalid",
        "Development",
        "storage type")]
    [InlineData(
        "DataProtection:ApplicationDiscriminator",
        "DifferentApplication",
        "Development",
        "application discriminator")]
    [InlineData(
        "DataProtection:KeyLifetime",
        "6.00:00:00",
        "Development",
        "key lifetime")]
    [InlineData(
        "DataProtection:EncryptKeysAtRest",
        "false",
        "Production",
        "encrypted at rest")]
    [InlineData(
        "DataProtection:KeyVaultKeyIdentifier",
        "https://vault.vault.azure.net/keys/data-protection/version-1",
        "Development",
        "versionless")]
    public void RejectsInvalidSharedCookieProtectionSettingsAtStartup(
        string setting,
        string value,
        string environmentName,
        string expectedMessage)
    {
        string storagePath = CreateStoragePath();
        var arguments = new List<string>
        {
            "--DataProtection:StorageLocation",
            storagePath,
            "--DataProtection:EncryptKeysAtRest",
            setting == "DataProtection:KeyVaultKeyIdentifier" ? "true" : "false",
            "--" + setting,
            value,
        };

        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Program.BuildApplication(
                    arguments.ToArray(),
                    builder =>
                    {
                        builder.WebHost.UseTestServer();
                        builder.Environment.EnvironmentName = environmentName;
                    }));

            Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteStoragePath(storagePath);
        }
    }

    private static WebApplication CreateApplication(
        string storagePath,
        params string[] additionalArguments)
    {
        return CreateApplication(storagePath, configureBuilder: null, additionalArguments);
    }

    private static WebApplication CreateApplication(
        string storagePath,
        Action<WebApplicationBuilder> configureBuilder,
        params string[] additionalArguments)
    {
        string[] arguments = new[]
        {
            "--DataProtection:StorageLocation",
            storagePath,
        }.Concat(additionalArguments).ToArray();

        return Program.BuildApplication(
            arguments,
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Environment.EnvironmentName = Environments.Development;
                configureBuilder?.Invoke(builder);
            });
    }

    private static string CreateCookie(
        WebApplication application,
        params Claim[] claims)
    {
        CookieAuthenticationOptions options = application.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(SharedCookieConstants.AuthenticationScheme);
        DateTimeOffset issuedUtc = DateTimeOffset.UtcNow;
        var identity = new ClaimsIdentity(
            claims,
            SharedCookieConstants.AuthenticationScheme,
            ClaimTypes.Name,
            SharedCookieConstants.RoleClaimType);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IssuedUtc = issuedUtc,
                ExpiresUtc = issuedUtc.Add(SharedCookieConstants.Expiration),
            },
            SharedCookieConstants.AuthenticationScheme);
        return options.TicketDataFormat.Protect(ticket);
    }

    private static async Task<HttpResponseMessage> SendWithCookieAsync(
        HttpClient client,
        string path,
        string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation(
            "Cookie",
            SharedCookieConstants.CookieName + "=" + cookie);
        return await client.SendAsync(request);
    }

    private static string CreateStoragePath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "data-protection",
            Guid.NewGuid().ToString("N"));
    }

    private static IConfiguration CreateConfiguration(string environmentName)
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .Build();
    }

    private static SharedDataProtectionServiceCollectionExtensions.StorageCredentialMode GetSystemAssignedManagedIdentityMode(
        IConfiguration configuration)
    {
        return SharedDataProtectionServiceCollectionExtensions.GetStorageCredentialMode(
            new ConfigurationBuilder()
                .AddConfiguration(configuration)
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>(
                        Constants.StorageUseManagedIdentityPropertyName,
                        bool.TrueString),
                    new KeyValuePair<string, string>(
                        Constants.StorageManagedIdentityClientIdPropertyName,
                        string.Empty),
                })
                .Build());
    }

    private static void DeleteStoragePath(string storagePath)
    {
        if (Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
            && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("The repository root could not be found.");
    }
}
