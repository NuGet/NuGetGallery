// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Helpers;
using NuGetGallery.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
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
            public void WhenValidDeprecation_SetsPropertiesToPackage(Deprecation deprecation)
            {
                var doc = TheReadPackageMethod.CreateDocument();
                var docDeprecation = new JObject();
                docDeprecation.Add("Reasons", JArray.FromObject(deprecation.Reasons));

                if (!string.IsNullOrEmpty(deprecation.Message))
                {
                    docDeprecation.Add("Message", deprecation.Message);
                }
                if (deprecation.AlternatePackage != null)
                {
                    docDeprecation.Add("AlternatePackage", JObject.FromObject(deprecation.AlternatePackage));
                }

                doc.Add("Deprecation", docDeprecation);

                // Act
                var result = ExternalSearchService.ReadPackage(doc, SemVerLevelKey.SemVerLevel2);

                // Assert
                var deprecationResult = result.Deprecation;
                Assert.Equal(deprecation.Message, deprecationResult.Message);

                if (deprecation.AlternatePackage != null)
                {
                    Assert.Equal(deprecation.AlternatePackage.Id, deprecationResult.AlternatePackage.Id);
                    Assert.Equal(deprecation.AlternatePackage.Range, deprecationResult.AlternatePackage.Range);
                }

                Assert.Equal(deprecation.Reasons.Length, deprecationResult.Reasons.Length);
                for (var index = 0; index < deprecation.Reasons.Length; index++)
                {
                    Assert.Equal(deprecation.Reasons[index], deprecationResult.Reasons[index]);
                }
            }

            [Theory]
            [MemberData(nameof(DeprecationItemsHelper.InvalidObjects), MemberType = typeof(DeprecationItemsHelper))]
            public void WhenInvalidDeprecation_SetsNullToPackage(Deprecation deprecation)
            {
                var doc = TheReadPackageMethod.CreateDocument();
                if (deprecation != null)
                {
                    doc.Add("Deprecation", JObject.FromObject(deprecation));
                }

                // Act
                var result = ExternalSearchService.ReadPackage(doc, SemVerLevelKey.SemVerLevel2);

                // Assert
                Assert.Null(result.Deprecation);
            }

            [Theory]
            [MemberData(nameof(VulnerabilityItemsHelper.ValidObjects), MemberType = typeof(VulnerabilityItemsHelper))]
            public void WhenValidVulnerabilities_SetsPropertiesToPackage(IReadOnlyList<Vulnerability> vulnerabilities)
            {
                var doc = TheReadPackageMethod.CreateDocument();
                doc.Add("Vulnerabilities", JArray.FromObject(vulnerabilities));

                // Act
                var result = ExternalSearchService.ReadPackage(doc, SemVerLevelKey.SemVerLevel2);

                // Assert
                var vulnerabilitiesResult = result.Vulnerabilities;
                Assert.NotNull(vulnerabilitiesResult);
                Assert.NotEmpty(vulnerabilitiesResult);
                Assert.Equal(vulnerabilities.Count, vulnerabilitiesResult.Count);

                for (var index = 0; index < vulnerabilities.Count; index++)
                {
                    Assert.Equal(vulnerabilities[index].AdvisoryURL, vulnerabilitiesResult[index].AdvisoryURL);
                    Assert.Equal(vulnerabilities[index].Severity, vulnerabilitiesResult[index].Severity);
                }
            }

            [Theory]
            [MemberData(nameof(VulnerabilityItemsHelper.InvalidObjects), MemberType = typeof(VulnerabilityItemsHelper))]
            public void WhenInvalidVulnerabilities_SetsEmptyArrayToPackage(IReadOnlyList<Vulnerability> vulnerabilities)
            {
                var doc = TheReadPackageMethod.CreateDocument();
                if (vulnerabilities != null)
                {
                    doc.Add("Vulnerabilities", JArray.FromObject(vulnerabilities));
                }

                // Act
                var result = ExternalSearchService.ReadPackage(doc, SemVerLevelKey.SemVerLevel2);

                // Assert
                Assert.Empty(result.Vulnerabilities);
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
