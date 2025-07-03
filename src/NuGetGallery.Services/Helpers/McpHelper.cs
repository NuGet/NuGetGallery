// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
                packageTypes.Any(t => string.Equals(t.Name, NuGet.Packaging.Core.PackageType.DotnetTool.Name, StringComparison.OrdinalIgnoreCase)) &&
                packageTypes.Any(t => string.Equals(t.Name,McpServerPackageTypeName, StringComparison.OrdinalIgnoreCase));
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
                    Template = string.Empty,
                };
            }

            McpServerMetadata mcpServerMetadata;
            try
            {
                mcpServerMetadata = JsonConvert.DeserializeObject<McpServerMetadata>(
                    metadataJson,
                    new ArgumentConverter(),
                    new RuntimeArgumentConverter());
            }
            catch(JsonException)
            {
                return new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.InvalidMetadata,
                    Template = string.Empty,
                };
            }

            if (mcpServerMetadata.Packages == null)
            {
                return new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.MissingNugetRegistry,
                    Template = string.Empty,
                };
            }

            var nugetPackage = mcpServerMetadata.Packages.FirstOrDefault(p => p != null && p.RegistryName?.ToLowerInvariant() == "nuget");
            if (nugetPackage == null)
            {
                return new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.MissingNugetRegistry,
                    Template = string.Empty,
                };
            }

            var env = MapEnvVarsToEnv(nugetPackage.EnvironmentVariables);
            var envInputs = MapEnvVarsToInputs(nugetPackage.EnvironmentVariables);
            var argInputs = MapArgumentsToInputs(nugetPackage.PackageArguments, envInputs.Count + 1);
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
                var jsonSerializerSettings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented
                };
                var templateJson = JsonConvert.SerializeObject(entry, jsonSerializerSettings);

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
                    Template = string.Empty,
                };
            }
        }

        public static Dictionary<string, string> MapEnvVarsToEnv(List<EnvironmentVariable> envVars)
        {
            var env = new Dictionary<string, string>();

            if (envVars == null || envVars.Count == 0)
            {
                return env;
            }

            var inputId = 1;
            foreach (var envVar in envVars)
            {
                if (envVar == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(envVar.Name))
                {
                    continue;
                }

                env[envVar.Name] = $"${{input:input-{inputId++}}}";
            }

            return env;
        }

        public static List<VsCodeInput> MapEnvVarsToInputs(List<EnvironmentVariable> envVars)
        {
            var result = new List<VsCodeInput>();

            if (envVars == null || envVars.Count == 0)
            {
                return result;
            }

            int inputId = 1;
            foreach (var envVar in envVars)
            {
                if (envVar == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(envVar.Name))
                {
                    continue;
                }

                var type = "promptString";
                if (envVar.Choices != null && envVar.Choices.Count > 0)
                {
                    type = "pickString";
                }

                var isPassword = false;
                if (envVar.IsSecret.HasValue)
                {
                    isPassword = envVar.IsSecret.Value;
                }

                result.Add(new VsCodeInput
                {
                    Type = type,
                    Id = $"input-{inputId++}",
                    Description = envVar.Description,
                    Password = isPassword,
                    Default = envVar.Default,
                    Choices = envVar.Choices,
                });
            }

            return result;
        }

        public static List<VsCodeInput> MapArgumentsToInputs(List<Argument> arguments, int startId)
        {
            var result = new List<VsCodeInput>();

            if (arguments == null || arguments.Count == 0)
            {
                return result;
            }

            int inputId = startId;
            foreach (var arg in arguments)
            {
                if (arg == null || string.IsNullOrWhiteSpace(arg.Description))
                {
                    continue;
                }

                var type = "promptString";
                if (arg.Choices != null && arg.Choices.Count > 0)
                {
                    type = "pickString";
                }

                if (arg.Type == "positional")
                {
                    var positionalArg = arg as PositionalArgument;
                    result.Add(new VsCodeInput
                    {
                        Type = type,
                        Id = $"input-{inputId++}",
                        Description = positionalArg.Description,
                        Password = false,
                        Default = positionalArg.Default,
                        Choices = positionalArg.Choices,
                    });
                }
                else if (arg.Type == "named")
                {
                    var namedArg = arg as NamedArgument;
                    result.Add(new VsCodeInput
                    {
                        Type = type,
                        Id = $"input-{inputId++}",
                        Description = namedArg.Description,
                        Password = false,
                        Choices = namedArg.Choices,
                    });
                }
            }

            return result;
        }
    }
}
