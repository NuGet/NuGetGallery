﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using Xunit;

namespace NuGetGallery.Infrastructure.Search
{
    public class ExternalSearchServiceFacts
    {
        public class TheReadPackageMethod
        {
            [Fact]
            public void WhenSearchFilterIsNotSemVer2_DoesNotSetIsLatestSemVer2Properties()
            {
                var jObject = JObject.Parse(isLatestSearchResult);

                // Act
                var result = ExternalSearchService.ReadPackage(jObject, "1.0.0");

                // Assert
                Assert.False(result.IsLatestSemVer2);
                Assert.False(result.IsLatestStableSemVer2);
            }

            [Fact]
            public void WhenSearchFilterIsSemVer2_SetsIsLatestSemVer2Properties()
            {
                var jObject = JObject.Parse(isLatestSearchResult);

                // Act
                var result = ExternalSearchService.ReadPackage(jObject, SemVerLevelKey.SemVerLevel2);

                // Assert
                Assert.True(result.IsLatestSemVer2);
                Assert.True(result.IsLatestStableSemVer2);
            }

            public static string isLatestSearchResult = @"{
  ""PackageRegistration"": {
    ""Id"": ""MyPackage"",
    ""DownloadCount"": 1,
    ""Verified"": false,
    ""Owners"": [ ""owner"" ]
  },
  ""Version"": ""1.2.34+git.abc123"",
  ""NormalizedVersion"": ""1.2.34"",
  ""Title"": ""MyPackage"",
  ""Description"": """",
  ""Summary"": """",
  ""Authors"": ""author"",
  ""Copyright"": ""Copyright 2018"",
  ""Tags"": ""sometag"",
  ""ProjectUrl"": """",
  ""IconUrl"": """",
  ""IsLatestStable"": true,
  ""IsLatest"": true,
  ""Listed"": true,
  ""Created"": ""2018-01-01T01:00:00.000-00:00"",
  ""Published"": ""2018-01-01T01:00:00.000-00:00"",
  ""LastUpdated"": ""2018-01-01T01:00:00.000-00:00"",
  ""LastEdited"": ""2018-01-01T01:00:00.000-00:00"",
  ""DownloadCount"": 1,
  ""Dependencies"": [],
  ""SupportedFrameworks"": [
    ""net46""
  ],
  ""Hash"": ""hash"",
  ""HashAlgorithm"": ""SHA512"",
  ""PackageFileSize"": 100000,
  ""LicenseUrl"": """",
  ""RequiresLicenseAcceptance"": false
}";
        }
    }
}
