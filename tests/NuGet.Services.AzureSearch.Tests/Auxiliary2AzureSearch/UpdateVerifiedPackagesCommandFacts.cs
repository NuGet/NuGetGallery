// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Support;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Auxiliary2AzureSearch
{
    public class UpdateVerifiedPackagesCommandFacts
    {
        public class ExecuteAsync : Facts
        {
            public ExecuteAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task PushesAddedVerifiedPackage()
            {
                NewVerifiedPackagesData.Add("NuGet.Versioning");

                await Target.ExecuteAsync();

                VerifyCompletedTelemetry(JobOutcome.Success);
                VerifiedPackagesDataClient.Verify(
                    x => x.ReplaceLatestAsync(
                        NewVerifiedPackagesData,
                        It.Is<IAccessCondition>(a => a.IfMatchETag == OldVerifiedPackagesResult.Metadata.ETag)),
                    Times.Once);
            }

            [Fact]
            public async Task PushesRemovedVerifiedPackage()
            {
                OldVerifiedPackagesData.Add("NuGet.Versioning");

                await Target.ExecuteAsync();

                VerifyCompletedTelemetry(JobOutcome.Success);
                VerifiedPackagesDataClient.Verify(
                    x => x.ReplaceLatestAsync(
                        NewVerifiedPackagesData,
                        It.Is<IAccessCondition>(a => a.IfMatchETag == OldVerifiedPackagesResult.Metadata.ETag)),
                    Times.Once);
            }

            [Fact]
            public async Task DoesNotPushUnchangedVerifiedPackages()
            {
                await Target.ExecuteAsync();

                VerifyCompletedTelemetry(JobOutcome.NoOp);
                VerifiedPackagesDataClient.Verify(
                    x => x.ReplaceLatestAsync(It.IsAny<HashSet<string>>(), It.IsAny<IAccessCondition>()),
                    Times.Never);
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                DatabaseAuxiliaryDataFetcher = new Mock<IDatabaseAuxiliaryDataFetcher>();
                VerifiedPackagesDataClient = new Mock<IVerifiedPackagesDataClient>();
                TelemetryService = new Mock<IAzureSearchTelemetryService>();
                Logger = output.GetLogger<Auxiliary2AzureSearchCommand>();

                OldVerifiedPackagesData = new HashSet<string>();
                OldVerifiedPackagesResult = Data.GetAuxiliaryFileResult(OldVerifiedPackagesData, "verified-packages-etag");
                VerifiedPackagesDataClient
                    .Setup(x => x.ReadLatestAsync(It.IsAny<IAccessCondition>(), It.IsAny<StringCache>()))
                    .ReturnsAsync(() => OldVerifiedPackagesResult);
                NewVerifiedPackagesData = new HashSet<string>();
                DatabaseAuxiliaryDataFetcher.Setup(x => x.GetVerifiedPackagesAsync()).ReturnsAsync(() => NewVerifiedPackagesData);

                Target = new UpdateVerifiedPackagesCommand(
                    DatabaseAuxiliaryDataFetcher.Object,
                    VerifiedPackagesDataClient.Object,
                    TelemetryService.Object,
                    Logger);
            }

            public Mock<IDatabaseAuxiliaryDataFetcher> DatabaseAuxiliaryDataFetcher { get; }
            public Mock<IVerifiedPackagesDataClient> VerifiedPackagesDataClient { get; }
            public Mock<IAzureSearchTelemetryService> TelemetryService { get; }
            public RecordingLogger<Auxiliary2AzureSearchCommand> Logger { get; }
            public UpdateVerifiedPackagesCommand Target { get; }
            public HashSet<string> OldVerifiedPackagesData { get; }
            public AuxiliaryFileResult<HashSet<string>> OldVerifiedPackagesResult { get; }
            public HashSet<string> NewVerifiedPackagesData { get; }

            public void VerifyCompletedTelemetry(JobOutcome outcome)
            {
                TelemetryService.Verify(
                    x => x.TrackUpdateVerifiedPackagesCompleted(It.IsAny<JobOutcome>(), It.IsAny<TimeSpan>()),
                    Times.Once);
                TelemetryService.Verify(
                    x => x.TrackUpdateVerifiedPackagesCompleted(outcome, It.IsAny<TimeSpan>()),
                    Times.Once);
            }
        }
    }
}
