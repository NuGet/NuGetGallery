// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGetGallery.Services.Models
{
    public class McpServerMetadata
    {
        [JsonProperty("packages")]
        public IReadOnlyList<McpPackage>? Packages { get; set; }
    }

    public class McpPackage
    {
        [JsonProperty("registry_name", Required = Required.Always)]
        public required string RegistryName { get; set; }

        [JsonProperty("runtime_arguments")]
        [JsonConverter(typeof(ArgumentListConverter))]
        public IReadOnlyList<VariableInput>? RuntimeArguments { get; set; }

        [JsonProperty("package_arguments")]
        [JsonConverter(typeof(ArgumentListConverter))]
        public IReadOnlyList<VariableInput>? PackageArguments { get; set; }

        [JsonProperty("environment_variables")]
        public IReadOnlyList<KeyValueInput>? EnvironmentVariables { get; set; }
    }

    public class Input
    {
        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("value")]
        public string? Value { get; set; }

        [JsonProperty("is_secret")]
        public bool? IsSecret { get; set; }

        [JsonProperty("default")]
        public string? Default { get; set; }

        [JsonProperty("choices")]
        public IReadOnlyList<string>? Choices { get; set; }
    }

    public class VariableInput : Input
    {
        [JsonProperty("variables")]
        public IReadOnlyDictionary<string, Input>? Variables { get; set; }
    }

    public class PositionalInput : VariableInput
    {
        [JsonProperty("type", Required = Required.Always)]
        public required string Type { get; set; }

        [JsonProperty("value_hint", Required = Required.Always)]
        public required string ValueHint { get; set; }
    }

    public class NamedInput : VariableInput
    {
        [JsonProperty("type", Required = Required.Always)]
        public required string Type { get; set; }

        [JsonProperty("name", Required = Required.Always)]
        public required string Name { get; set; }
    }

    public class KeyValueInput : VariableInput
    {
        [JsonProperty("name", Required = Required.Always)]
        public required string Name { get; set; }

        [JsonProperty("value")]
        public new string? Value { get; set; }
    }

    public class ArgumentListConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) =>
            objectType == typeof(IReadOnlyList<VariableInput>) || objectType == typeof(List<VariableInput>);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var array = JArray.Load(reader);
            var items = new List<VariableInput>();

            foreach (var token in array)
            {
                if (token.Type != JTokenType.Object)
                {
                    throw new JsonSerializationException();
                }

                var type = token["type"]?.Value<string>();
                VariableInput? input = null;

                if (type == "positional")
                    input = token.ToObject<PositionalInput>(serializer);
                else if (type == "named")
                    input = token.ToObject<NamedInput>(serializer);
                else
                    throw new JsonSerializationException($"Unknown input type: {type}");

                items.Add(input!);
            }
            return items;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
