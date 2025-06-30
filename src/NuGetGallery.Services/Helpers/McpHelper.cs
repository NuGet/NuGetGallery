// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;

namespace NuGetGallery.Services.Helpers
{
    public static class McpHelper
    {
        public const string McpServerPackageTypeName = "McpServer";
        public const string McpServerMetadataFilePath = @".mcp/server.json";

        public static bool IsMcpServerPackage(PackageArchiveReader packageArchive)
        {
            // Should we add McpServer to Nuget.Client PackageType?
            // https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/Core/PackageType.cs
            var packageTypes = packageArchive.GetPackageTypes();

            // The package must be of type McpServer and DotnetTool
            return
                packageTypes.Contains(NuGet.Packaging.Core.PackageType.DotnetTool) &&
                packageTypes.Any(t => t.Name == McpServerPackageTypeName);
        }

        public static bool PackageContainsMcpServerMetadata(PackageArchiveReader packageArchive)
        {
            var fileList = new HashSet<string>(packageArchive.GetFiles());
            return fileList.Contains(McpServerMetadataFilePath);
        }

        public static string ReadMcpServerMetadata(PackageArchiveReader packageArchive)
        {
            using var stream = packageArchive.GetStream(McpServerMetadataFilePath);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static async Task<string> ReadMcpServerMetadataAsync(PackageArchiveReader packageArchive)
        {
            using var stream = await packageArchive.GetStreamAsync(McpServerMetadataFilePath, CancellationToken.None);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
    }
}
