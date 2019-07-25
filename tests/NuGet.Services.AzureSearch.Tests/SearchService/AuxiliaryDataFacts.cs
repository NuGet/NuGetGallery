// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Indexing;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using Xunit;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class AuxiliaryDataFacts
    {
        public class IsVerified : BaseFacts
        {
            [Fact]
            public void VerifiedWhenInSet()
            {
                _target.VerifiedPackages.Data.Add("NuGet.Versioning");

                var actual = _target.IsVerified("nuget.versioning");

                Assert.True(actual);
            }

            [Fact]
            public void NotVerifiedWhenNotInSet()
            {
                var actual = _target.IsVerified("nuget.versioning");

                Assert.False(actual);
            }
        }

        public class GetTotalDownloadCount : BaseFacts
        {
            [Fact]
            public void ZeroWhenUnknownId()
            {
                var actual = _target.GetTotalDownloadCount("nuget.versioning");

                Assert.Equal(0, actual);
            }

            [Fact]
            public void ReturnsTotal()
            {
                var downloads = new DownloadsByVersion();
                downloads["1.0.0"] = 2;
                downloads["3.0.0-alpha"] = 23;
                _target.Downloads.Data["NuGet.Versioning"] = downloads;

                var actual = _target.GetTotalDownloadCount("nuget.versioning");

                Assert.Equal(25, actual);
            }
        }

        public class GetDownloadCount : BaseFacts
        {
            [Fact]
            public void ZeroWhenUnknownId()
            {
                var actual = _target.GetDownloadCount("nuget.versioning", "1.0.0");

                Assert.Equal(0, actual);
            }

            [Fact]
            public void ZeroWhenUnknownVersion()
            {
                var downloads = new DownloadsByVersion();
                downloads["1.0.0"] = 2;
                _target.Downloads.Data["NuGet.Versioning"] = downloads;

                var actual = _target.GetDownloadCount("nuget.versioning", "2.0.0");

                Assert.Equal(0, actual);
            }

            [Fact]
            public void ReturnsCount()
            {
                var downloads = new DownloadsByVersion();
                downloads["1.0.0"] = 2;
                downloads["3.0.0-alpha"] = 23;
                _target.Downloads.Data["NuGet.Versioning"] = downloads;

                var actual = _target.GetDownloadCount("nuget.versioning", "3.0.0-ALPHA");

                Assert.Equal(23, actual);
            }
        }

        public class Metadata
        {
            [Fact]
            public void UsesSameMetadataInstances()
            {
                var downloadsMetadata = new AuxiliaryFileMetadata(
                    DateTimeOffset.MinValue,
                    DateTimeOffset.MinValue,
                    TimeSpan.Zero,
                    fileSize: 0,
                    etag: string.Empty);
                var verifiedPackagesMetadata = new AuxiliaryFileMetadata(
                    DateTimeOffset.MinValue,
                    DateTimeOffset.MinValue,
                    TimeSpan.Zero,
                    fileSize: 0,
                    etag: string.Empty);

                var target = new AuxiliaryData(
                    new AuxiliaryFileResult<Downloads>(
                        notModified: false,
                        data: new Downloads(),
                        metadata: downloadsMetadata),
                    new AuxiliaryFileResult<HashSet<string>>(
                        notModified: false,
                        data: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        metadata: verifiedPackagesMetadata));

                Assert.Same(downloadsMetadata, target.Metadata.Downloads);
                Assert.Same(verifiedPackagesMetadata, target.Metadata.VerifiedPackages);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly AuxiliaryData _target;

            public BaseFacts()
            {
                _target = new AuxiliaryData(
                    new AuxiliaryFileResult<Downloads>(
                        notModified: false,
                        data: new Downloads(),
                        metadata: new AuxiliaryFileMetadata(
                            DateTimeOffset.MinValue,
                            DateTimeOffset.MinValue,
                            TimeSpan.Zero,
                            fileSize: 0,
                            etag: string.Empty)),
                    new AuxiliaryFileResult<HashSet<string>>(
                        notModified: false,
                        data: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        metadata: new AuxiliaryFileMetadata(
                            DateTimeOffset.MinValue,
                            DateTimeOffset.MinValue,
                            TimeSpan.Zero,
                            fileSize: 0,
                            etag: string.Empty)));
            }
        }
    }
}
