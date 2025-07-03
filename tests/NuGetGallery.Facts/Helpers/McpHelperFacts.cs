// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGetGallery.Services.Helpers;
using NuGetGallery.Services.Models;
using NuGetGallery.TestData;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery.Helpers
{
    public class McpHelperFacts
    {
        public class IsMcpServerPackageMethod
        {
            [Fact]
            public void ReturnsTrue_WhenPackageIsDotnetToolAndMcpServer()
            {
                // Arrange
                var packageTypes = new List<PackageType>
                {
                    new("DotnetTool", new Version("1.0.0")),
                    new("McpServer", new Version("1.0.0")),
                };
                var packageStream = PackageServiceUtility.CreateNuGetPackageStream(packageTypes: packageTypes);
                var package = PackageServiceUtility.CreateNuGetPackage(packageStream);

                // Act
                var isMcpServerPackage = McpHelper.IsMcpServerPackage(package.Object);

                // Assert
                Assert.True(isMcpServerPackage);
            }

            [Fact]
            public void ReturnsFalse_WhenPackageIsNotDotnetTool()
            {
                // Arrange
                var packageTypes = new List<PackageType>
                {
                    new("McpServer", new Version("1.0.0")),
                };
                var packageStream = PackageServiceUtility.CreateNuGetPackageStream(packageTypes: packageTypes);
                var package = PackageServiceUtility.CreateNuGetPackage(packageStream);

                // Act
                var isMcpServerPackage = McpHelper.IsMcpServerPackage(package.Object);

                // Assert
                Assert.False(isMcpServerPackage);
            }

            [Fact]
            public void ReturnsFalse_WhenPackageIsNotMcpServer()
            {
                // Arrange
                var packageTypes = new List<PackageType>
                {
                    new("DotnetTool", new Version("1.0.0")),
                };
                var packageStream = PackageServiceUtility.CreateNuGetPackageStream(packageTypes: packageTypes);
                var package = PackageServiceUtility.CreateNuGetPackage(packageStream);

                // Act
                var isMcpServerPackage = McpHelper.IsMcpServerPackage(package.Object);

                // Assert
                Assert.False(isMcpServerPackage);
            }
        }

        public class PackageContainsMcpServerMetadataMethod
        {
            [Fact]
            public void ReturnsTrue_WhenMetadataFileIsPresent()
            {
                // Arrange
                var packageStream = PackageServiceUtility.CreateNuGetPackageStream(
                    mcpServerMetadataFilename: ".mcp/server.json",
                    mcpServerMetadataFileContents: []);
                var package = PackageServiceUtility.CreateNuGetPackage(packageStream);

                // Act
                var containsMetadata = McpHelper.PackageContainsMcpServerMetadata(package.Object);

                // Assert
                Assert.True(containsMetadata);
            }

            [Fact]
            public void ReturnsFalse_WhenMetadataFileIsMissing()
            {
                // Arrange
                var packageStream = PackageServiceUtility.CreateNuGetPackageStream();
                var package = PackageServiceUtility.CreateNuGetPackage(packageStream);

                // Act
                var containsMetadata = McpHelper.PackageContainsMcpServerMetadata(package.Object);

                // Assert
                Assert.False(containsMetadata);
            }
        }

        public class ReadMcpServerMetadataMethod
        {
            [Fact]
            public void ReturnsMetadataContent_WhenFileExists()
            {
                // Arrange
                var expectedContent = "{\"key\":\"value\"}";

                var packageStream = PackageServiceUtility.CreateNuGetPackageStream(
                    mcpServerMetadataFilename: ".mcp/server.json",
                    mcpServerMetadataFileContents: Encoding.UTF8.GetBytes(expectedContent));
                var package = PackageServiceUtility.CreateNuGetPackage(packageStream);

                // Act
                var actualContent = McpHelper.ReadMcpServerMetadata(package.Object);

                // Assert
                Assert.True(actualContent == expectedContent);
            }
        }

        public class ReadMcpServerMetadataAsyncMethod
        {
            [Fact]
            public async Task ReturnsMetadataContent_WhenFileExists()
            {
                // Arrange
                var expectedContent = "{\"key\":\"value\"}";

                var packageStream = PackageServiceUtility.CreateNuGetPackageStream(
                    mcpServerMetadataFilename: ".mcp/server.json",
                    mcpServerMetadataFileContents: Encoding.UTF8.GetBytes(expectedContent));
                var package = PackageServiceUtility.CreateNuGetPackage(packageStream);

                // Act
                var actualContent = await McpHelper.ReadMcpServerMetadataAsync(package.Object);

                // Assert
                Assert.True(actualContent == expectedContent);
            }
        }

        public class CreateVsCodeMcpServerEntryTemplateMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void ReturnsMissingMetadata_WhenMetadataIsNullOrWhiteSpace(string metadataJson)
            {
                // Arrange
                var expectedResult = new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.MissingMetadata,
                    Template = string.Empty,
                };

                // Act
                var actualResult = McpHelper.CreateVsCodeMcpServerEntryTemplate(metadataJson);

                // Assert
                Assert.True(actualResult == expectedResult);
            }

            [Theory]
            [InlineData(McpServerData.ServerJsonNoNugetRegistry)]
            [InlineData(McpServerData.ServerJsonNullString)]
            [InlineData(McpServerData.ServerJsonNullList)]
            [InlineData(McpServerData.ServerJsonNullPackage)]
            public void ReturnsMissingNugetRegistry_WhenNoNugetRegistryInMetadata(string metadataJson)
            {
                // Arrange
                var expectedResult = new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.MissingNugetRegistry,
                    Template = string.Empty,
                };

                // Act
                var actualResult = McpHelper.CreateVsCodeMcpServerEntryTemplate(metadataJson);

                // Assert
                Assert.True(actualResult == expectedResult);
            }

            [Theory]
            [InlineData(McpServerData.ServerJsonNullPackageArgument)]
            [InlineData(McpServerData.ServerJsonNonTypedPackageArgs)]
            public void ReturnsInvalid_WhenMetadataIsMalformed(string metadataJson)
            {
                // Arrange
                var expectedResult = new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.InvalidMetadata,
                    Template = string.Empty,
                };

                // Act
                var actualResult = McpHelper.CreateVsCodeMcpServerEntryTemplate(metadataJson);

                // Assert
                Assert.True(actualResult == expectedResult);
            }

            [Theory]
            [InlineData(McpServerData.ServerJsonValid, McpServerData.McpJsonValid)]
            [InlineData(McpServerData.ServerJsonEmptyArgsAndEnv, McpServerData.McpJsonMinimal)]
            [InlineData(McpServerData.ServerJsonNoArgsAndEnv, McpServerData.McpJsonMinimal)]
            [InlineData(McpServerData.ServerJsonNoNamedArgValues, McpServerData.McpJsonMinimal)]
            [InlineData(McpServerData.ServerJsonNoPositionalArgValues, McpServerData.McpJsonMinimal)]
            [InlineData(McpServerData.ServerJsonNoEnvVarValues, McpServerData.McpJsonMinimal)]
            [InlineData(McpServerData.ServerJsonNullEnvVar, McpServerData.McpJsonMinimal)]
            public void ReturnsSuccess_WhenMetadataIsValid(string metadataJson, string vsCodeTemplateJson)
            {
                // Arrange
                var expectedResult = new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.Success,
                    Template = vsCodeTemplateJson,
                };

                // Act
                var actualResult = McpHelper.CreateVsCodeMcpServerEntryTemplate(metadataJson);

                // Assert
                Assert.True(actualResult == expectedResult);
            }
        }

        public class MapEnvVarsToEnvMethod
        {
            [Fact]
            public void MapsEnvironmentVariablesToDictionary()
            {
                // Arrange
                var envVars = new List<EnvironmentVariable>
                {
                    null,
                    new() { Name = "USER", Description = "User name" },
                    new() { Name = "TOKEN", Description = "Access token" }
                };

                // Act
                var result = McpHelper.MapEnvVarsToEnv(envVars);

                // Assert
                Assert.Equal(2, result.Count);
                Assert.Equal("${input:input-1}", result["USER"]);
                Assert.Equal("${input:input-2}", result["TOKEN"]);
            }
        }

        public class MapEnvVarsToInputsMethod
        {
            [Fact]
            public void MapsEnvironmentVariablesToInputs()
            {
                // Arrange
                var envVars = new List<EnvironmentVariable>
                {
                    null,
                    new() { Name = "USER", Description = "User name" },
                    new() { Name = "TOKEN", Description = "Access token", IsSecret = true },
                    new() { Name = "HOST", Description = "Database host", Default = "localhost" },
                    new() { Name = "PORT", Description = "Database port", Choices = ["1", "2", "3"] }
                };

                // Act
                var result = McpHelper.MapEnvVarsToInputs(envVars);

                // Assert
                Assert.Equal(4, result.Count);

                Assert.Equal("promptString", result[0].Type);
                Assert.Equal("input-1", result[0].Id);
                Assert.Equal("User name", result[0].Description);
                Assert.False(result[0].Password);

                Assert.Equal("promptString", result[1].Type);
                Assert.Equal("input-2", result[1].Id);
                Assert.Equal("Access token", result[1].Description);
                Assert.True(result[1].Password);

                Assert.Equal("promptString", result[2].Type);
                Assert.Equal("input-3", result[2].Id);
                Assert.Equal("Database host", result[2].Description);
                Assert.False(result[2].Password);
                Assert.Equal("localhost", result[2].Default);

                Assert.Equal("pickString", result[3].Type);
                Assert.Equal("input-4", result[3].Id);
                Assert.Equal("Database port", result[3].Description);
                Assert.False(result[3].Password);
                Assert.Equal(["1", "2", "3"], result[3].Choices);
            }
        }

        public class MapArgumentsToInputsMethod
        {
            [Fact]
            public void MapsArgumentsToInputsWithCorrectStartId()
            {
                // Arrange
                var args = new List<Argument>
                {
                    null,
                    new PositionalArgument()
                    {
                        Type = "positional",
                        Description = "First arg",
                        Value = "arg1",
                        IsRequired = true,
                        IsRepeated = false,
                        Format = "string",
                        Choices = ["1", "2", "3"],
                        ValueHint = "Enter first arg",
                        Default = "default1"
                    },
                    new NamedArgument()
                    {
                        Type = "named",
                        Description = "Second arg",
                        Name = "secondArg",
                    }
                };
                int startId = 2;

                // Act
                var result = McpHelper.MapArgumentsToInputs(args, startId);

                // Assert
                Assert.Equal(2, result.Count);

                Assert.Equal("pickString", result[0].Type);
                Assert.Equal("input-2", result[0].Id);
                Assert.Equal("First arg", result[0].Description);
                Assert.False(result[0].Password);
                Assert.Equal("default1", result[0].Default);
                Assert.Equal(["1", "2", "3"], result[0].Choices);

                Assert.Equal("promptString", result[1].Type);
                Assert.Equal("input-3", result[1].Id);
            }
        }
    }
}
