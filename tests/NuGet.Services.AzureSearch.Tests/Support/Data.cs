// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Support
{
    public class Data : V3Data
    {
        public static readonly DateTimeOffset DocumentLastUpdated = new DateTimeOffset(2018, 12, 14, 9, 30, 0, TimeSpan.Zero);

        public static void SetDocumentLastUpdated(IUpdatedDocument document, ITestOutputHelper output)
        {
            var currentTimestamp = document.LastUpdatedDocument;
            output.WriteLine(
                $"The commited document has a generated {nameof(document.LastUpdatedDocument)} value of " +
                $"{currentTimestamp:O}. Replacing this value with {DocumentLastUpdated:O}.");
            document.LastUpdatedDocument = DocumentLastUpdated;
        }

        public static string[] Versions => new[]
        {
            "1.0.0",
            "2.0.0+git",
            "3.0.0-alpha.1",
            FullVersion
        };

        public static string[] Owners => new[]
        {
            "Microsoft",
            "azure-sdk",
        };

        public const int TotalDownloadCount = 1001;

        public static readonly SearchFilters SearchFilters = SearchFilters.IncludePrereleaseAndSemVer2;

        public static readonly AzureSearchScoringConfiguration Config = new AzureSearchScoringConfiguration
        {
            DownloadScoreBoost = 2
        };

        private static IOptionsSnapshot<AzureSearchJobConfiguration> Options
        {
            get
            {
                var mock = new Mock<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                var config = new AzureSearchJobConfiguration
                {
                    GalleryBaseUrl = GalleryBaseUrl,
                    FlatContainerBaseUrl = FlatContainerBaseUrl,
                    FlatContainerContainerName = FlatContainerContainerName,
                    Scoring = new AzureSearchScoringConfiguration(),
                };

                mock.Setup(o => o.Value).Returns(config);

                return mock.Object;
            }
        }

        private static BaseDocumentBuilder BaseDocumentBuilder => new BaseDocumentBuilder(Options);

        public static SearchDocument.Full SearchDocument => new SearchDocumentBuilder(BaseDocumentBuilder).FullFromDb(
            PackageId,
            SearchFilters.IncludePrereleaseAndSemVer2,
            Versions,
            isLatestStable: false,
            isLatest: true,
            fullVersion: FullVersion,
            package: PackageEntity,
            owners: Owners,
            totalDownloadCount: TotalDownloadCount,
            isExcludedByDefault: false);

        public static HijackDocumentChanges HijackDocumentChanges => new HijackDocumentChanges(
            delete: false,
            updateMetadata: true,
            latestStableSemVer1: false,
            latestSemVer1: true,
            latestStableSemVer2: false,
            latestSemVer2: true);

        public static HijackDocument.Full HijackDocument => new HijackDocumentBuilder(BaseDocumentBuilder).FullFromDb(
            PackageId,
            HijackDocumentChanges,
            PackageEntity);

        public static AuxiliaryFileMetadata GetAuxiliaryFileMetadata(string etag) => new AuxiliaryFileMetadata(
            DateTimeOffset.MinValue,
            TimeSpan.Zero,
            fileSize: 0,
            etag: etag);

        public static AuxiliaryFileResult<T> GetAuxiliaryFileResult<T>(T data, string etag) where T : class
        {
            return new AuxiliaryFileResult<T>(
                modified: true,
                data: data,
                metadata: GetAuxiliaryFileMetadata(etag));
        }
    }
}
