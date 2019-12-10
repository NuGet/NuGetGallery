// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using NuGet.Services;
using NuGet.Versioning;
using System.Collections.Generic;
using Xunit;

namespace NuGet.Jobs.Catalog2Registration
{
    public class EntityBuilderFacts
    {
        public class UpdateLeafItem : Facts
        {
            [Fact]
            public void EncodesUnsafeCharactersInPackageContentUrl()
            {
                Target.UpdateLeafItem(
                    LeafItem,
                    HiveType.Legacy,
                    "测试更新包",
                    PackageDetails);

                Assert.Equal(
                    "https://example/fc/%E6%B5%8B%E8%AF%95%E6%9B%B4%E6%96%B0%E5%8C%85/7.1.2-alpha/%E6%B5%8B%E8%AF%95%E6%9B%B4%E6%96%B0%E5%8C%85.7.1.2-alpha.nupkg",
                    LeafItem.PackageContent);
            }

            [Fact]
            public void UsesEmptyStringForNoTags()
            {
                var leaf = V3Data.Leaf;
                leaf.Tags = null;

                Target.UpdateLeafItem(LeafItem, Hive, Id, leaf);

                Assert.Equal(new[] { string.Empty }, LeafItem.CatalogEntry.Tags.ToArray());
            }

            [Fact]
            public void UsesGalleryForLicenseUrlWhenPackageHasLicenseExpression()
            {
                var leaf = V3Data.Leaf;
                leaf.LicenseExpression = "MIT";

                Target.UpdateLeafItem(LeafItem, Hive, Id, leaf);

                Assert.Equal("MIT", LeafItem.CatalogEntry.LicenseExpression);
                Assert.Equal("https://example-gallery/packages/WindowsAzure.Storage/7.1.2-alpha/license", LeafItem.CatalogEntry.LicenseUrl);
            }

            [Fact]
            public void UsesGalleryForLicenseUrlWhenPackageHasLicenseFile()
            {
                var leaf = V3Data.Leaf;
                leaf.LicenseFile = "license.txt";

                Target.UpdateLeafItem(LeafItem, Hive, Id, leaf);

                Assert.Equal(string.Empty, LeafItem.CatalogEntry.LicenseExpression);
                Assert.Equal("https://example-gallery/packages/WindowsAzure.Storage/7.1.2-alpha/license", LeafItem.CatalogEntry.LicenseUrl);
            }

            [Fact]
            public void UsesPackageIdCaseFromLeafItemNotParameterWhenBuildingLicenseUrl()
            {
                var leaf = V3Data.Leaf;
                leaf.LicenseExpression = "MIT";
                leaf.PackageId = "WindowsAzure.Storage";
                Id = "windowsazure.storage";

                Target.UpdateLeafItem(LeafItem, Hive, Id, leaf);

                Assert.Equal("https://example-gallery/packages/WindowsAzure.Storage/7.1.2-alpha/license", LeafItem.CatalogEntry.LicenseUrl);
            }

            [Fact]
            public void UsesEmptyStringWhenThereIsNoIconUrl()
            {
                var leaf = V3Data.Leaf;
                leaf.IconUrl = null;

                Target.UpdateLeafItem(LeafItem, Hive, Id, leaf);

                Assert.Equal(string.Empty, LeafItem.CatalogEntry.IconUrl);
            }

            [Fact]
            public void UsesFlatContainerForIconUrlWhenThereIsIconFile()
            {
                var leaf = V3Data.Leaf;
                leaf.IconUrl = null;
                leaf.IconFile = "icon.png";

                Target.UpdateLeafItem(LeafItem, Hive, Id, leaf);

                Assert.Equal("https://example/fc/windowsazure.storage/7.1.2-alpha/icon", LeafItem.CatalogEntry.IconUrl);
            }

            [Theory]
            [InlineData(HiveType.Legacy, false)]
            [InlineData(HiveType.Gzipped, false)]
            [InlineData(HiveType.SemVer2, true)]
            public void ExcludesDeprecationInformationForNonSemVer2Hives(HiveType hive, bool hasDeprecation)
            {
                Hive = hive;

                var leaf = V3Data.Leaf;
                leaf.Deprecation = new PackageDeprecation
                {
                    Message = "Don't use this for real.",
                    Reasons = new List<string> { "Other" },
                    Url = "https://catalog/#deprecation",
                };

                Target.UpdateLeafItem(LeafItem, Hive, Id, leaf);

                Assert.Equal(hasDeprecation, LeafItem.CatalogEntry.Deprecation != null);
            }

            [Fact]
            public void PopulatesProperties()
            {
                Hive = HiveType.SemVer2;
                var leaf = V3Data.Leaf;
                leaf.Deprecation = new PackageDeprecation
                {
                    Message = "Don't use this for real.",
                    Reasons = new List<string> { "Other", "Legacy" },
                    Url = "https://catalog/#deprecation",
                    AlternatePackage = new AlternatePackage
                    {
                        Id = "NuGet.Core",
                        Range = "[2.8.6, )",
                        Url = "https://catalog/#alternative"
                    }
                };

                Target.UpdateLeafItem(LeafItem, Hive, Id, leaf);

                var json = JsonConvert.SerializeObject(LeafItem, SerializerSettings);
                Assert.Equal(
                    @"{
  ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/7.1.2-alpha.json"",
  ""@type"": ""Package"",
  ""commitTimeStamp"": ""0001-01-01T00:00:00+00:00"",
  ""catalogEntry"": {
    ""@type"": ""PackageDetails"",
    ""authors"": ""Microsoft"",
    ""dependencyGroups"": [
      {
        ""dependencies"": [
          {
            ""id"": ""Microsoft.Data.OData"",
            ""range"": ""[5.6.4, )"",
            ""registration"": ""https://example/reg-gz-semver2/microsoft.data.odata/index.json""
          },
          {
            ""id"": ""Newtonsoft.Json"",
            ""range"": ""[6.0.8, )"",
            ""registration"": ""https://example/reg-gz-semver2/newtonsoft.json/index.json""
          }
        ],
        ""targetFramework"": "".NETFramework4.0-Client""
      }
    ],
    ""deprecation"": {
      ""@id"": ""https://catalog/#deprecation"",
      ""@type"": ""deprecation"",
      ""alternatePackage"": {
        ""@id"": ""https://catalog/#alternative"",
        ""@type"": ""alternatePackage"",
        ""id"": ""NuGet.Core"",
        ""range"": ""[2.8.6, )""
      },
      ""message"": ""Don't use this for real."",
      ""reasons"": [
        ""Other"",
        ""Legacy""
      ]
    },
    ""description"": ""Description."",
    ""iconUrl"": ""https://example/fc/windowsazure.storage/7.1.2-alpha/icon"",
    ""id"": ""WindowsAzure.Storage"",
    ""language"": ""en-US"",
    ""licenseExpression"": """",
    ""licenseUrl"": ""http://go.microsoft.com/fwlink/?LinkId=331471"",
    ""listed"": true,
    ""minClientVersion"": ""2.12"",
    ""packageContent"": ""https://example/fc/windowsazure.storage/7.1.2-alpha/windowsazure.storage.7.1.2-alpha.nupkg"",
    ""projectUrl"": ""https://github.com/Azure/azure-storage-net"",
    ""published"": ""2017-01-03T00:00:00+00:00"",
    ""requireLicenseAcceptance"": true,
    ""summary"": ""Summary."",
    ""tags"": [
      ""Microsoft"",
      ""Azure"",
      ""Storage"",
      ""Table"",
      ""Blob"",
      ""File"",
      ""Queue"",
      ""Scalable"",
      ""windowsazureofficial""
    ],
    ""title"": ""Windows Azure Storage"",
    ""version"": ""7.1.2-alpha+git""
  },
  ""packageContent"": ""https://example/fc/windowsazure.storage/7.1.2-alpha/windowsazure.storage.7.1.2-alpha.nupkg"",
  ""registration"": ""https://example/reg-gz-semver2/windowsazure.storage/index.json""
}",
                    json);
            }
        }

        public class NewLeaf : Facts
        {
            [Fact]
            public void PopulatesProperties()
            {
                Target.UpdateLeafItem(LeafItem, Hive, Id, V3Data.Leaf);

                var leaf = Target.NewLeaf(LeafItem);

                var json = JsonConvert.SerializeObject(leaf, SerializerSettings);
                Assert.Equal(
                    @"{
  ""@id"": ""https://example/reg/windowsazure.storage/7.1.2-alpha.json"",
  ""@type"": [
    ""Package"",
    ""http://schema.nuget.org/catalog#Permalink""
  ],
  ""listed"": true,
  ""packageContent"": ""https://example/fc/windowsazure.storage/7.1.2-alpha/windowsazure.storage.7.1.2-alpha.nupkg"",
  ""published"": ""2017-01-03T00:00:00+00:00"",
  ""registration"": ""https://example/reg/windowsazure.storage/index.json"",
  ""@context"": {
    ""@vocab"": ""http://schema.nuget.org/schema#"",
    ""xsd"": ""http://www.w3.org/2001/XMLSchema#"",
    ""catalogEntry"": {
      ""@type"": ""@id""
    },
    ""registration"": {
      ""@type"": ""@id""
    },
    ""packageContent"": {
      ""@type"": ""@id""
    },
    ""published"": {
      ""@type"": ""xsd:dateTime""
    }
  }
}",
                    json);
            }
        }

        public class UpdateCommit : Facts
        {
            [Fact]
            public void PopulatesProperties()
            {
                Target.UpdateCommit(LeafItem, Commit);

                Assert.Equal(V3Data.CommitId, LeafItem.CommitId);
                Assert.Equal(V3Data.CommitTimestamp, LeafItem.CommitTimestamp);
            }
        }

        public class UpdateInlinedPageItem : Facts
        {
            [Fact]
            public void PopulatesProperties()
            {
                var lower = NuGetVersion.Parse("1.0.0-BETA.1+git");
                var upper = NuGetVersion.Parse("2.0.0+foo");
                Target.UpdateCommit(Page, Commit);
                Page.Items = new List<RegistrationLeafItem> { new RegistrationLeafItem() };

                Target.UpdateInlinedPageItem(Page, Hive, Id, 1, lower, upper);

                var json = JsonConvert.SerializeObject(Page, SerializerSettings);
                Assert.Equal(@"{
  ""@id"": ""https://example/reg/windowsazure.storage/index.json#page/1.0.0-beta.1/2.0.0"",
  ""@type"": ""catalog:CatalogPage"",
  ""commitId"": ""6b9b24dd-7aec-48ae-afc1-2a117e3d50d1"",
  ""commitTimeStamp"": ""2018-12-13T12:30:00+00:00"",
  ""count"": 1,
  ""items"": [
    {
      ""commitTimeStamp"": ""0001-01-01T00:00:00+00:00""
    }
  ],
  ""parent"": ""https://example/reg/windowsazure.storage/index.json"",
  ""lower"": ""1.0.0-BETA.1"",
  ""upper"": ""2.0.0""
}", json);
            }
        }

        public class UpdateNonInlinedPageItem : Facts
        {
            [Fact]
            public void PopulatesProperties()
            {
                var lower = NuGetVersion.Parse("1.0.0-BETA.1+git");
                var upper = NuGetVersion.Parse("2.0.0+foo");
                Target.UpdateCommit(Page, Commit);

                Target.UpdateNonInlinedPageItem(Page, Hive, Id, 1, lower, upper);

                var json = JsonConvert.SerializeObject(Page, SerializerSettings);
                Assert.Equal(@"{
  ""@id"": ""https://example/reg/windowsazure.storage/page/1.0.0-beta.1/2.0.0.json"",
  ""@type"": ""catalog:CatalogPage"",
  ""commitId"": ""6b9b24dd-7aec-48ae-afc1-2a117e3d50d1"",
  ""commitTimeStamp"": ""2018-12-13T12:30:00+00:00"",
  ""count"": 1,
  ""lower"": ""1.0.0-BETA.1"",
  ""upper"": ""2.0.0""
}", json);
            }
        }

        public class UpdatePage : Facts
        {
            [Fact]
            public void PopulatesProperties()
            {
                var lower = NuGetVersion.Parse("1.0.0-BETA.1+git");
                var upper = NuGetVersion.Parse("2.0.0+foo");
                Target.UpdateCommit(Page, Commit);
                Page.Items = new List<RegistrationLeafItem> { new RegistrationLeafItem() };

                Target.UpdatePage(Page, Hive, Id, 1, lower, upper);

                var json = JsonConvert.SerializeObject(Page, SerializerSettings);
                Assert.Equal(@"{
  ""@id"": ""https://example/reg/windowsazure.storage/page/1.0.0-beta.1/2.0.0.json"",
  ""@type"": ""catalog:CatalogPage"",
  ""commitId"": ""6b9b24dd-7aec-48ae-afc1-2a117e3d50d1"",
  ""commitTimeStamp"": ""2018-12-13T12:30:00+00:00"",
  ""count"": 1,
  ""items"": [
    {
      ""commitTimeStamp"": ""0001-01-01T00:00:00+00:00""
    }
  ],
  ""parent"": ""https://example/reg/windowsazure.storage/index.json"",
  ""lower"": ""1.0.0-BETA.1"",
  ""upper"": ""2.0.0"",
  ""@context"": {
    ""@vocab"": ""http://schema.nuget.org/schema#"",
    ""catalog"": ""http://schema.nuget.org/catalog#"",
    ""xsd"": ""http://www.w3.org/2001/XMLSchema#"",
    ""items"": {
      ""@id"": ""catalog:item"",
      ""@container"": ""@set""
    },
    ""commitTimeStamp"": {
      ""@id"": ""catalog:commitTimeStamp"",
      ""@type"": ""xsd:dateTime""
    },
    ""commitId"": {
      ""@id"": ""catalog:commitId""
    },
    ""count"": {
      ""@id"": ""catalog:count""
    },
    ""parent"": {
      ""@id"": ""catalog:parent"",
      ""@type"": ""@id""
    },
    ""tags"": {
      ""@id"": ""tag"",
      ""@container"": ""@set""
    },
    ""reasons"": {
      ""@container"": ""@set""
    },
    ""packageTargetFrameworks"": {
      ""@id"": ""packageTargetFramework"",
      ""@container"": ""@set""
    },
    ""dependencyGroups"": {
      ""@id"": ""dependencyGroup"",
      ""@container"": ""@set""
    },
    ""dependencies"": {
      ""@id"": ""dependency"",
      ""@container"": ""@set""
    },
    ""packageContent"": {
      ""@type"": ""@id""
    },
    ""published"": {
      ""@type"": ""xsd:dateTime""
    },
    ""registration"": {
      ""@type"": ""@id""
    }
  }
}", json);
            }
        }

        public class UpdateIndex : Facts
        {
            [Fact]
            public void PopulatesProperties()
            {
                Target.UpdateCommit(Index, Commit);
                Index.Items = new List<RegistrationPage>() { new RegistrationPage() };

                Target.UpdateIndex(Index, Hive, Id, 1);

                var json = JsonConvert.SerializeObject(Index, SerializerSettings);
                Assert.Equal(@"{
  ""@id"": ""https://example/reg/windowsazure.storage/index.json"",
  ""@type"": [
    ""catalog:CatalogRoot"",
    ""PackageRegistration"",
    ""catalog:Permalink""
  ],
  ""commitId"": ""6b9b24dd-7aec-48ae-afc1-2a117e3d50d1"",
  ""commitTimeStamp"": ""2018-12-13T12:30:00+00:00"",
  ""count"": 1,
  ""items"": [
    {
      ""commitTimeStamp"": ""0001-01-01T00:00:00+00:00"",
      ""count"": 0
    }
  ],
  ""@context"": {
    ""@vocab"": ""http://schema.nuget.org/schema#"",
    ""catalog"": ""http://schema.nuget.org/catalog#"",
    ""xsd"": ""http://www.w3.org/2001/XMLSchema#"",
    ""items"": {
      ""@id"": ""catalog:item"",
      ""@container"": ""@set""
    },
    ""commitTimeStamp"": {
      ""@id"": ""catalog:commitTimeStamp"",
      ""@type"": ""xsd:dateTime""
    },
    ""commitId"": {
      ""@id"": ""catalog:commitId""
    },
    ""count"": {
      ""@id"": ""catalog:count""
    },
    ""parent"": {
      ""@id"": ""catalog:parent"",
      ""@type"": ""@id""
    },
    ""tags"": {
      ""@id"": ""tag"",
      ""@container"": ""@set""
    },
    ""reasons"": {
      ""@container"": ""@set""
    },
    ""packageTargetFrameworks"": {
      ""@id"": ""packageTargetFramework"",
      ""@container"": ""@set""
    },
    ""dependencyGroups"": {
      ""@id"": ""dependencyGroup"",
      ""@container"": ""@set""
    },
    ""dependencies"": {
      ""@id"": ""dependency"",
      ""@container"": ""@set""
    },
    ""packageContent"": {
      ""@type"": ""@id""
    },
    ""published"": {
      ""@type"": ""xsd:dateTime""
    },
    ""registration"": {
      ""@type"": ""@id""
    }
  }
}", json);
            }
        }

        public class UpdateIndexUrls : Facts
        {
            public UpdateIndexUrls()
            {
                Page.Items = new List<RegistrationLeafItem> { LeafItem };
                Index.Items = new List<RegistrationPage> { Page };
                Target.UpdateLeafItem(LeafItem, Hive, Id, V3Data.Leaf);
                Target.UpdateInlinedPageItem(Page, Hive, Id, 1, NuGetVersion.Parse(V3Data.FullVersion), NuGetVersion.Parse(V3Data.FullVersion));
                Target.UpdateIndex(Index, Hive, Id, 1);
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            [InlineData(3)]
            public void ConvertsHive(int trips)
            {
                Target.UpdateIndexUrls(Index, Hive, HiveType.Gzipped);
                for (var i = 1; i < trips; i++)
                {
                    Target.UpdateIndexUrls(Index, HiveType.Gzipped, Hive);
                    Target.UpdateIndexUrls(Index, Hive, HiveType.Gzipped);
                }

                Assert.Equal("https://example/reg-gz/windowsazure.storage/index.json", Index.Url);
                Assert.Equal("https://example/reg-gz/windowsazure.storage/index.json#page/7.1.2-alpha/7.1.2-alpha", Page.Url);
                Assert.Equal("https://example/reg-gz/windowsazure.storage/index.json", Page.Parent);
                Assert.Equal("https://example/reg-gz/windowsazure.storage/7.1.2-alpha.json", LeafItem.Url);
                Assert.Equal("https://example/reg-gz/windowsazure.storage/index.json", LeafItem.Registration);
                Assert.Single(LeafItem.CatalogEntry.DependencyGroups);
                Assert.Equal(2, LeafItem.CatalogEntry.DependencyGroups[0].Dependencies.Count);
                Assert.Equal("https://example/reg-gz/microsoft.data.odata/index.json", LeafItem.CatalogEntry.DependencyGroups[0].Dependencies[0].Registration);
                Assert.Equal("https://example/reg-gz/newtonsoft.json/index.json", LeafItem.CatalogEntry.DependencyGroups[0].Dependencies[1].Registration);
            }

            [Fact]
            public void DoesNotContainUrlToOldHive()
            {
                Target.UpdateIndexUrls(Index, Hive, HiveType.Gzipped);

                var json = JsonConvert.SerializeObject(Index, SerializerSettings);
                Assert.DoesNotContain("https://example/reg/", json);
                Assert.Contains("https://example/reg-gz/", json);
            }
        }

        public class UpdatePageUrls : Facts
        {
            public UpdatePageUrls()
            {
                Page.Items = new List<RegistrationLeafItem> { LeafItem };
                Target.UpdateLeafItem(LeafItem, Hive, Id, V3Data.Leaf);
                Target.UpdatePage(Page, Hive, Id, 1, NuGetVersion.Parse(V3Data.FullVersion), NuGetVersion.Parse(V3Data.FullVersion));
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            [InlineData(3)]
            public void ConvertsHive(int trips)
            {
                Target.UpdatePageUrls(Page, Hive, HiveType.Gzipped);
                for (var i = 1; i < trips; i++)
                {
                    Target.UpdatePageUrls(Page, HiveType.Gzipped, Hive);
                    Target.UpdatePageUrls(Page, Hive, HiveType.Gzipped);
                }

                Assert.Equal("https://example/reg-gz/windowsazure.storage/page/7.1.2-alpha/7.1.2-alpha.json", Page.Url);
                Assert.Equal("https://example/reg-gz/windowsazure.storage/index.json", Page.Parent);
                Assert.Equal("https://example/reg-gz/windowsazure.storage/7.1.2-alpha.json", LeafItem.Url);
                Assert.Equal("https://example/reg-gz/windowsazure.storage/index.json", LeafItem.Registration);
                Assert.Single(LeafItem.CatalogEntry.DependencyGroups);
                Assert.Equal(2, LeafItem.CatalogEntry.DependencyGroups[0].Dependencies.Count);
                Assert.Equal("https://example/reg-gz/microsoft.data.odata/index.json", LeafItem.CatalogEntry.DependencyGroups[0].Dependencies[0].Registration);
                Assert.Equal("https://example/reg-gz/newtonsoft.json/index.json", LeafItem.CatalogEntry.DependencyGroups[0].Dependencies[1].Registration);
            }

            [Fact]
            public void DoesNotContainUrlToOldHive()
            {
                Target.UpdatePageUrls(Page, Hive, HiveType.Gzipped);

                var json = JsonConvert.SerializeObject(Page, SerializerSettings);
                Assert.DoesNotContain("https://example/reg/", json);
                Assert.Contains("https://example/reg-gz/", json);
            }
        }

        public class UpdateLeafUrls : Facts
        {
            public UpdateLeafUrls()
            {
                Target.UpdateLeafItem(LeafItem, Hive, Id, V3Data.Leaf);
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            [InlineData(3)]
            public void ConvertsHive(int trips)
            {
                var leaf = Target.NewLeaf(LeafItem);

                Target.UpdateLeafUrls(leaf, Hive, HiveType.Gzipped);
                for (var i = 1; i < trips; i++)
                {
                    Target.UpdateLeafUrls(leaf, HiveType.Gzipped, Hive);
                    Target.UpdateLeafUrls(leaf, Hive, HiveType.Gzipped);
                }

                Assert.Equal("https://example/reg-gz/windowsazure.storage/7.1.2-alpha.json", leaf.Url);
                Assert.Equal("https://example/reg-gz/windowsazure.storage/index.json", leaf.Registration);
            }

            [Fact]
            public void DoesNotContainUrlToOldHive()
            {
                var leaf = Target.NewLeaf(LeafItem);

                Target.UpdateLeafUrls(leaf, Hive, HiveType.Gzipped);

                var json = JsonConvert.SerializeObject(leaf, SerializerSettings);
                Assert.DoesNotContain("https://example/reg/", json);
                Assert.Contains("https://example/reg-gz/", json);
            }
        }

        public abstract class Facts
        {
            public Facts()
            {
                Options = new Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();
                Config = new Catalog2RegistrationConfiguration
                {
                    LegacyBaseUrl = "https://example/reg/",
                    GzippedBaseUrl = "https://example/reg-gz/",
                    SemVer2BaseUrl = "https://example/reg-gz-semver2/",
                    GalleryBaseUrl = "https://example-gallery/",
                    FlatContainerBaseUrl = "https://example/fc/",
                };
                Options.Setup(x => x.Value).Returns(() => Config);

                LeafItem = new RegistrationLeafItem();
                Page = new RegistrationPage();
                Index = new RegistrationIndex();
                Hive = HiveType.Legacy;
                Id = V3Data.PackageId;
                PackageDetails = new PackageDetailsCatalogLeaf
                {
                    PackageVersion = V3Data.NormalizedVersion,
                };
                Commit = new CatalogCommit(V3Data.CommitId, V3Data.CommitTimestamp);

                SerializerSettings = NuGetJsonSerialization.Settings;
                SerializerSettings.Formatting = Formatting.Indented;
            }

            public Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>> Options { get; }
            public Catalog2RegistrationConfiguration Config { get; }
            public RegistrationUrlBuilder UrlBuilder => new RegistrationUrlBuilder(Options.Object);
            public EntityBuilder Target => new EntityBuilder(UrlBuilder, Options.Object);
            public RegistrationLeafItem LeafItem { get; }
            public RegistrationPage Page { get; }
            public RegistrationIndex Index { get; }
            public HiveType Hive { get; set; }
            public string Id { get; set; }
            public PackageDetailsCatalogLeaf PackageDetails { get; }
            public CatalogCommit Commit { get; }
            public JsonSerializerSettings SerializerSettings { get; }
        }
    }
}
