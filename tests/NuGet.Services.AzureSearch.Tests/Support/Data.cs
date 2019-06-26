// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Protocol.Catalog;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery;
using Xunit.Abstractions;
using PackageDependency = NuGet.Protocol.Catalog.PackageDependency;

namespace NuGet.Services.AzureSearch.Support
{
    public static class Data
    {
        public const string GalleryBaseUrl = "https://example/";
        public const string FlatContainerBaseUrl = "https://example/flat-container/";
        public const string FlatContainerContainerName = "v3-flatcontainer";
        public const string PackageId = "WindowsAzure.Storage";
        public const string FullVersion = "7.1.2-alpha+git";
        public static readonly string NormalizedVersion = NuGetVersion.Parse(FullVersion).ToNormalizedString();
        public static readonly string LowerPackageId = PackageId.ToLowerInvariant();
        public static readonly string LowerNormalizedVersion = NormalizedVersion.ToLowerInvariant();
        public static readonly string GalleryLicenseUrl = $"{GalleryBaseUrl}packages/{PackageId}/{NormalizedVersion}/license";
        public static readonly string FlatContainerIconUrl = $"{FlatContainerBaseUrl}{FlatContainerContainerName}/{LowerPackageId}/{LowerNormalizedVersion}/icon";
        public static readonly DateTimeOffset DocumentLastUpdated = new DateTimeOffset(2018, 12, 14, 9, 30, 0, TimeSpan.Zero);
        public static readonly DateTimeOffset CommitTimestamp = new DateTimeOffset(2018, 12, 13, 12, 30, 0, TimeSpan.Zero);
        public static readonly string CommitId = "6b9b24dd-7aec-48ae-afc1-2a117e3d50d1";

        public static void SetDocumentLastUpdated(ICommittedDocument document, ITestOutputHelper output)
        {
            var currentTimestamp = document.LastUpdatedDocument;
            output.WriteLine(
                $"The commited document has a generated {nameof(document.LastUpdatedDocument)} value of " +
                $"{currentTimestamp:O}. Replacing this value with {DocumentLastUpdated:O}.");
            document.LastUpdatedDocument = DocumentLastUpdated;
        }

        public static Package PackageEntity => new Package
        {
            FlattenedAuthors = "Microsoft",
            Copyright = "© Microsoft Corporation. All rights reserved.",
            Created = new DateTime(2017, 1, 1),
            Description = "Description.",
            FlattenedDependencies = "Microsoft.Data.OData:5.6.4:net40-client|Newtonsoft.Json:6.0.8:net40-client",
            Hash = "oMs9XKzRTsbnIpITcqZ5XAv1h2z6oyJ33+Z/PJx36iVikge/8wm5AORqAv7soKND3v5/0QWW9PQ0ktQuQu9aQQ==",
            HashAlgorithm = "SHA512",
            IconUrl = "http://go.microsoft.com/fwlink/?LinkID=288890",
            IsPrerelease = true,
            Language = "en-US",
            LastEdited = new DateTime(2017, 1, 2),
            LicenseUrl = "http://go.microsoft.com/fwlink/?LinkId=331471",
            Listed = true,
            MinClientVersion = "2.12",
            NormalizedVersion = "7.1.2-alpha",
            PackageFileSize = 3039254,
            ProjectUrl = "https://github.com/Azure/azure-storage-net",
            Published = new DateTime(2017, 1, 3),
            ReleaseNotes = "Release notes.",
            RequiresLicenseAcceptance = true,
            SemVerLevelKey = SemVerLevelKey.SemVer2,
            Summary = "Summary.",
            Tags = "Microsoft Azure Storage Table Blob File Queue Scalable windowsazureofficial",
            Title = "Windows Azure Storage",
            Version = "7.1.2.0-alpha+git",
        };

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

        public static SearchDocument.Full SearchDocument => new SearchDocumentBuilder(BaseDocumentBuilder, Options).FullFromDb(
            PackageId,
            SearchFilters.IncludePrereleaseAndSemVer2,
            Versions,
            isLatestStable: false,
            isLatest: true,
            fullVersion: FullVersion,
            package: PackageEntity,
            owners: Owners,
            totalDownloadCount: TotalDownloadCount);

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

        public static PackageDetailsCatalogLeaf Leaf => new PackageDetailsCatalogLeaf
        {
            Authors = "Microsoft",
            CommitId = CommitId,
            CommitTimestamp = CommitTimestamp,
            Copyright = "© Microsoft Corporation. All rights reserved.",
            Created = new DateTimeOffset(new DateTime(2017, 1, 1), TimeSpan.Zero),
            Description = "Description.",
            DependencyGroups = new List<PackageDependencyGroup>
            {
                new PackageDependencyGroup
                {
                    TargetFramework = ".NETFramework4.0-Client",
                    Dependencies = new List<PackageDependency>
                    {
                        new PackageDependency
                        {
                            Id = "Microsoft.Data.OData",
                            Range = "[5.6.4, )",
                        },
                        new PackageDependency
                        {
                            Id = "Newtonsoft.Json",
                            Range = "[6.0.8, )",
                        },
                    },
                },
            },
            IconUrl = "http://go.microsoft.com/fwlink/?LinkID=288890",
            IsPrerelease = true,
            Language = "en-US",
            LastEdited = new DateTimeOffset(new DateTime(2017, 1, 2), TimeSpan.Zero),
            LicenseUrl = "http://go.microsoft.com/fwlink/?LinkId=331471",
            Listed = true,
            MinClientVersion = "2.12",
            PackageHash = "oMs9XKzRTsbnIpITcqZ5XAv1h2z6oyJ33+Z/PJx36iVikge/8wm5AORqAv7soKND3v5/0QWW9PQ0ktQuQu9aQQ==",
            PackageHashAlgorithm = "SHA512",
            PackageId = PackageId,
            PackageSize = 3039254,
            PackageVersion = FullVersion,
            ProjectUrl = "https://github.com/Azure/azure-storage-net",
            Published = new DateTimeOffset(new DateTime(2017, 1, 3), TimeSpan.Zero),
            ReleaseNotes = "Release notes.",
            RequireLicenseAgreement = true,
            Summary = "Summary.",
            Tags = new List<string> { "Microsoft", "Azure", "Storage", "Table", "Blob", "File", "Queue", "Scalable", "windowsazureofficial" },
            Title = "Windows Azure Storage",
            VerbatimVersion = "7.1.2.0-alpha+git",
        };
    }
}
