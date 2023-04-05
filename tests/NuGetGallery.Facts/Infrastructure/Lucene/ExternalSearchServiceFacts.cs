// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Newtonsoft.Json.Linq;
using NuGetGallery.Helpers;
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


            [Theory]
            [MemberData(nameof(DeprecationItemsHelper.ValidObjects), MemberType = typeof(DeprecationItemsHelper))]
            public void WhenValidDeprecation_SetsPropertiesToPackage(JObject docDeprecation)
            {
                var doc = TheReadPackageMethod.CreateDocument();
                doc.Add("Deprecation", docDeprecation);

                // Act
                var result = ExternalSearchService.ReadPackage(doc, SemVerLevelKey.SemVerLevel2);

                // Assert
                var deprecationResult = result.Deprecations.First();
                var deprecation = SearchResponseHelper.GetDeprecationsOrNull(docDeprecation).First();

                Assert.Equal(deprecation.CustomMessage, deprecationResult.CustomMessage);

                if (deprecation.AlternatePackage != null)
                {
                    Assert.Equal(deprecation.AlternatePackage.Id, deprecationResult.AlternatePackage.Id);
                    Assert.Equal(deprecation.AlternatePackage.Version, deprecationResult.AlternatePackage.Version);
                }

                Assert.Equal(deprecation.Status, deprecationResult.Status);
            }

            [Theory]
            [MemberData(nameof(DeprecationItemsHelper.InvalidObjects), MemberType = typeof(DeprecationItemsHelper))]
            public void WhenInvalidDeprecation_SetsNullToPackage(JObject deprecation)
            {
                var doc = TheReadPackageMethod.CreateDocument();
                if (deprecation != null)
                {
                    doc.Add("Deprecation", deprecation);
                }

                // Act
                var result = ExternalSearchService.ReadPackage(doc, SemVerLevelKey.SemVerLevel2);

                // Assert
                Assert.Null(result.Deprecations);
            }

            [Theory]
            [MemberData(nameof(VulnerabilityItemsHelper.ValidObjects), MemberType = typeof(VulnerabilityItemsHelper))]
            public void WhenValidVulnerabilities_SetsPropertiesToPackage(JArray docVulnerabilities)
            {
                var doc = TheReadPackageMethod.CreateDocument();
                doc.Add("Vulnerabilities", docVulnerabilities);

                // Act
                var result = ExternalSearchService.ReadPackage(doc, SemVerLevelKey.SemVerLevel2);

                // Assert
                var vulnerabilities = SearchResponseHelper.GetVulnerabilities(docVulnerabilities);
                var vulnerabilitiesResult = result.VulnerablePackageRanges;

                Assert.NotNull(vulnerabilitiesResult);
                Assert.NotEmpty(vulnerabilitiesResult);
                Assert.Equal(vulnerabilities.Count, vulnerabilitiesResult.Count);

                for (var index = 0; index < vulnerabilities.Count; index++)
                {
                    Assert.Equal(vulnerabilities.ElementAt(index).Vulnerability.AdvisoryUrl, vulnerabilitiesResult.ElementAt(index).Vulnerability.AdvisoryUrl);
                    Assert.Equal(vulnerabilities.ElementAt(index).Vulnerability.Severity, vulnerabilitiesResult.ElementAt(index).Vulnerability.Severity);
                }
            }

            [Theory]
            [MemberData(nameof(VulnerabilityItemsHelper.InvalidObjects), MemberType = typeof(VulnerabilityItemsHelper))]
            public void WhenInvalidVulnerabilities_SetsEmptyArrayToPackage(JArray docVulnerabilities)
            {
                var doc = TheReadPackageMethod.CreateDocument();
                if (docVulnerabilities != null)
                {
                    doc.Add("Vulnerabilities", docVulnerabilities);
                }

                // Act
                var result = ExternalSearchService.ReadPackage(doc, SemVerLevelKey.SemVerLevel2);

                // Assert
                Assert.Empty(result.VulnerablePackageRanges);
            }

            public static JObject CreateDocument()
            {
                var doc = new JObject();

                doc.Add("PackageRegistration", JObject.FromObject(
                    new
                    {
                        Id = "myId",
                        Owners = new string[] { "nuget" },
                        DownloadCount = 1,
                        IsVerified = true,
                        Key = 2
                    }));
                doc.Add("Dependencies", JToken.Parse("[]"));
                doc.Add("SupportedFrameworks", JToken.Parse("[]"));

                return doc;
            }
        }
    }
}
