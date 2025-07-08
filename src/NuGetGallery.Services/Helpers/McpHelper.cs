// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        public const int McpServerMetadataMaxLength = 20_000;

        public static bool IsMcpServerPackage(PackageArchiveReader packageArchive)
        {
            var packageTypes = packageArchive.GetPackageTypes();

            // The package must be of type McpServer and DotnetTool
            return
                packageTypes.Any(t => string.Equals(t.Name, NuGet.Packaging.Core.PackageType.DotnetTool.Name, StringComparison.OrdinalIgnoreCase)) &&
                packageTypes.Any(t => string.Equals(t.Name,McpServerPackageTypeName, StringComparison.OrdinalIgnoreCase));
        }

        public static bool PackageContainsMcpServerMetadata(PackageArchiveReader packageArchive)
        {
            return packageArchive.GetFiles()
                .Select(f => f.ToLowerInvariant())
                .Contains(McpServerMetadataFilePath);
        }

        public static string ReadMcpServerMetadata(PackageArchiveReader packageArchive)
        {
            using var stream = packageArchive.GetStream(McpServerMetadataFilePath);
            using var truncatedStream = StreamHelper.GetTruncatedStreamWithMaxSize(stream, McpServerMetadataMaxLength + 1);

            return Encoding.UTF8.GetString(truncatedStream.Stream.GetBuffer(), 0, (int)truncatedStream.Stream.Length);
        }

        public static async Task<string> ReadMcpServerMetadataAsync(PackageArchiveReader packageArchive)
        {
            using var stream = packageArchive.GetStream(McpServerMetadataFilePath);
            using var truncatedStream = await StreamHelper.GetTruncatedStreamWithMaxSizeAsync(stream, McpServerMetadataMaxLength + 1);

            return Encoding.UTF8.GetString(truncatedStream.Stream.GetBuffer(), 0, (int)truncatedStream.Stream.Length);
        }

        public static McpServerEntryTemplateResult CreateVsCodeMcpServerEntryTemplate(string metadataJson, string packageId, string packageVersion)
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
                    new ArgumentConverter());
            }
            catch(JsonException)
            {
                return new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.InvalidMetadata,
                    Template = string.Empty,
                };
            }

            var nugetPackage = mcpServerMetadata.Packages?.FirstOrDefault(p => p != null && p.RegistryName?.ToLowerInvariant() == "nuget");
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

            var argArgs = MapArgumentsToArgs(nugetPackage.PackageArguments, envInputs.Count + 1);
            var args = new List<string> { packageId, "--version", packageVersion, "--yes" };

            if (argArgs.Count > 0)
            {
                args.Add("--");
                args.AddRange(argArgs);
            }

            var server = new VsCodeServer
            {
                Type = "stdio",
                Command = "dnx",
                Args = args,
                Env = env,
            };

            var entry = new VsCodeMcpServerEntry
            {
                Inputs = inputs,
                Servers = new Dictionary<string, VsCodeServer>
                {
                    { packageId, server },
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

                result.Add(new VsCodeInput
                {
                    Type = type,
                    Id = $"input-{inputId++}",
                    Description = envVar.Description,
                    Password = envVar.IsSecret ?? false,
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

                if (arg.Type == "named")
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
            }

            return result;
        }

        public static List<string> MapArgumentsToArgs(List<Argument> arguments, int startId)
        {
            var result = new List<string>();

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

                if (arg.Type == "named")
                {
                    var namedArg = arg as NamedArgument;

                    if (string.IsNullOrWhiteSpace(namedArg.Name))
                    {
                        continue;
                    }

                    var name = namedArg.Name;
                    if (!name.StartsWith("--", StringComparison.Ordinal))
                    {
                        name = "--" + name;
                    }

                    result.Add($"{namedArg.Name}");
                    result.Add($"${{input:input-{inputId++}}}");
                }
            }

            foreach (var arg in arguments)
            {
                if (arg == null || string.IsNullOrWhiteSpace(arg.Description))
                {
                    continue;
                }

                if (arg.Type == "positional")
                {
                    var positionalArg = arg as PositionalArgument;
                    result.Add($"${{input:input-{inputId++}}}");
                }
            }

            return result;
        }
    }
}
