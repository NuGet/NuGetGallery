// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Packaging;
using NuGetGallery.Services.Models;

namespace NuGetGallery.Services.Helpers
{
    public static class McpHelper
    {
        public const string McpServerPackageTypeName = "McpServer";
        public const string McpServerMetadataFilePath = @".mcp/server.json";
        public const string McpServerPackageIdPlaceholder = "%%PACKAGE_ID_PLACEHOLDER%%";

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

        public static McpServerEntryTemplateResult CreateVsCodeMcpServerEntryTemplate(string metadataJson)
        {
            if (string.IsNullOrWhiteSpace(metadataJson))
            {
                return new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.MissingMetadata,
                    Template = null,
                };
            }

            McpServerMetadata mcpServerMetadata;
            try
            {
                mcpServerMetadata = JsonConvert.DeserializeObject<McpServerMetadata>(metadataJson);
            }
            catch(JsonException)
            {
                return new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.InvalidMetadata,
                    Template = null,
                };
            }

            var nugetPackage = mcpServerMetadata.Packages.FirstOrDefault(p => p.RegistryName?.ToLowerInvariant() == "nuget");
            if (nugetPackage == null)
            {
                return new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.MissingMetadata,
                    Template = null,
                };
            }

            var env = MapEnvVarsToEnv(nugetPackage.EnvironmentVariables);
            var envInputs = MapEnvVarsToInputs(nugetPackage.EnvironmentVariables);
            var argInputs = MapArgumentsToInputs(nugetPackage.PackageArguments, envInputs.Count);
            var inputs = envInputs.Concat(argInputs).ToList();

            var server = new VsCodeServer
            {
                Type = "stdio",
                Command = "dnx",
                Args = [McpServerPackageIdPlaceholder, "--", "mcp", "start"],
                Env = env,
            };

            var entry = new VsCodeMcpServerEntry
            {
                Inputs = inputs,
                Servers = new Dictionary<string, VsCodeServer>
                {
                    { McpServerPackageIdPlaceholder, server },
                }
            };

            try
            {
                var templateJson = JsonConvert.SerializeObject(entry, Formatting.Indented);
                return new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.Success,
                    Template = templateJson,
                };
            }
            catch (JsonException)
            {
                return new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.InvalidMetadata,
                    Template = null,
                };
            }
        }

        public static Dictionary<string, string> MapEnvVarsToEnv(List<EnvironmentVariable> envVars)
        {
            var env = new Dictionary<string, string>();

            var inputId = 0;
            foreach (var envVar in envVars)
            {
                env[envVar.Name] = $"${{input:input-{inputId++}}}";
            }

            return env;
        }

        public static List<VsCodeInput> MapEnvVarsToInputs(List<EnvironmentVariable> envVars)
        {
            var result = new List<VsCodeInput>();

            int inputId = 0;
            foreach (var envVar in envVars)
            {
                result.Add(new VsCodeInput
                {
                    Type = "promptString",
                    Id = $"input-{inputId++}",
                    Description = envVar.Description,
                    Password = true,
                });
            }

            return result;
        }

        public static List<VsCodeInput> MapArgumentsToInputs(List<PackageArgument> arguments, int startId)
        {
            var result = new List<VsCodeInput>();

            int inputId = startId;
            foreach (var arg in arguments)
            {
                result.Add(new VsCodeInput
                {
                    Type = "promptString",
                    Id = $"input-{inputId++}",
                    Description = arg.Description,
                    Password = false,
                });
            }

            return result;
        }
    }
}
