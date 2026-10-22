// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests.Staging
{
    /// <summary>
    /// Exercises real validation workers with development parent/scan validators and actual symbol matching.
    /// </summary>
    [Collection(GalleryTestCollection.Definition)]
    public class StagingWorkerTests
    {
        public StagingWorkerTests()
        {
            Assert.Equal("full", Environment.GetEnvironmentVariable("APPHOST_PROFILE"));
        }

        [Fact]
        [Category("StagingFullTests")]
        public async Task ParentBecomesReadyThroughTheWorkerWithoutBecomingPublic()
        {
            await using var context = new StagingTestContext();
            var id = StagingTestContext.NewPackageId();
            var bytes = StagingTestContext.CreateArchive(id);
            using (var upload = await context.UploadAsync(id, bytes))
            {
                var accepted = await StagingTestContext.ReadJsonAsync(upload, HttpStatusCode.Created);
                Assert.Equal("validating", accepted["status"].GetValue<string>());
                Assert.False(accepted["canPromote"].GetValue<bool>());
            }

            var ready = await context.WaitForArtifactAsync(id);
            StagingTestContext.AssertArtifact(ready, id);
            Assert.True(ready["canPromote"].GetValue<bool>());
            using var download = await context.SendAsync(HttpMethod.Get, StagingTestContext.ArtifactPath(id));
            Assert.Equal(bytes, await StagingTestContext.ReadBytesAsync(download));
            using var publicPackage = await context.SendAsync(HttpMethod.Get, "../../../packages/" + id + "/1.0.0");
            Assert.Equal(HttpStatusCode.NotFound, publicPackage.StatusCode);
        }

        [Fact]
        [Category("StagingFullTests")]
        public async Task DevelopmentParentFailureProducesBlockersAndKeepsUploadedContentPrivate()
        {
            await using var context = new StagingTestContext();
            var id = StagingTestContext.NewPackageId("ValidationFail.StagingFunctional");
            var bytes = StagingTestContext.CreateArchive(id);
            using (var upload = await context.UploadAsync(id, bytes))
            {
                var accepted = await StagingTestContext.ReadJsonAsync(upload, HttpStatusCode.Created);
                Assert.Equal("validating", accepted["status"].GetValue<string>());
            }

            var failed = await context.WaitForArtifactAsync(id, status: "validationFailed");
            Assert.False(failed["canPromote"].GetValue<bool>());
            Assert.Contains(failed["blockers"].AsArray(), blocker => blocker["code"].GetValue<string>().StartsWith("Validation", StringComparison.Ordinal));
            using var download = await context.SendAsync(HttpMethod.Get, StagingTestContext.ArtifactPath(id));
            Assert.Equal(bytes, await StagingTestContext.ReadBytesAsync(download));
            using var publicPackage = await context.SendAsync(HttpMethod.Get, "../../../packages/" + id + "/1.0.0");
            Assert.Equal(HttpStatusCode.NotFound, publicPackage.StatusCode);
        }

        [Fact]
        [Category("StagingFullTests")]
        public async Task MismatchedSymbolsFailRealValidationAndCorrectedReplacementMakesTheGroupReady()
        {
            await using var context = new StagingTestContext();
            var group = await context.CreateGroupAsync();
            var id = StagingTestContext.NewPackageId();
            using (var parent = await context.UploadAsync(id, StagingTestContext.CreateArchive(id), groupId: group))
            {
                await StagingTestContext.ReadJsonAsync(parent, HttpStatusCode.Created);
            }

            await context.WaitForArtifactAsync(id);
            var invalid = StagingTestContext.CreateArchive(id, symbols: true, assemblyName: "MissingAssembly");
            using (var symbols = await context.UploadAsync(id, invalid, symbols: true))
            {
                var accepted = await StagingTestContext.ReadJsonAsync(symbols, HttpStatusCode.Created);
                Assert.Equal("validating", accepted["status"].GetValue<string>());
            }

            var failed = await context.WaitForArtifactAsync(id, symbols: true, status: "validationFailed");
            Assert.False(failed["canPromote"].GetValue<bool>());
            Assert.Contains(failed["blockers"].AsArray(), blocker => blocker["code"].GetValue<string>() == "ValidationSymbolErrorCode_MatchingAssemblyNotFound");
            Assert.False((await context.GetJsonAsync("groups/" + group))["group"]["canPromote"].GetValue<bool>());
            using (var failedDownload = await context.SendAsync(HttpMethod.Get, StagingTestContext.ArtifactPath(id, true)))
            {
                Assert.Equal(invalid, await StagingTestContext.ReadBytesAsync(failedDownload));
            }

            var corrected = StagingTestContext.CreateArchive(id, symbols: true);
            using (var replacement = await context.UploadAsync(id, corrected, symbols: true))
            {
                var accepted = await StagingTestContext.ReadJsonAsync(replacement, HttpStatusCode.OK);
                Assert.Equal("validating", accepted["status"].GetValue<string>());
            }

            StagingTestContext.AssertArtifact(await context.WaitForArtifactAsync(id, symbols: true), id, symbols: true);
            Assert.True((await context.GetJsonAsync("groups/" + group))["group"]["canPromote"].GetValue<bool>());
            using var download = await context.SendAsync(HttpMethod.Get, StagingTestContext.ArtifactPath(id, true));
            Assert.Equal(corrected, await StagingTestContext.ReadBytesAsync(download));
        }

        [Fact]
        [Category("StagingFullTests")]
        public async Task RestagedParentResumesValidationOfRetainedSymbols()
        {
            await using var context = new StagingTestContext();
            var id = StagingTestContext.NewPackageId();
            var parentBytes = StagingTestContext.CreateArchive(id);
            var symbolBytes = StagingTestContext.CreateArchive(id, symbols: true);
            using (var parent = await context.UploadAsync(id, parentBytes))
            using (var symbols = await context.UploadAsync(id, symbolBytes, symbols: true))
            {
                await StagingTestContext.ReadJsonAsync(parent, HttpStatusCode.Created);
                await StagingTestContext.ReadJsonAsync(symbols, HttpStatusCode.Created);
            }

            await context.WaitForArtifactAsync(id);
            await context.WaitForArtifactAsync(id, symbols: true);
            using (var deleted = await context.SendAsync(HttpMethod.Delete, StagingTestContext.ArtifactPath(id)))
            {
                Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            }

            var waiting = await context.GetJsonAsync(StagingTestContext.ArtifactPath(id, true) + "/status");
            Assert.Equal("waitingForParent", waiting["status"].GetValue<string>());
            using (var restaged = await context.UploadAsync(id, parentBytes))
            {
                var accepted = await StagingTestContext.ReadJsonAsync(restaged, HttpStatusCode.OK);
                Assert.Equal("validating", accepted["status"].GetValue<string>());
            }

            await context.WaitForArtifactAsync(id);
            StagingTestContext.AssertArtifact(await context.WaitForArtifactAsync(id, symbols: true), id, symbols: true);
            using var download = await context.SendAsync(HttpMethod.Get, StagingTestContext.ArtifactPath(id, true));
            Assert.Equal(symbolBytes, await StagingTestContext.ReadBytesAsync(download));
        }
    }
}
