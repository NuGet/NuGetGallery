using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Indexing;
using Xunit;

namespace NuGet.IndexingTests.Extraction
{
    public class CatalogPackageMetadataExtractorTests
    {
        [Theory, MemberData(nameof(AddsListedData))]
        public void AddsListed(object catalogEntry, string expected)
        {
            // Arrange
            var catalogEntryJObject = CatalogEntry(catalogEntry);

            // Act
            var metadata = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogEntryJObject);

            // Assert
            Assert.Contains("listed", metadata.Keys);
            Assert.Equal(expected, metadata["listed"]);
        }

        [Theory, MemberData(nameof(AddsSupportedFrameworksData))]
        public void AddsSupportedFrameworks(object catalogEntry, string expected)
        {
            // Arrange
            var catalogEntryJObject = CatalogEntry(catalogEntry);

            // Act
            var metadata = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogEntryJObject);

            // Assert
            Assert.Contains("supportedFrameworks", metadata.Keys);
            Assert.Equal(expected, metadata["supportedFrameworks"]);
        }

        [Theory, MemberData(nameof(AddsFlattenedDependenciesData))]
        public void AddsFlattenedDependencies(object catalogEntry, string expected)
        {
            // Arrange
            var catalogEntryJObject = CatalogEntry(catalogEntry);

            // Act
            var metadata = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogEntryJObject);

            // Assert
            Assert.Contains("flattenedDependencies", metadata.Keys);
            Assert.Equal(expected, metadata["flattenedDependencies"]);
        }

        public static IEnumerable<object[]> AddsListedData
        {
            get
            {
                yield return new object[] { new { }, "true" };
                yield return new object[] { new { listed = (string)null }, "true" };
                yield return new object[] { new { listed = "TRUE" }, "TRUE" };
                yield return new object[] { new { listed = "False" }, "False" };
                yield return new object[] { new { listed = "Bad" }, "Bad" }; // validation is not done at this stage
                yield return new object[] { new { published = "1900-01-01T00:00:00" }, "false" };
                yield return new object[] { new { published = "1900-01-02T00:00:00" }, "true" };
                yield return new object[] { new { published = "1900-01-01T00:00:00", listed = "True" }, "True" };
            }
        }

        public static IEnumerable<object[]> AddsSupportedFrameworksData
        {
            get
            {
                yield return new object[] { WithFrameworkAssemblyGroup(".NETFramework4.0-Client"), "net40-Client" };
                yield return new object[] { WithFrameworkAssemblyGroup(".NETFramework4.0-Client, .NETFramework4.5"), "net40-Client|net45" };
                yield return new object[] { WithFrameworkAssemblyGroup("   .NETFramework4.0-Client, , , .NETFramework4.5  ,,"), "net40-Client|net45" };
                yield return new object[]
                {
                    new
                    {
                        frameworkAssemblyGroup = new object[]
                        {
                            new { targetFramework = ".NETFramework4.0-Client" },
                            new { targetFramework = ".NETFramework4.0, .NETFramework4.5" },
                            new { targetFramework = "  " }
                        }
                    },
                    "net40-Client|net40|net45"
                };

                // a single item
                yield return new object[]
                {
                    new
                    {
                        frameworkAssemblyGroup = new { targetFramework = ".NETFramework4.0, .NETFramework4.5" }
                    },
                    "net40|net45"
                };
            }
        }

        public static IEnumerable<object[]> AddsFlattenedDependenciesData
        {
            get
            {
                // multiple packages
                yield return new object[]
                {
                    new
                    {
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "4.5.11" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },

                    },
                    "Newtonsoft.Json:4.5.11|Microsoft.Data.OData:5.6.2"
                };

                // multiple target frameworks
                yield return new object[]
                {
                    new
                    {
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "4.5.11" }
                                },
                                targetFramework = ".NETFramework4.5"
                            },
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                },
                                targetFramework = ".NETFramework4.0-Client"
                            },
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },

                    },
                    "Newtonsoft.Json:4.5.11:net45|Microsoft.Data.OData:5.6.2:net40-Client|Microsoft.Data.OData:5.6.2"
                };

                // a single item
                yield return new object[]
                {
                    new
                    {
                        dependencyGroups = new
                        {
                            dependencies = new object[]
                            {
                                new { id = "Newtonsoft.Json", range = "4.5.11" },
                                new { id = "Microsoft.Data.OData", range = "5.6.2" }
                            }
                        }
                    },
                    "Newtonsoft.Json:4.5.11|Microsoft.Data.OData:5.6.2"
                };

                // different target framework format
                yield return new object[] { WithDependency("Newtonsoft.Json", "4.5.11", ".NETFramework4.5"), "Newtonsoft.Json:4.5.11:net45" };
                yield return new object[] { WithDependency("Newtonsoft.Json", "4.5.11", ".NETPortable0.0-wp8+netcore45+net45"), "Newtonsoft.Json:4.5.11:portable-wp80+win+net45" };
                yield return new object[] { WithDependency("Newtonsoft.Json", "4.5.11", string.Empty), "Newtonsoft.Json:4.5.11" };
                yield return new object[] { WithDependency("Newtonsoft.Json", "4.5.11", " "), "Newtonsoft.Json:4.5.11" };
                yield return new object[] { WithDependency("Newtonsoft.Json", "4.5.11", null), "Newtonsoft.Json:4.5.11" };
                yield return new object[]
                {
                    new
                    {
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new
                                    {
                                        id = "Newtonsoft.Json",
                                        range = "4.5.11"
                                    }
                                }
                            }
                        }
                    },
                    "Newtonsoft.Json:4.5.11"
                };
            }
        }

        private static object WithDependency(string id, string range, string targetFramework)
        {
            return new
            {
                dependencyGroups = new object[]
                {
                    new
                    {
                        dependencies = new object[]
                        {
                            new { id, range }
                        },
                        targetFramework
                    }
                }
            };
        }

        private static object WithFrameworkAssemblyGroup(string targetFramework)
        {
            return new
            {
                frameworkAssemblyGroup = new object[]
                {
                    new { targetFramework }
                }
            };
        }

        private static JObject CatalogEntry(object obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            return JsonConvert.DeserializeObject<JObject>(json);
        }
    }
}
