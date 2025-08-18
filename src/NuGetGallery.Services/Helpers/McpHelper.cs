// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                mcpServerMetadata = JsonConvert.DeserializeObject<McpServerMetadata>(metadataJson);
            }
            catch(JsonException)
            {
                return new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.InvalidMetadata,
                    Template = string.Empty,
                };
            }

            var nugetRegistryPackage = mcpServerMetadata.Packages?.FirstOrDefault(p => p != null && StringComparer.OrdinalIgnoreCase.Equals(p.RegistryName, "nuget"));
            if (nugetRegistryPackage == null)
            {
                return new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.MissingNugetRegistry,
                    Template = string.Empty,
                };
            }

            VsCodeMcpServerEntry entry;
            try
            {
                entry = RegistryToVsCodeServerEntry(nugetRegistryPackage, packageId, packageVersion);
            }
            catch (Exception)
            {
                return new McpServerEntryTemplateResult
                {
                    Validity = McpServerEntryResultValidity.InvalidMetadata,
                    Template = string.Empty,
                };
            }

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

        private static VsCodeMcpServerEntry RegistryToVsCodeServerEntry(McpPackage registryPackage, string packageId, string packageVersion)
        {
            var inputs = new List<VsCodeInput>();
            var args = new List<string>();
            var env = new Dictionary<string, string>();

            foreach (var arg in registryPackage.RuntimeArguments ?? [])
            {
                if (arg == null)
                {
                    continue;
                }

                var variables = GetVariables(arg.Variables);

                if (arg is PositionalInput positionalArg)
                {
                    var value = positionalArg.Value;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        foreach (var variable in variables)
                        {
                            value = value.Replace($"{{{variable.Id}}}", $"{{input:{variable.Id}}}");
                        }
                    }
                    else
                    {
                        value = positionalArg.ValueHint;
                    }

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    args.Add(value);
                }
                else if (arg is NamedInput namedArg)
                {
                    if (string.IsNullOrWhiteSpace(namedArg.Name) || string.IsNullOrWhiteSpace(namedArg.Value))
                    {
                        continue;
                    }

                    args.Add(namedArg.Name);

                    var value = namedArg.Value;
                    foreach (var variable in variables)
                    {
                        value = value.Replace($"{{{variable.Id}}}", $"{{input:{variable.Id}}}");
                    }

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    args.Add(value);
                }

                if (variables.Count > 0)
                {
                    inputs.AddRange(variables);
                }
            }

            foreach (var envVar in  registryPackage.EnvironmentVariables ?? [])
            {
                if (envVar == null)
                {
                    continue;
                }

                var variables = GetVariables(envVar.Variables);

                var value = envVar.Value;
                foreach (var variable in variables)
                {
                    value = value.Replace($"{{{variable.Id}}}", $"${{input:{variable.Id}}}");
                }

                var name = envVar.Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                env[name] = value ?? string.Empty;

                if (variables.Count > 0)
                {
                    inputs.AddRange(variables);
                }
            }

            args.Add($"{packageId}@{packageVersion}");
            args.Add("--yes");

            var packageArgs = new List<string>();
            foreach (var arg in registryPackage.PackageArguments ?? [])
            {
                if (arg == null)
                {
                    continue;
                }

                var variables = GetVariables(arg.Variables);

                if (arg is PositionalInput positionalArg)
                {
                    var value = positionalArg.Value;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        foreach (var variable in variables)
                        {
                            value = value.Replace($"{{{variable.Id}}}", $"${{input:{variable.Id}}}");
                        }
                    }
                    else
                    {
                        value = positionalArg.ValueHint;
                    }

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    packageArgs.Add(value);
                }
                else if (arg is NamedInput namedArg)
                {
                    if (string.IsNullOrWhiteSpace(namedArg.Name) || string.IsNullOrWhiteSpace(namedArg.Value))
                    {
                        continue;
                    }

                    packageArgs.Add(namedArg.Name);

                    var value = namedArg.Value;
                    foreach (var variable in variables)
                    {
                        value = value.Replace($"{{{variable.Id}}}", $"${{input:{variable.Id}}}");
                    }

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    packageArgs.Add(value);
                }

                if (variables.Count > 0)
                {
                    inputs.AddRange(variables);
                }
            }

            if (packageArgs.Count > 0)
            {
                args.Add("--");
                args.AddRange(packageArgs);
            }

            var server = new VsCodeServer
            {
                Type = "stdio",
                Command = "dnx",
                Args = args,
                Env = env,
            };

            return new VsCodeMcpServerEntry
            {
                Inputs = inputs,
                Servers = new Dictionary<string, VsCodeServer>
                {
                    { packageId, server },
                }
            };
        }

        private static List<VsCodeInput> GetVariables(IReadOnlyDictionary<string, Input> variables)
        {
            var result = new List<VsCodeInput>();

            if (variables == null || variables.Count == 0)
            {
                return result;
            }

            foreach (var item in variables)
            {
                if (item.Value == null)
                {
                    throw new Exception("Variable input cannot be null.");
                }

                result.Add(new VsCodeInput
                {
                    Id = item.Key,
                    Type = item.Value.Choices?.Count > 0 ? "pickString" : "promptString",
                    Description = item.Value.Description,
                    Password = item.Value.IsSecret,
                    Default = item.Value.Default,
                    Options = item.Value.Choices?.ToList(),
                });
            }

            return result;
        }
    }
}
