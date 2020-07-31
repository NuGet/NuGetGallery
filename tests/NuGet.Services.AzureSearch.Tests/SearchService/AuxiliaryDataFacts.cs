// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Support;
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
                _target.Downloads.Data.SetDownloadCount("NuGet.Versioning", "1.0.0", 2);
                _target.Downloads.Data.SetDownloadCount("NuGet.Versioning", "3.0.0-alpha", 23);

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
                _target.Downloads.Data.SetDownloadCount("NuGet.Versioning", "1.0.0", 2);

                var actual = _target.GetDownloadCount("nuget.versioning", "2.0.0");

                Assert.Equal(0, actual);
            }

            [Fact]
            public void ReturnsCount()
            {
                _target.Downloads.Data.SetDownloadCount("NuGet.Versioning", "1.0.0", 2);
                _target.Downloads.Data.SetDownloadCount("NuGet.Versioning", "3.0.0-alpha", 23);

                var actual = _target.GetDownloadCount("nuget.versioning", "3.0.0-ALPHA");

                Assert.Equal(23, actual);
            }
        }

        public class Metadata
        {
            [Fact]
            public void UsesSameMetadataInstances()
            {
                var downloadData = Data.GetAuxiliaryFileResult(new DownloadData(), string.Empty);
                var verifiedPackages = Data.GetAuxiliaryFileResult(new HashSet<string>(StringComparer.OrdinalIgnoreCase), string.Empty);
                var popularityTransfers = Data.GetAuxiliaryFileResult(new PopularityTransferData(), string.Empty);

                var target = new AuxiliaryData(
                    DateTimeOffset.MaxValue,
                    downloadData,
                    verifiedPackages,
                    popularityTransfers);

                Assert.Equal(DateTimeOffset.MaxValue, target.Metadata.Loaded);
                Assert.Same(downloadData.Metadata, target.Metadata.Downloads);
                Assert.Same(verifiedPackages.Metadata, target.Metadata.VerifiedPackages);
                Assert.Same(popularityTransfers.Metadata, target.Metadata.PopularityTransfers);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly AuxiliaryData _target;

            public BaseFacts()
            {
                _target = new AuxiliaryData(
                    DateTimeOffset.MinValue,
                    Data.GetAuxiliaryFileResult(new DownloadData(), string.Empty),
                    Data.GetAuxiliaryFileResult(new HashSet<string>(StringComparer.OrdinalIgnoreCase), string.Empty),
                    Data.GetAuxiliaryFileResult(new PopularityTransferData(), string.Empty));
            }
        }
    }
}
