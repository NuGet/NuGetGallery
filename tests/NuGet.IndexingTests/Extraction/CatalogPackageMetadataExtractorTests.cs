using System.Collections.Generic;
using System.Linq;
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
            Assert.Contains(MetadataConstants.ListedPropertyName, metadata.Keys);
            Assert.Equal(expected, metadata[MetadataConstants.ListedPropertyName]);
        }

        [Theory, MemberData(nameof(AddsSemVerLevelKeyData))]
        public void AddsSemVerLevelKey(object catalogEntry, bool expectedToContainKey, string expected)
        {
            // Arrange
            var catalogEntryJObject = CatalogEntry(catalogEntry);

            // Act
            var metadata = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogEntryJObject);


            // Assert
            Assert.Equal(expectedToContainKey, metadata.Keys.Contains(MetadataConstants.SemVerLevelKeyPropertyName));
            if (expectedToContainKey)
            {
                Assert.Equal(expected, metadata[MetadataConstants.SemVerLevelKeyPropertyName]);
            }
        }

        [Theory, MemberData(nameof(AddsSupportedFrameworksData))]
        public void AddsSupportedFrameworks(object catalogEntry, string expected)
        {
            // Arrange
            var catalogEntryJObject = CatalogEntry(catalogEntry);

            // Act
            var metadata = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogEntryJObject);

            // Assert
            Assert.Contains(MetadataConstants.SupportedFrameworksPropertyName, metadata.Keys);
            Assert.Equal(expected.Split('|').OrderBy(f => f), metadata[MetadataConstants.SupportedFrameworksPropertyName].Split('|').OrderBy(f => f));
        }

        [Theory, MemberData(nameof(AddsFlattenedDependenciesData))]
        public void AddsFlattenedDependencies(object catalogEntry, string expected)
        {
            // Arrange
            var catalogEntryJObject = CatalogEntry(catalogEntry);

            // Act
            var metadata = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogEntryJObject);

            // Assert
            Assert.Contains(MetadataConstants.FlattenedDependenciesPropertyName, metadata.Keys);
            Assert.Equal(expected, metadata[MetadataConstants.FlattenedDependenciesPropertyName]);
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

        public static IEnumerable<object[]> AddsSemVerLevelKeyData
        {
            get
            {
                // no dependencies
                yield return new object[] { new { verbatimVersion = "1.0.0" }, false, null };
                yield return new object[] { new { verbatimVersion = "1.0.0-semver1" }, false, null };
                yield return new object[] { new { verbatimVersion = "1.0.0-semver2.0" }, true, "2" };
                yield return new object[] { new { verbatimVersion = "1.0.0-semver2.0+again" }, true, "2" };
                yield return new object[] { new { verbatimVersion = "1.0.0+aThirdTime" }, true, "2" };

                // dependencies
                yield return new object[] { new {
                    verbatimVersion = "1.0.0",
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
                    false,
                    null
                };

                yield return new object[] { new {
                    verbatimVersion = "1.0.0+semver2",
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
                    true,
                    "2"
                };

                // dependencies show semver2
                yield return new object[] { new {
                    verbatimVersion = "1.0.0",
                    dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "4.5.11-semver2.0.dep" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    true,
                    "2"
                };

                yield return new object[] { new {
                    verbatimVersion = "1.0.0",
                    dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "4.5.11-semver2.0.dep+meta" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    true,
                    "2"
                };

                // semver2 in real ranges
                yield return new object[] { new {
                    verbatimVersion = "1.0.0",
                    dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "(4.5.11, 6.0.0-semver.2]" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    true,
                    "2"
                };

                yield return new object[] { new {
                    verbatimVersion = "1.0.0",
                    dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "(4.5.11-semver.2, 6.0.0]" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    true,
                    "2"
                };

                yield return new object[] { new {
                    verbatimVersion = "1.0.0",
                    dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "(4.5.11-semver.2, ]" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    true,
                    "2"
                };

                yield return new object[] { new {
                    verbatimVersion = "1.0.0",
                    dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "(, 6.0.0-semver.2]" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    true,
                    "2"
                };

                yield return new object[] { new {
                    verbatimVersion = "1.0.0",
                    dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "(, 6.0.0]" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    false,
                    null
                };
            }
        }

        public static IEnumerable<object[]> AddsSupportedFrameworksData
        {
            get
            {
                // framework assembly group
                yield return new object[] { WithFrameworkAssemblyGroup(".NETFramework4.0-Client"), "net40-client" };
                yield return new object[] { WithFrameworkAssemblyGroup(".NETFramework4.0-Client, .NETFramework4.5"), "net40-client|net45" };
                yield return new object[] { WithFrameworkAssemblyGroup("   .NETFramework4.0-Client, , , .NETFramework4.5  ,,"), "net40-client|net45" };
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
                    "net40-client|net40|net45"
                };

                // a single framework assembly
                yield return new object[] { new { frameworkAssemblyGroup = new { targetFramework = ".NETFramework4.0, .NETFramework4.5" } }, "net40|net45" };

                // package entries
                yield return new object[] { WithPackageEntry("lib/net40/something.dll"), "net40" };
                yield return new object[] { WithPackageEntry("lib/portable-net45%2Bwin%2Bwpa81%2Bwp80%2BMonoAndroid10%2BXamarin.iOS10%2BMonoTouch10/something.dll"), "portable-net45+win8+wp8+wpa81" };
                yield return new object[]
                {
                    new
                    {
                        packageEntries = new object[]
                        {
                            new { fullName = "lib/net45/something.dll" },
                            new { fullName = "lib/net40/something-else.dll" },
                            new { fullName = "bad" }
                        }
                    },
                    "net45|net40"
                };

                // a single package entry
                yield return new object[] { new { packageEntries = new { fullName = "lib/net40/something.dll" } }, "net40" };

                // not target framework folder name
                yield return new object[]
                {
                    new
                    {
                        packageEntries = new object[]
                        {
                            new { fullName = "lib/something.dll" },
                            new { fullName = "lib/net40/something-else.dll" }
                        }
                    },
                    "net40"
                };

                // both
                yield return new object[]
                {
                    new
                    {
                        frameworkAssemblyGroup = new object[]
                        {
                            new { targetFramework = ".NETFramework4.0-Client" },
                            new { targetFramework = ".NETFramework4.0, .NETFramework4.5" },
                            new { targetFramework = "  " }
                        },
                        packageEntries = new object[]
                        {
                            new { fullName = "lib/net45/something.dll" },
                            new { fullName = "lib/net20/something.dll" },
                            new { fullName = "bad" }
                        }
                    },
                    "net40-client|net40|net45|net20"
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
                                targetFramework = ".NETFramework4.0-client"
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
                    "Newtonsoft.Json:4.5.11:net45|Microsoft.Data.OData:5.6.2:net40-client|Microsoft.Data.OData:5.6.2"
                };

                // multiple target frameworks without direct package dependencies
                yield return new object[]
                {
                    new
                    {
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[0],
                                targetFramework = ".NETFramework4.5"
                            },
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                },
                                targetFramework = ".NETFramework4.0-client"
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
                    "::net45|Microsoft.Data.OData:5.6.2:net40-client|Microsoft.Data.OData:5.6.2"
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
                yield return new object[] { WithDependency("Newtonsoft.Json", "4.5.11", ".NETFramework4.0"), "Newtonsoft.Json:4.5.11:net40" };
                yield return new object[] { WithDependency("Newtonsoft.Json", "4.5.11", string.Empty), "Newtonsoft.Json:4.5.11" };
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

        private static object WithPackageEntry(string fullName)
        {
            return new
            {
                packageEntries = new object[]
                {
                    new { fullName }
                }
            };
        }

        private static JObject CatalogEntry(object obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            return JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.DateTimeOffset
            });
        }
    }
}
