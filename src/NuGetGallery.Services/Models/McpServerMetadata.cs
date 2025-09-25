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
        [JsonProperty("registryType", Required = Required.Always)]
        public required string RegistryType { get; set; }

        [JsonProperty("runtimeArguments")]
        [JsonConverter(typeof(ArgumentListConverter))]
        public IReadOnlyList<InputWithVariables>? RuntimeArguments { get; set; }

        [JsonProperty("packageArguments")]
        [JsonConverter(typeof(ArgumentListConverter))]
        public IReadOnlyList<InputWithVariables>? PackageArguments { get; set; }

        [JsonProperty("environmentVariables")]
        public IReadOnlyList<KeyValueInput>? EnvironmentVariables { get; set; }
    }

    public class Input
    {
        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("value")]
        public string? Value { get; set; }

        [JsonProperty("isSecret")]
        public bool? IsSecret { get; set; }

        [JsonProperty("default")]
        public string? Default { get; set; }

        [JsonProperty("choices")]
        public IReadOnlyList<string>? Choices { get; set; }
    }

    public class InputWithVariables : Input
    {
        [JsonProperty("variables")]
        public IReadOnlyDictionary<string, Input>? Variables { get; set; }
    }

    public class PositionalArgument : InputWithVariables
    {
        [JsonProperty("type", Required = Required.Always)]
        public required string Type { get; set; }

        [JsonProperty("valueHint", Required = Required.Always)]
        public required string ValueHint { get; set; }
    }

    public class NamedArgument : InputWithVariables
    {
        [JsonProperty("type", Required = Required.Always)]
        public required string Type { get; set; }

        [JsonProperty("name", Required = Required.Always)]
        public required string Name { get; set; }
    }

    public class KeyValueInput : InputWithVariables
    {
        [JsonProperty("name", Required = Required.Always)]
        public required string Name { get; set; }

        [JsonProperty("value")]
        public new string? Value { get; set; }
    }

    public class ArgumentListConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) =>
            objectType == typeof(IReadOnlyList<InputWithVariables>) || objectType == typeof(List<InputWithVariables>);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var array = JArray.Load(reader);
            var items = new List<InputWithVariables>();

            foreach (var token in array)
            {
                if (token.Type != JTokenType.Object)
                {
                    throw new JsonSerializationException();
                }

                var type = token["type"]?.Value<string>();
                InputWithVariables? input = null;

                if (type == "positional")
                {
                    input = token.ToObject<PositionalArgument>(serializer);
                }
                else if (type == "named")
                {
                    input = token.ToObject<NamedArgument>(serializer);
                }
                else
                {
                    throw new JsonSerializationException($"Unknown input type: {type}");
                }

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
