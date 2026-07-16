// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Packaging;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.PackageCreation
{
    public class ApiPushTests : GalleryTestBase
    {
        private const int TaskCount = 16;
        private readonly ClientSdkHelper _clientSdkHelper;

        public ApiPushTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            _clientSdkHelper = new ClientSdkHelper(TestOutputHelper);
        }

        [Fact]
        [Description("Pushes many packages of the same ID and version. Verifies exactly one push succeeds and the rest fail with a conflict.")]
        [Priority(1)]
        [Category("AppServiceTests")]
        // This test fires 16 concurrent push requests per version (10 versions sequentially)
        // using an async gate to maximize race conditions. Versions are pushed sequentially
        // because .NET 10's SocketsHttpHandler allows unlimited concurrent connections per
        // server (unlike .NET Framework's ServicePointManager.DefaultConnectionLimit of 2),
        // and 160 simultaneous pushes can overwhelm the gallery.
        public async Task DuplicatePushesAreRejectedAndNotDeleted()
        {
            // Arrange
            var packageId = $"{nameof(DuplicatePushesAreRejectedAndNotDeleted)}.{Guid.NewGuid():N}";

            int pushVersionCount = 10;
            for (var duplicateTaskIndex = 0; duplicateTaskIndex < pushVersionCount; duplicateTaskIndex++)
            {
                await PushDuplicates(packageId, $"1.0.{duplicateTaskIndex}", duplicateTaskIndex == 0);
            }
        }

        private async Task PushDuplicates(string packageId, string packageVersion, bool isFirstTask)
        {
            using (var client = new HttpClient())
            {
                var packageCreationHelper = new PackageCreationHelper(TestOutputHelper);
                if (!isFirstTask)
                {
                    TestOutputHelper.WriteLine(string.Empty);
                    TestOutputHelper.WriteLine(new string('=', 80));
                    TestOutputHelper.WriteLine(string.Empty);
                }

                TestOutputHelper.WriteLine($"Starting package {packageId} {packageVersion}...");

                var packagePath = await packageCreationHelper.CreatePackage(packageId, packageVersion);
                var packageBytes = await File.ReadAllBytesAsync(packagePath);

                var tasks = new List<Task<HttpStatusCode>>();
                // Use an async gate so all tasks await a single signal, then fire SendAsync
                // simultaneously. This replaces the old Barrier+BarrierStream approach which
                // could deadlock in .NET 10 when SocketsHttpHandler aborts body uploads on
                // early server responses (e.g. HTTP/2 RST_STREAM on 409 Conflict).
                var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                // Act
                for (var taskIndex = 0; taskIndex < TaskCount; taskIndex++)
                {
                    tasks.Add(PushAsync(client, packageBytes, gate.Task));
                }

                // Release all tasks at once to maximize concurrency
                gate.SetResult(true);

                var statusCodes = await Task.WhenAll(tasks);

                // Assert push results first to fail fast with clear diagnostics instead
                // of waiting 30 minutes for the V2 verification timeout.
                for (var taskIndex = 1; taskIndex <= statusCodes.Length; taskIndex++)
                {
                    TestOutputHelper.WriteLine($"{packageId}/{packageVersion} Task {taskIndex:D2} push:     HTTP {(int)statusCodes[taskIndex - 1]}");
                }

                Assert.Single(statusCodes, x => x == HttpStatusCode.Created);
                Assert.Equal(TaskCount - 1, statusCodes.Count(x => x == HttpStatusCode.Conflict));

                //Wait for the packages to be available in V2(due to async validation)
                await _clientSdkHelper.VerifyPackageExistsInV2Async(packageId, packageVersion);

                var downloadUrl = $"{UrlHelper.V2FeedRootUrl}package/{packageId}/{packageVersion}";
                using (var response = await client.GetAsync(downloadUrl))
                {
                    TestOutputHelper.WriteLine($"Package {packageId}/{packageVersion}  download: HTTP {(int)response.StatusCode}");

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                    var actualPackageBytes = await response.Content.ReadAsByteArrayAsync();
                    using (var stream = new MemoryStream(actualPackageBytes))
                    using (var packageReader = new PackageArchiveReader(stream))
                    {
                        Assert.Equal(packageId, packageReader.NuspecReader.GetId());
                        Assert.Equal(packageVersion, packageReader.NuspecReader.GetVersion().ToNormalizedString());
                    }
                }
            }
        }

        private async Task<HttpStatusCode> PushAsync(
            HttpClient client,
            byte[] packageBytes,
            Task gate)
        {
            // Wait until all tasks are created, then fire simultaneously
            await gate;

            using (var request = new HttpRequestMessage(HttpMethod.Put, UrlHelper.V2FeedPushSourceUrl))
            {
                request.Content = new ByteArrayContent(packageBytes);
                request.Headers.Add(Constants.NuGetHeaderApiKey, GalleryConfiguration.Instance.Account.ApiKey);
                request.Headers.Add(Constants.NuGetHeaderProtocolVersion, Constants.NuGetProtocolVersion);

                using (var response = await client.SendAsync(request))
                {
                    if (response.StatusCode != HttpStatusCode.Created &&
                        response.StatusCode != HttpStatusCode.Conflict)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        TestOutputHelper.WriteLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}{Environment.NewLine}{content}");
                    }

                    return response.StatusCode;
                }
            }
        }
    }
}
