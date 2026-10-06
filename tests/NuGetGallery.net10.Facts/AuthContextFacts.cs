// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery;

public class AuthContextFacts
{
    [Theory]
    [InlineData("anonymous")]
    [InlineData("authenticated")]
    [InlineData("invalid")]
    [InlineData("expired")]
    public async Task ReportsOnlyAllowlistedIdentityWithoutIssuingOrRenewingCookies(string requestKind)
    {
        string storagePath = Path.Combine(Path.GetTempPath(), "gallery-auth-context", Guid.NewGuid().ToString("N"));
        await using WebApplication application = CreateApplication(storagePath, Environments.Development);
        try
        {
            await application.StartAsync();
            using HttpClient client = application.GetTestClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, AuthContextController.Route);
            if (requestKind != "anonymous")
            {
                string cookie = requestKind == "invalid" ? "not-a-ticket" : CreateCookie(application, requestKind == "expired");
                request.Headers.TryAddWithoutValidation("Cookie", SharedCookieConstants.CookieName + "=" + cookie);
            }
            // Even supplied secrets in unrelated request data must not be reflected.
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer secret-token");
            using HttpResponseMessage response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl.NoStore);
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);

            string body = await response.Content.ReadAsStringAsync();
            using JsonDocument json = JsonDocument.Parse(body);
            JsonElement root = json.RootElement;
            Assert.Equal(
                new[] { "authenticationType", "host", "isAuthenticated", "name", "nameIdentifier", "roles" },
                root.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray());
            bool authenticated = requestKind == "authenticated";
            Assert.Equal("NuGetGallery.net10", root.GetProperty("host").GetString());
            Assert.Equal(authenticated, root.GetProperty("isAuthenticated").GetBoolean());
            Assert.Equal(authenticated ? "LocalUser" : null, root.GetProperty("authenticationType").GetString());
            Assert.Equal(authenticated ? "interop-user" : null, root.GetProperty("name").GetString());
            Assert.Equal(authenticated ? "interop-user" : null, root.GetProperty("nameIdentifier").GetString());
            Assert.Equal(
                authenticated ? new[] { "Administrators", "PackageOwners" } : Array.Empty<string>(),
                root.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ToArray());
            Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("credential", body, StringComparison.OrdinalIgnoreCase);

            // A real MVC controller endpoint won over the host's actual YARP catch-all.
            Endpoint[] endpoints = ((IEndpointRouteBuilder)application).DataSources
                .SelectMany(source => source.Endpoints).ToArray();
            Endpoint diagnostic = Assert.Single(endpoints.Where(endpoint =>
                endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerTypeInfo.AsType()
                    == typeof(AuthContextController)));
            Assert.Equal(nameof(AuthContextController.Get),
                diagnostic.Metadata.GetMetadata<ControllerActionDescriptor>().ActionName);
            Assert.True(((RouteEndpoint)diagnostic).Order <
                ((RouteEndpoint)Assert.Single(endpoints.Where(endpoint =>
                    endpoint.DisplayName == "Legacy NuGetGallery fallback"))).Order);
        }
        finally
        {
            await application.StopAsync();
            if (Directory.Exists(storagePath))
            {
                Directory.Delete(storagePath, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("Custom")]
    public async Task NonDevelopmentMappingReservesThePathWithoutDiscoveringTheController(string environment)
    {
        // Exercise the exact Program mapping without Production's independent Azure startup gate.
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment,
        });
        builder.WebHost.UseTestServer();
        await using WebApplication application = builder.Build();
        Program.MapAuthContextEndpoints(application);
        application.Map("/{**catch-all}", () => Results.Text("legacy-marker")).WithOrder(int.MaxValue);
        await application.StartAsync();
        using HttpClient client = application.GetTestClient();
        foreach (HttpMethod method in new[] { HttpMethod.Get, HttpMethod.Post })
        {
            using var request = new HttpRequestMessage(method, AuthContextController.Route);
            using HttpResponseMessage response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(response.Headers.CacheControl.NoStore);
            Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
        Assert.IsType<NotFoundResult>(new AuthContextController(application.Environment).Get());
        Assert.DoesNotContain(((IEndpointRouteBuilder)application).DataSources.SelectMany(source => source.Endpoints),
            endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() != null);
        Assert.Equal("legacy-marker", await client.GetStringAsync("/some-legacy-path"));
    }

    [Fact]
    public async Task FullStagingHostDoesNotServeOrProxyDiagnosticInformation()
    {
        string storagePath = Path.Combine(Path.GetTempPath(), "gallery-auth-context", Guid.NewGuid().ToString("N"));
        await using WebApplication application = CreateApplication(storagePath, Environments.Staging);
        try
        {
            await application.StartAsync();
            using HttpClient client = application.GetTestClient();
            using HttpResponseMessage response = await client.GetAsync(AuthContextController.Route);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(response.Headers.CacheControl.NoStore);
            Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        }
        finally
        {
            await application.StopAsync();
            if (Directory.Exists(storagePath))
            {
                Directory.Delete(storagePath, recursive: true);
            }
        }
    }

    private static WebApplication CreateApplication(string storagePath, string environment)
    {
        return Program.BuildApplication(
            new[]
            {
                "--DataProtection:StorageLocation", storagePath,
                "--LegacyProxy:PublicOrigins:0", "https://localhost:7150",
            },
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Environment.EnvironmentName = environment;
            });
    }

    private static string CreateCookie(WebApplication application, bool expired)
    {
        CookieAuthenticationOptions options = application.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(SharedCookieConstants.AuthenticationScheme);
        // Past the sliding-renewal midpoint: Core must still only read, not refresh.
        DateTimeOffset issuedUtc = DateTimeOffset.UtcNow.AddHours(expired ? -7 : -4);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(SharedCookieConstants.NameClaimType, "interop-user"),
            new Claim(SharedCookieConstants.NameIdentifierClaimType, "interop-user"),
            new Claim(SharedCookieConstants.RoleClaimType, "PackageOwners"),
            new Claim(SharedCookieConstants.RoleClaimType, "Administrators"),
            new Claim(SharedCookieConstants.RoleClaimType, "Administrators"),
            new Claim(ClaimTypes.Email, "secret-email@example.test"),
            new Claim(SharedCookieConstants.ExternalCredentialIdentitiesClaimType, "secret-credential"),
            new Claim("access_token", "secret-token"),
        }, SharedCookieConstants.AuthenticationScheme,
            SharedCookieConstants.NameClaimType, SharedCookieConstants.RoleClaimType);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IssuedUtc = issuedUtc,
                ExpiresUtc = issuedUtc.Add(SharedCookieConstants.Expiration),
                Items = { ["secret-property"] = "secret-value" },
            }, SharedCookieConstants.AuthenticationScheme);
        return options.TicketDataFormat.Protect(ticket);
    }
}
