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
        // This test fires 160 concurrent push requests (10 versions × 16 parallel pushes each)
        // using an async gate to maximize race conditions. It can technically run locally but is
        // flaky on resource-constrained environments due to the extreme load on IIS Express.
        public async Task DuplicatePushesAreRejectedAndNotDeleted()
        {
            // Arrange
            var packageId = $"{nameof(DuplicatePushesAreRejectedAndNotDeleted)}.{Guid.NewGuid():N}";

            int pushVersionCount = 10;
            var duplicatePushTasks = new List<Task>();
            for (var duplicateTaskIndex = 0; duplicateTaskIndex < pushVersionCount; duplicateTaskIndex++)
            {
                duplicatePushTasks.Add(PushDuplicates(packageId, $"1.0.{duplicateTaskIndex}", duplicateTaskIndex == 0));
            }

            await Task.WhenAll(duplicatePushTasks);
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

                // Assert
                for (var taskIndex = 1; taskIndex <= statusCodes.Length; taskIndex++)
                {
                    TestOutputHelper.WriteLine($"{packageId}/{packageVersion} Task {taskIndex:D2} push:     HTTP {(int)statusCodes[taskIndex - 1]}");
                }

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

                Assert.Equal(1, statusCodes.Count(x => x == HttpStatusCode.Created));
                Assert.Equal(TaskCount - 1, statusCodes.Count(x => x == HttpStatusCode.Conflict));
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
