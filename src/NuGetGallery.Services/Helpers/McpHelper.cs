// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

            var nugetRegistryPackage = mcpServerMetadata.Packages?.FirstOrDefault(p => p != null && StringComparer.OrdinalIgnoreCase.Equals(p.RegistryType, "nuget"));
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

            if (registryPackage.RuntimeArguments?.Count > 0)
            {
                var result = ProcessArguments(registryPackage.RuntimeArguments);
                inputs.AddRange(result.Inputs);
                args.AddRange(result.Args);
            }

            if (registryPackage.EnvironmentVariables?.Count > 0)
            {
                var result = ProcessKeyValueInputs(registryPackage.EnvironmentVariables);
                inputs.AddRange(result.Inputs);
                foreach (var kvp in result.Env)
                {
                    env[kvp.Key] = kvp.Value;
                }
            }

            args.Add($"{packageId}@{packageVersion}");
            args.Add("--yes");

            if (registryPackage.PackageArguments?.Count > 0)
            {
                var result = ProcessArguments(registryPackage.PackageArguments);
                inputs.AddRange(result.Inputs);

                if (result.Args.Count > 0)
                {
                    args.Add("--");
                    args.AddRange(result.Args);
                }
            }

            var server = new VsCodeServer
            {
                Type = "stdio",
                Command = "dnx",
                Args = args,
                Env = env.Count > 0 ? env : null,
            };

            return new VsCodeMcpServerEntry
            {
                Inputs = inputs.Count > 0 ? inputs : null,
                Servers = new Dictionary<string, VsCodeServer>
                {
                    { packageId, server },
                }
            };
        }

        private static ProcessArgumentsResult ProcessArguments(IReadOnlyList<InputWithVariables> arguments)
        {
            var inputs = new List<VsCodeInput>();
            var args = new List<string>();

            foreach (var arg in arguments)
            {
                var variables = GetVariables(arg.Variables);

                if (arg is PositionalArgument positionalArg)
                {
                    var value = positionalArg.Value;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        foreach (var variable in variables)
                        {
                            value = value.Replace($"{{{variable.Id}}}", $"${{input:{variable.Id}}}");
                        }

                        args.Add(value);

                        if (variables.Count > 0)
                        {
                            inputs.AddRange(variables);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(positionalArg.ValueHint) && (!string.IsNullOrWhiteSpace(positionalArg.Description) || positionalArg.Choices?.Count > 0 || !string.IsNullOrWhiteSpace(positionalArg.Default)))
                    {
                        args.Add($"${{input:{positionalArg.ValueHint}}}");

                        inputs.Add(new VsCodeInput
                        {
                            Id = positionalArg.ValueHint,
                            Type = "promptString",
                            Description = positionalArg.Description ?? string.Empty,
                            Default = positionalArg.Default,
                            Options = positionalArg.Choices?.ToList(),
                        });
                    }
                    else if (!string.IsNullOrWhiteSpace(positionalArg.ValueHint))
                    {
                        args.Add(positionalArg.ValueHint);
                    }
                }
                else if (arg is NamedArgument namedArg)
                {
                    if (string.IsNullOrWhiteSpace(namedArg.Name))
                    {
                        continue;
                    }

                    args.Add(namedArg.Name);

                    if (!string.IsNullOrWhiteSpace(namedArg.Value))
                    {
                        var value = namedArg.Value;
                        foreach (var variable in variables)
                        {
                            value = value.Replace($"{{{variable.Id}}}", $"${{input:{variable.Id}}}");
                        }

                        args.Add(value);

                        if (variables.Count > 0)
                        {
                            inputs.AddRange(variables);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(namedArg.Description) || namedArg.Choices?.Count > 0 || !string.IsNullOrWhiteSpace(namedArg.Default))
                    {
                        var inputId = namedArg.Name.TrimStart('-');

                        args.Add($"${{input:{inputId}}}");

                        inputs.Add(new VsCodeInput
                        {
                            Id = inputId,
                            Type = "promptString",
                            Description = namedArg.Description ?? string.Empty,
                            Default = namedArg.Default,
                            Options = namedArg.Choices?.ToList(),
                        });
                    }
                }
            }

            return new ProcessArgumentsResult
            {
                Inputs = inputs,
                Args = args,
            };
        }

        private static ProcessKeyValueInputsResult ProcessKeyValueInputs(IReadOnlyList<KeyValueInput> keyValueInputs)
        {
            var inputs = new List<VsCodeInput>();
            var env = new Dictionary<string, string>();

            foreach (var input in keyValueInputs)
            {
                if (input == null || string.IsNullOrWhiteSpace(input.Name))
                {
                    continue;
                }

                var value = input.Value ?? string.Empty;

                var variables = GetVariables(input.Variables);
                if (variables.Count > 0)
                {
                    foreach (var variable in variables)
                    {
                        value = value.Replace($"{{{variable.Id}}}", $"${{input:{variable.Id}}}");
                    }

                    // We shouldn't add inputs that aren't referenced. Divergence from VS Code implementation.
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        inputs.AddRange(variables);
                    }
                }
                else if (string.IsNullOrWhiteSpace(input.Value) && (!string.IsNullOrWhiteSpace(input.Description) || input.Choices?.Count > 0 || !string.IsNullOrWhiteSpace(input.Default)))
                {
                    value = $"${{input:{input.Name}}}";

                    inputs.Add(new VsCodeInput
                    {
                        Id = input.Name,
                        Type = input.Choices?.Count > 0 ? "pickString" : "promptString",
                        Description = input.Description ?? string.Empty,
                        Password = input?.IsSecret == true ? true : null,
                        Default = input.Default,
                        Options = input.Choices?.ToList(),
                    });
                }

                env[input.Name] = value ?? string.Empty;
            }

            return new ProcessKeyValueInputsResult
            {
                Inputs = inputs,
                Env = env,
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
                    Description = item.Value.Description ?? string.Empty,
                    Password = item.Value.IsSecret,
                    Default = item.Value.Default,
                    Options = item.Value.Choices?.ToList(),
                });
            }

            return result;
        }

        private class ProcessArgumentsResult
        {
            public required List<VsCodeInput> Inputs { get; set; }

            public required List<string> Args { get; set; }
        }

        private class ProcessKeyValueInputsResult
        {
            public required List<VsCodeInput> Inputs { get; set; }

            public required Dictionary<string, string> Env { get; set; }
        }
    }
}
