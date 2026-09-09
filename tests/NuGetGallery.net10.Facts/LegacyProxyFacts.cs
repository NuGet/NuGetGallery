// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NuGetGallery.Authentication;
using Xunit;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;

namespace NuGetGallery;

public class LegacyProxyFacts
{
    [Fact]
    public async Task LocalRoutesTakePrecedenceAndAllOtherRoutesReachLegacy()
    {
        var requests = new List<string>();
        await using ProxyHarness harness = await ProxyHarness.CreateAsync(async context =>
        {
            requests.Add(context.Request.Path);
            await context.Response.WriteAsync("legacy");
        });

        Assert.Equal("NuGetGallery.net10", await harness.Client.GetStringAsync("/_local"));
        Assert.Equal(HttpStatusCode.OK, (await harness.Client.GetAsync("/_health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await harness.Client.GetAsync("/_ready")).StatusCode);
        Assert.Empty(requests);

        Assert.Equal("legacy", await harness.Client.GetStringAsync("/not-migrated"));
        Assert.Equal("legacy", await harness.Client.GetStringAsync("/nested/_health"));
        Assert.Equal("legacy", await harness.Client.GetStringAsync("/"));
        Assert.Equal(new[] { "/not-migrated", "/nested/_health", "/" }, requests);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task PreservesMethodAndResponseStatus(string method)
    {
        string receivedMethod = null;
        await using ProxyHarness harness = await ProxyHarness.CreateAsync(context =>
        {
            receivedMethod = context.Request.Method;
            context.Response.StatusCode = StatusCodes.Status202Accepted;
            context.Response.Headers["X-Legacy"] = "true";
            return Task.CompletedTask;
        });

        using var request = new HttpRequestMessage(new HttpMethod(method), "/method");
        using HttpResponseMessage response = await harness.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("true", response.Headers.GetValues("X-Legacy").Single());
        Assert.Equal(method, receivedMethod);
    }

    [Fact]
    public async Task StreamsMultipartPostAndPreservesCookies()
    {
        var firstBodyRead = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBody = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        string receivedContentType = null;
        string receivedCookie = null;
        string receivedBody = null;

        await using ProxyHarness harness = await ProxyHarness.CreateAsync(async context =>
        {
            receivedContentType = context.Request.ContentType;
            receivedCookie = context.Request.Headers.Cookie;

            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var buffer = new char[5];
            int read = await reader.ReadAsync(buffer.AsMemory());
            firstBodyRead.SetResult(true);
            await releaseBody.Task;
            receivedBody = new string(buffer, 0, read) + await reader.ReadToEndAsync();

            context.Response.Headers.Append(
                HeaderNames.SetCookie,
                ".AspNet.LocalUser=updated; path=/; secure; httponly");
            await context.Response.WriteAsync("uploaded");
        });

        using var content = new MultipartFormDataContent("gallery-boundary");
        content.Add(
            new GatedStreamingContent(
                Encoding.UTF8.GetBytes("streamed package bytes"),
                firstBodyRead,
                releaseBody),
            "package",
            "package.nupkg");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/package")
        {
            Content = content,
        };
        request.Headers.TryAddWithoutValidation(
            HeaderNames.Cookie,
            ".AspNet.LocalUser=original");

        Task<HttpResponseMessage> sendTask = harness.Client.SendAsync(request);
        await firstBodyRead.Task.WaitAsync(TimeSpan.FromSeconds(5));
        releaseBody.SetResult(true);
        using HttpResponseMessage response = await sendTask;

        Assert.Equal("uploaded", await response.Content.ReadAsStringAsync());
        Assert.StartsWith("multipart/form-data; boundary=\"gallery-boundary\"", receivedContentType);
        Assert.Equal(".AspNet.LocalUser=original", receivedCookie);
        Assert.Contains("streamed package bytes", receivedBody);
        Assert.Equal(
            ".AspNet.LocalUser=updated; path=/; secure; httponly",
            response.Headers.GetValues(HeaderNames.SetCookie).Single());
    }

    [Fact]
    public async Task ReaderOnlyAuthenticationLeavesTheLegacyHostAsTheSingleCookieRefresher()
    {
        string forwardedCookie = null;
        await using ProxyHarness harness = await ProxyHarness.CreateAsync(context =>
        {
            forwardedCookie = context.Request.Headers.Cookie;
            context.Response.Headers.Append(
                HeaderNames.SetCookie,
                SharedCookieConstants.CookieName + "=legacy-renewal; path=/; secure; httponly");
            return context.Response.WriteAsync("authenticated");
        });

        CookieAuthenticationOptions options = harness.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(SharedCookieConstants.AuthenticationScheme);
        DateTimeOffset issuedUtc = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(4));
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, "proxy-user") },
                SharedCookieConstants.AuthenticationScheme)),
            new AuthenticationProperties
            {
                IssuedUtc = issuedUtc,
                ExpiresUtc = issuedUtc.Add(SharedCookieConstants.Expiration),
            },
            SharedCookieConstants.AuthenticationScheme);
        string cookie = options.TicketDataFormat.Protect(ticket);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/legacy-authenticated");
        request.Headers.TryAddWithoutValidation(
            HeaderNames.Cookie,
            SharedCookieConstants.CookieName + "=" + cookie);
        using HttpResponseMessage response = await harness.Client.SendAsync(request);

        Assert.Equal("authenticated", await response.Content.ReadAsStringAsync());
        Assert.Equal(SharedCookieConstants.CookieName + "=" + cookie, forwardedCookie);
        Assert.Equal(
            SharedCookieConstants.CookieName + "=legacy-renewal; path=/; secure; httponly",
            response.Headers.GetValues(HeaderNames.SetCookie).Single());
    }

    [Fact]
    public async Task InvalidSharedCookieStillUsesAnonymousLegacyFallback()
    {
        int legacyRequests = 0;
        string forwardedCookie = null;
        await using ProxyHarness harness = await ProxyHarness.CreateAsync(async context =>
        {
            legacyRequests++;
            forwardedCookie = context.Request.Headers.Cookie;
            await context.Response.WriteAsync(
                context.User.Identity?.IsAuthenticated == true ? "authenticated" : "anonymous");
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/legacy-anonymous");
        request.Headers.TryAddWithoutValidation(
            HeaderNames.Cookie,
            SharedCookieConstants.CookieName + "=not-a-valid-ticket");
        using HttpResponseMessage response = await harness.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("anonymous", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, legacyRequests);
        Assert.Equal(
            SharedCookieConstants.CookieName + "=not-a-valid-ticket",
            forwardedCookie);
        Assert.False(response.Headers.TryGetValues(HeaderNames.SetCookie, out _));
    }

    [Fact]
    public async Task StreamsARequestBodyWithoutBufferingIt()
    {
        const int testLimit = 64 * 1024;
        long receivedLength = 0;
        await using ProxyHarness harness = await ProxyHarness.CreateAsync(async context =>
        {
            var buffer = new byte[8192];
            int read;
            while ((read = await context.Request.Body.ReadAsync(buffer)) != 0)
            {
                receivedLength += read;
            }

            context.Response.StatusCode = StatusCodes.Status204NoContent;
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/near-limit")
        {
            Content = new GeneratedContent(testLimit - 1),
        };
        using HttpResponseMessage response = await harness.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(testLimit - 1, receivedLength);
    }

    [Fact]
    public async Task StreamsTheLegacyResponseWithoutWaitingForCompletion()
    {
        var firstChunkSent = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponse = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using ProxyHarness harness = await ProxyHarness.CreateAsync(async context =>
        {
            await context.Response.WriteAsync("first");
            await context.Response.Body.FlushAsync();
            firstChunkSent.SetResult(true);
            await releaseResponse.Task;
            await context.Response.WriteAsync("second");
        });

        try
        {
            using HttpResponseMessage response = await harness.Client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "/stream"),
                HttpCompletionOption.ResponseHeadersRead);
            await firstChunkSent.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await using Stream stream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[5];
            int read = await stream.ReadAsync(buffer);

            Assert.Equal("first", Encoding.UTF8.GetString(buffer, 0, read));

            releaseResponse.SetResult(true);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            Assert.Equal("second", await reader.ReadToEndAsync());
        }
        finally
        {
            releaseResponse.TrySetResult(true);
        }
    }

    [Fact]
    public async Task PreservesRangesConditionalRequestsAndDownloadHeaders()
    {
        string range = null;
        string ifNoneMatch = null;
        string ifModifiedSince = null;
        var lastModified = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        await using ProxyHarness harness = await ProxyHarness.CreateAsync(async context =>
        {
            range = context.Request.Headers.Range;
            ifNoneMatch = context.Request.Headers.IfNoneMatch;
            ifModifiedSince = context.Request.Headers.IfModifiedSince;

            context.Response.StatusCode = StatusCodes.Status206PartialContent;
            context.Response.Headers.ContentRange = "bytes 10-12/100";
            context.Response.Headers.ETag = "\"package-etag\"";
            context.Response.Headers.LastModified = lastModified.ToString("R");
            context.Response.Headers.CacheControl = "public, max-age=3600";
            context.Response.Headers.ContentDisposition = "attachment; filename=package.nupkg";
            context.Response.Headers.AcceptRanges = "bytes";
            await context.Response.WriteAsync("abc");
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/download");
        request.Headers.Range = new RangeHeaderValue(10, 12);
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"client-etag\""));
        request.Headers.IfModifiedSince = lastModified.AddDays(-1);
        using HttpResponseMessage response = await harness.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("bytes=10-12", range);
        Assert.Equal("\"client-etag\"", ifNoneMatch);
        Assert.Equal(lastModified.AddDays(-1).ToString("R"), ifModifiedSince);
        Assert.Equal("bytes 10-12/100", response.Content.Headers.ContentRange.ToString());
        Assert.Equal("\"package-etag\"", response.Headers.ETag.Tag);
        Assert.Equal(lastModified, response.Content.Headers.LastModified);
        Assert.Equal("public, max-age=3600", response.Headers.CacheControl.ToString());
        Assert.Equal("attachment; filename=package.nupkg", response.Content.Headers.ContentDisposition.ToString());
        Assert.Equal("bytes", response.Headers.AcceptRanges.Single());
    }

    [Fact]
    public async Task PreservesTheRawEscapedPathAndQuery()
    {
        const string requestedTarget =
            "/packages/a%2Fb/%2525?q=a%2Fb&literal=%25&space=a%20b";
        string rawTarget = null;
        string outgoingTarget = null;
        await using ProxyHarness harness = await ProxyHarness.CreateAsync(
            async context =>
            {
                rawTarget = context.Features.Get<IHttpRequestFeature>().RawTarget;
                if (string.IsNullOrEmpty(rawTarget))
                {
                    rawTarget = context.Request.GetEncodedPathAndQuery();
                }
                await context.Response.WriteAsync("encoded");
            },
            request => outgoingTarget = request.RequestUri.OriginalString);

        Assert.Equal(
            "encoded",
            await harness.Client.GetStringAsync(requestedTarget));
        Assert.Equal(
            "http://legacy.test/packages/a%2Fb/%25?q=a%2Fb&literal=%25&space=a%20b",
            outgoingTarget);
        Assert.Equal(
            "/packages/a%2Fb/%25?q=a%2Fb&literal=%25&space=a%20b",
            rawTarget);

        var context = new DefaultHttpContext();
        context.Features.Get<IHttpRequestFeature>().RawTarget = requestedTarget;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("www.nuget.test");
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        var proxyRequest = new HttpRequestMessage();
        var transformer = new LegacyProxyTransformer(
            Options.Create(new LegacyProxyOptions { Origin = "http://legacy.test" }));

        await transformer.TransformRequestAsync(
            context,
            proxyRequest,
            "http://legacy.test",
            CancellationToken.None);

        Assert.Equal(
            "http://legacy.test" + requestedTarget,
            proxyRequest.RequestUri.OriginalString);
    }

    [Fact]
    public async Task PreservesErrorsAndRewritesLegacyRedirectsToThePublicOrigin()
    {
        await using ProxyHarness harness = await ProxyHarness.CreateAsync(async context =>
        {
            if (context.Request.Path == "/redirect")
            {
                context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
                context.Response.Headers.Location = "http://legacy.test/account/login?return=%2Fpackage";
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers.Server = "Internal-Legacy-Origin";
                context.Response.Headers["X-Powered-By"] = "ASP.NET";
                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("legacy unavailable");
        });

        using HttpResponseMessage redirect = await harness.Client.GetAsync("/redirect");
        Assert.Equal(HttpStatusCode.TemporaryRedirect, redirect.StatusCode);
        Assert.Equal(
            "https://www.nuget.test/account/login?return=%2Fpackage",
            redirect.Headers.Location.AbsoluteUri);
        Assert.Equal("no-store", redirect.Headers.CacheControl.ToString());
        Assert.DoesNotContain("legacy.test", redirect.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(redirect.Headers.Contains(HeaderNames.Server));
        Assert.False(redirect.Headers.Contains("X-Powered-By"));

        using HttpResponseMessage error = await harness.Client.GetAsync("/error");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Equal("legacy unavailable", await error.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReplacesForwardingHeadersWithTrustedPublicRequestValues()
    {
        var received = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using ProxyHarness harness = await ProxyHarness.CreateAsync(context =>
        {
            foreach (string name in new[]
            {
                HeaderNames.Host,
                "Forwarded",
                "X-Forwarded-For",
                "X-Forwarded-Host",
                "X-Forwarded-Proto",
                "X-Forwarded-Prefix",
            })
            {
                received[name] = context.Request.Headers[name];
            }

            return Task.CompletedTask;
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/forwarded");
        request.Headers.TryAddWithoutValidation("Forwarded", "for=attacker;host=attacker");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.9");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "www.nuget.test");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Prefix", "/attacker");
        using HttpResponseMessage response = await harness.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("www.nuget.test", received[HeaderNames.Host]);
        Assert.Equal("203.0.113.9", received["X-Forwarded-For"]);
        Assert.Equal("www.nuget.test", received["X-Forwarded-Host"]);
        Assert.Equal("https", received["X-Forwarded-Proto"]);
        Assert.Contains("for=\"203.0.113.9\"", received["Forwarded"]);
        Assert.DoesNotContain("attacker", received["Forwarded"], StringComparison.OrdinalIgnoreCase);
        Assert.Null(received["X-Forwarded-Prefix"]);
    }

    [Fact]
    public async Task PropagatesClientCancellationToTheLegacyOrigin()
    {
        var legacyCanceled = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using ProxyHarness harness = await ProxyHarness.CreateAsync(
            _ => Task.CompletedTask,
            new CancellationObservingHandler(legacyCanceled));
        using var cancellation = new CancellationTokenSource();

        Task<HttpResponseMessage> sendTask = harness.Client.GetAsync(
            "/cancel",
            cancellation.Token);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sendTask);
        Assert.True(await legacyCanceled.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task TimesOutAnInactiveLegacyOrigin()
    {
        var legacyCanceled = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using ProxyHarness harness = await ProxyHarness.CreateAsync(
            _ => Task.CompletedTask,
            new CancellationObservingHandler(legacyCanceled),
            "--LegacyProxy:ActivityTimeout",
            "00:00:00.100");

        using HttpResponseMessage response = await harness.Client.GetAsync("/timeout");

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.True(await legacyCanceled.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task DoesNotRetryANonIdempotentRequest()
    {
        var handler = new CountingFailureHandler();
        await using ProxyHarness harness = await ProxyHarness.CreateAsync(
            _ => Task.CompletedTask,
            handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/no-retry")
        {
            Content = new StringContent("one-shot"),
        };

        using HttpResponseMessage response = await harness.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task RejectsASelfReferentialDestinationBeforeServing()
    {
        string storagePath = ProxyHarness.CreateStoragePath();
        await using WebApplication application = Program.BuildApplication(
            new[]
            {
                "--DataProtection:StorageLocation",
                storagePath,
                "--LegacyProxy:Origin",
                "http://localhost:5150",
                "--urls",
                "http://127.0.0.1:5150",
            },
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Environment.EnvironmentName = Environments.Development;
            });

        try
        {
            OptionsValidationException exception = await Assert.ThrowsAsync<OptionsValidationException>(
                () => application.StartAsync());
            Assert.Contains("configured listener", exception.Message);
        }
        finally
        {
            ProxyHarness.DeleteStoragePath(storagePath);
        }
    }

    [Fact]
    public async Task RejectsAPublicOriginMatchAcrossTlsTermination()
    {
        string storagePath = ProxyHarness.CreateStoragePath();
        await using WebApplication application = Program.BuildApplication(
            new[]
            {
                "--DataProtection:StorageLocation",
                storagePath,
                "--LegacyProxy:Origin",
                "http://gallery.example.test",
                "--LegacyProxy:PublicOrigins:0",
                "https://gallery.example.test",
            },
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Environment.EnvironmentName = Environments.Development;
            });

        try
        {
            OptionsValidationException exception = await Assert.ThrowsAsync<OptionsValidationException>(
                () => application.StartAsync());
            Assert.Contains("matches public application origin", exception.Message);
        }
        finally
        {
            ProxyHarness.DeleteStoragePath(storagePath);
        }
    }

    [Theory]
    [InlineData("ftp://legacy.test")]
    [InlineData("http://user@legacy.test")]
    [InlineData("http://legacy.test/base")]
    [InlineData("http://legacy.test?query=true")]
    public async Task RejectsAnInvalidLegacyOriginBeforeServing(string origin)
    {
        string storagePath = ProxyHarness.CreateStoragePath();
        await using WebApplication application = Program.BuildApplication(
            new[]
            {
                "--DataProtection:StorageLocation",
                storagePath,
                "--LegacyProxy:Origin",
                origin,
            },
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Environment.EnvironmentName = Environments.Development;
            });

        try
        {
            OptionsValidationException exception = await Assert.ThrowsAsync<OptionsValidationException>(
                () => application.StartAsync());
            Assert.Contains("absolute HTTP(S) origin", exception.Message);
        }
        finally
        {
            ProxyHarness.DeleteStoragePath(storagePath);
        }
    }

    [Fact]
    public async Task RejectsAnInvalidPublicOriginBeforeServing()
    {
        string storagePath = ProxyHarness.CreateStoragePath();
        await using WebApplication application = Program.BuildApplication(
            new[]
            {
                "--DataProtection:StorageLocation",
                storagePath,
                "--LegacyProxy:PublicOrigins:0",
                "https://public.example.test/base",
            },
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Environment.EnvironmentName = Environments.Development;
            });

        try
        {
            OptionsValidationException exception = await Assert.ThrowsAsync<OptionsValidationException>(
                () => application.StartAsync());
            Assert.Contains("Every LegacyProxy:PublicOrigins value", exception.Message);
        }
        finally
        {
            ProxyHarness.DeleteStoragePath(storagePath);
        }
    }

    [Theory]
    [InlineData("/raw/%41?query=%41")]
    [InlineData("/raw/a%2Fb?query=%2F")]
    [InlineData("/raw/a/../b?dot=..")]
    [InlineData("/raw/a/%2E%2E/b?dot=%2e%2E")]
    public async Task PreservesRawTargetThroughKestrelAndSocketsHttpHandler(string rawTarget)
    {
        await using var harness = await ProxyHarness.RealKestrelProxyHarness.CreateAsync();

        string response = await SendRawHttpRequestAsync(harness.ProxyOrigin, rawTarget);

        Assert.StartsWith("HTTP/1.1 200", response);
        Assert.Equal(
            rawTarget,
            await harness.LegacyRawTarget.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task RejectsUnsafeRawTargetBeforeContactingTheOrigin()
    {
        await using var harness = await ProxyHarness.RealKestrelProxyHarness.CreateAsync();

        string response = await SendRawHttpRequestAsync(
            harness.ProxyOrigin,
            "/invalid/%GG");

        Assert.StartsWith("HTTP/1.1 400", response);
        Assert.Equal(1, harness.ProxyRequestCount);
        Assert.Equal(0, harness.LegacyRequestCount);
    }

    private static async Task<string> SendRawHttpRequestAsync(
        Uri origin,
        string rawTarget)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(origin.Host, origin.Port);
        await using NetworkStream stream = client.GetStream();
        byte[] request = Encoding.ASCII.GetBytes(
            $"GET {rawTarget} HTTP/1.1\r\nHost: public.example.test\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(request);
        await stream.FlushAsync();

        using var reader = new StreamReader(stream, Encoding.ASCII);
        return await reader.ReadToEndAsync();
    }

    private static Uri GetServerOrigin(WebApplication application)
    {
        IServer server = application.Services.GetRequiredService<IServer>();
        string address = server.Features
            .Get<IServerAddressesFeature>()
            .Addresses
            .Single();
        return new Uri(address);
    }

    private sealed class ProxyHarness : IAsyncDisposable
    {
        private readonly WebApplication _legacy;
        private readonly WebApplication _proxy;
        private readonly string _storagePath;

        private ProxyHarness(
            WebApplication legacy,
            WebApplication proxy,
            string storagePath)
        {
            _legacy = legacy;
            _proxy = proxy;
            _storagePath = storagePath;
            Client = proxy.GetTestClient();
            Client.BaseAddress = new Uri("https://www.nuget.test");
        }

        public sealed class RealKestrelProxyHarness : IAsyncDisposable
        {
            private readonly WebApplication _legacy;
            private readonly WebApplication _proxy;
            private readonly string _storagePath;
            private readonly RequestCounter _legacyRequestCount;
            private readonly RequestCounter _proxyRequestCount;

            private RealKestrelProxyHarness(
                WebApplication legacy,
                WebApplication proxy,
                string storagePath,
                TaskCompletionSource<string> legacyRawTarget,
                RequestCounter legacyRequestCount,
                RequestCounter proxyRequestCount)
            {
                _legacy = legacy;
                _proxy = proxy;
                _storagePath = storagePath;
                _legacyRequestCount = legacyRequestCount;
                _proxyRequestCount = proxyRequestCount;
                LegacyRawTarget = legacyRawTarget;
                ProxyOrigin = GetServerOrigin(proxy);
            }

            public TaskCompletionSource<string> LegacyRawTarget { get; }
            public int LegacyRequestCount => _legacyRequestCount.Value;
            public int ProxyRequestCount => _proxyRequestCount.Value;
            public Uri ProxyOrigin { get; }

            public static async Task<RealKestrelProxyHarness> CreateAsync()
            {
                var legacyRawTarget = new TaskCompletionSource<string>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var legacyRequestCount = new RequestCounter();
                var proxyRequestCount = new RequestCounter();
                WebApplicationBuilder legacyBuilder = WebApplication.CreateBuilder();
                legacyBuilder.WebHost.ConfigureKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, 0);
                });
                legacyBuilder.Configuration["AllowedHosts"] = "*";
                WebApplication legacy = legacyBuilder.Build();
                legacy.Run(async context =>
                {
                    legacyRequestCount.Increment();
                    legacyRawTarget.TrySetResult(
                        context.Features.Get<IHttpRequestFeature>().RawTarget);
                    await context.Response.WriteAsync("legacy");
                });
                await legacy.StartAsync();

                string storagePath = ProxyHarness.CreateStoragePath();
                WebApplication proxy = Program.BuildApplication(
                    new[]
                    {
                        "--DataProtection:StorageLocation",
                        storagePath,
                        "--LegacyProxy:Origin",
                        GetServerOrigin(legacy).AbsoluteUri,
                        "--LegacyProxy:PublicOrigins:0",
                        "https://public.example.test",
                        "--AllowedHosts",
                        "public.example.test",
                        "--environment",
                        Environments.Development,
                    },
                    builder =>
                    {
                        builder.WebHost.ConfigureKestrel(options =>
                        {
                            options.Listen(IPAddress.Loopback, 0);
                        });
                        builder.Environment.EnvironmentName = Environments.Development;
                        builder.Services.AddSingleton<IStartupFilter>(
                            new RequestCountingStartupFilter(proxyRequestCount.Increment));
                    });

                try
                {
                    await proxy.StartAsync();
                    return new RealKestrelProxyHarness(
                        legacy,
                        proxy,
                        storagePath,
                        legacyRawTarget,
                        legacyRequestCount,
                        proxyRequestCount);
                }
                catch
                {
                    await proxy.DisposeAsync();
                    await legacy.DisposeAsync();
                    ProxyHarness.DeleteStoragePath(storagePath);
                    throw;
                }
            }

            public async ValueTask DisposeAsync()
            {
                await _proxy.StopAsync();
                await _proxy.DisposeAsync();
                await _legacy.StopAsync();
                await _legacy.DisposeAsync();
                ProxyHarness.DeleteStoragePath(_storagePath);
            }

            private sealed class RequestCounter
            {
                private int _value;

                public int Value => Volatile.Read(ref _value);

                public void Increment()
                {
                    Interlocked.Increment(ref _value);
                }
            }
        }

        public HttpClient Client { get; }

        public static async Task<ProxyHarness> CreateAsync(
            RequestDelegate legacyHandler,
            params string[] additionalArguments)
        {
            return await CreateAsync(
                legacyHandler,
                proxyHandler: null,
                requestObserver: null,
                additionalArguments);
        }

        public static async Task<ProxyHarness> CreateAsync(
            RequestDelegate legacyHandler,
            Action<HttpRequestMessage> requestObserver,
            params string[] additionalArguments)
        {
            return await CreateAsync(
                legacyHandler,
                proxyHandler: null,
                requestObserver,
                additionalArguments);
        }

        public static async Task<ProxyHarness> CreateAsync(
            RequestDelegate legacyHandler,
            HttpMessageHandler proxyHandler,
            params string[] additionalArguments)
        {
            return await CreateAsync(
                legacyHandler,
                proxyHandler,
                requestObserver: null,
                additionalArguments);
        }

        private static async Task<ProxyHarness> CreateAsync(
            RequestDelegate legacyHandler,
            HttpMessageHandler proxyHandler,
            Action<HttpRequestMessage> requestObserver,
            params string[] additionalArguments)
        {
            WebApplicationBuilder legacyBuilder = WebApplication.CreateBuilder();
            legacyBuilder.WebHost.UseTestServer();
            legacyBuilder.Configuration["AllowedHosts"] = "*";
            WebApplication legacy = legacyBuilder.Build();
            legacy.Run(legacyHandler);
            await legacy.StartAsync();

            string storagePath = CreateStoragePath();
            string[] arguments = new[]
            {
                "--DataProtection:StorageLocation",
                storagePath,
                "--LegacyProxy:Origin",
                "http://legacy.test",
                "--ForwardedHeaders:AllowedHosts:0",
                "www.nuget.test",
                "--AllowedHosts",
                "www.nuget.test",
                "--environment",
                Environments.Development,
            }.Concat(additionalArguments).ToArray();

            WebApplication proxy = Program.BuildApplication(
                arguments,
                builder =>
                {
                    builder.WebHost.UseTestServer();
                    builder.Environment.EnvironmentName = Environments.Development;
                    builder.Services.AddSingleton<IStartupFilter, LoopbackConnectionStartupFilter>();
                    HttpMessageHandler destinationHandler =
                        proxyHandler
                        ?? new TestServerUriCanonicalizingHandler(
                            legacy.GetTestServer().CreateHandler());
                    if (requestObserver != null)
                    {
                        destinationHandler = new ObservingHandler(
                            destinationHandler,
                            requestObserver);
                    }
                    builder.Services.AddSingleton<ILegacyProxyHttpClient>(
                        new TestLegacyProxyHttpClient(destinationHandler));
                });

            try
            {
                await proxy.StartAsync();
                return new ProxyHarness(legacy, proxy, storagePath);
            }
            catch
            {
                await proxy.DisposeAsync();
                await legacy.DisposeAsync();
                DeleteStoragePath(storagePath);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _proxy.StopAsync();
            await _proxy.DisposeAsync();
            await _legacy.StopAsync();
            await _legacy.DisposeAsync();
            DeleteStoragePath(_storagePath);
        }

        public IServiceProvider Services => _proxy.Services;

        public static string CreateStoragePath()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                "proxy-data-protection",
                Guid.NewGuid().ToString("N"));
        }

        public static void DeleteStoragePath(string storagePath)
        {
            if (Directory.Exists(storagePath))
            {
                Directory.Delete(storagePath, recursive: true);
            }
        }
    }

    private sealed class TestLegacyProxyHttpClient : ILegacyProxyHttpClient
    {
        public TestLegacyProxyHttpClient(HttpMessageHandler handler)
        {
            Invoker = new HttpMessageInvoker(handler, disposeHandler: true);
        }

        public HttpMessageInvoker Invoker { get; }

        public void Dispose()
        {
            Invoker.Dispose();
        }
    }

    private sealed class LoopbackConnectionStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return application =>
            {
                application.Use(async (context, continuePipeline) =>
                {
                    context.Connection.RemoteIpAddress = IPAddress.Loopback;
                    await continuePipeline();
                });
                next(application);
            };
        }
    }

    private sealed class RequestCountingStartupFilter : IStartupFilter
    {
        private readonly Action _increment;

        public RequestCountingStartupFilter(Action increment)
        {
            _increment = increment;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return application =>
            {
                application.Use(async (context, continuePipeline) =>
                {
                    _increment();
                    await continuePipeline();
                });
                next(application);
            };
        }
    }

    private sealed class GatedStreamingContent : HttpContent
    {
        private readonly byte[] _content;
        private readonly TaskCompletionSource<bool> _firstBodyRead;
        private readonly TaskCompletionSource<bool> _releaseBody;

        public GatedStreamingContent(
            byte[] content,
            TaskCompletionSource<bool> firstBodyRead,
            TaskCompletionSource<bool> releaseBody)
        {
            _content = content;
            _firstBodyRead = firstBodyRead;
            _releaseBody = releaseBody;
            Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        }

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext context)
        {
            int midpoint = _content.Length / 2;
            await stream.WriteAsync(_content.AsMemory(0, midpoint));
            await stream.FlushAsync();
            await _firstBodyRead.Task;
            await _releaseBody.Task;
            await stream.WriteAsync(_content.AsMemory(midpoint));
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _content.Length;
            return true;
        }
    }

    private sealed class GeneratedContent : HttpContent
    {
        private readonly int _length;

        public GeneratedContent(int length)
        {
            _length = length;
        }

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext context)
        {
            var buffer = new byte[8192];
            int remaining = _length;
            while (remaining > 0)
            {
                int count = Math.Min(buffer.Length, remaining);
                await stream.WriteAsync(buffer.AsMemory(0, count));
                remaining -= count;
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _length;
            return true;
        }
    }

    private sealed class CancellationObservingHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<bool> _canceled;

        public CancellationObservingHandler(TaskCompletionSource<bool> canceled)
        {
            _canceled = canceled;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("The request was expected to be canceled.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _canceled.TrySetResult(true);
                throw;
            }
        }
    }

    private sealed class ObservingHandler : DelegatingHandler
    {
        private readonly Action<HttpRequestMessage> _observer;

        public ObservingHandler(
            HttpMessageHandler innerHandler,
            Action<HttpRequestMessage> observer)
            : base(innerHandler)
        {
            _observer = observer;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _observer(request);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class TestServerUriCanonicalizingHandler : DelegatingHandler
    {
        public TestServerUriCanonicalizingHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.RequestUri = new Uri(request.RequestUri.OriginalString);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class CountingFailureHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            throw new HttpRequestException("Synthetic legacy origin failure.");
        }
    }
}
