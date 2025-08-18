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
        private const string TestPackageId = "Test.McpServer";
        private const string TestPackageVersion = "1.0.0";

        public class IsMcpServerPackageMethod
        {
            [Fact]
            public void ReturnsTrue_WhenPackageIsDotnetToolAndMcpServer()
            {
                // Arrange
                var packageTypes = new List<PackageType>
                {
                    new("DotnetTool", new Version(TestPackageVersion)),
                    new("McpServer", new Version(TestPackageVersion)),
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
                    new("McpServer", new Version(TestPackageVersion)),
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
                    new("DotnetTool", new Version(TestPackageVersion)),
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
            [Theory]
            [InlineData(".mcp/server.json")]
            [InlineData(".mcp/Server.json")]
            public void ReturnsTrue_WhenMetadataFileIsPresent(string mcpServerFileName)
            {
                // Arrange
                var packageStream = PackageServiceUtility.CreateNuGetPackageStream(
                    mcpServerMetadataFilename: mcpServerFileName,
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
            [InlineData(" ")]
            public void ReturnsMissingMetadata_WhenMetadataIsNullOrWhiteSpace(string metadataJson)
            {
                // Arrange
                var expectedResult = new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.MissingMetadata,
                    Template = string.Empty,
                };

                // Act
                var actualResult = McpHelper.CreateVsCodeMcpServerEntryTemplate(metadataJson, TestPackageId, TestPackageVersion);

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
                var actualResult = McpHelper.CreateVsCodeMcpServerEntryTemplate(metadataJson, TestPackageId, TestPackageVersion);

                // Assert
                Assert.True(actualResult == expectedResult);
            }

            [Theory]
            [InlineData(McpServerData.ServerJsonNullPackageArgument)]
            [InlineData(McpServerData.ServerJsonNonTypedPackageArgs)]
            [InlineData(McpServerData.ServerJsonNullVariables)]

            public void ReturnsInvalid_WhenMetadataIsMalformed(string metadataJson)
            {
                // Arrange
                var expectedResult = new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.InvalidMetadata,
                    Template = string.Empty,
                };

                // Act
                var actualResult = McpHelper.CreateVsCodeMcpServerEntryTemplate(metadataJson, TestPackageId, TestPackageVersion);

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
            [InlineData(McpServerData.ServerJsonEnvVarNameButNoValue, McpServerData.McpJsonEnvVarNameButNoValue)]
            public void ReturnsSuccess_WhenMetadataIsValid(string metadataJson, string vsCodeTemplateJson)
            {
                // Arrange
                var expectedResult = new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.Success,
                    Template = vsCodeTemplateJson,
                };

                // Act
                var actualResult = McpHelper.CreateVsCodeMcpServerEntryTemplate(metadataJson, TestPackageId, TestPackageVersion);

                // Assert
                Assert.True(actualResult == expectedResult);
            }
        }
    }
}
