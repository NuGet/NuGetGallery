// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGetGallery.Services.Models
{
    public class McpServerMetadata
    {
        [JsonProperty("packages")]
        public List<McpPackage> Packages { get; set; }
    }

    public class McpPackage
    {
        [JsonProperty("registry_name")]
        public string RegistryName { get; set; }

        [JsonProperty("package_arguments")]
        public List<Argument> PackageArguments { get; set; }

        [JsonProperty("environment_variables")]
        public List<EnvironmentVariable> EnvironmentVariables { get; set; }
    }

    public abstract class RuntimeArgument
    {
        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public abstract class Argument
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("choices")]
        public List<string> Choices { get; set; }
    }

    public class PositionalArgument : Argument
    {
        public PositionalArgument() { Type = "positional"; }

        [JsonProperty("default")]
        public string Default { get; set; }
    }

    public class NamedArgument : Argument
    {
        public NamedArgument() { Type = "named"; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class EnvironmentVariable
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("default")]
        public string Default { get; set; }

        [JsonProperty("is_secret")]
        public bool? IsSecret { get; set; }

        [JsonProperty("choices")]
        public List<string> Choices { get; set; }
    }

    public class ArgumentConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(Argument);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);
            var type = jsonObject["type"]?.Value<string>();
            if (type == "positional")
            {
                return jsonObject.ToObject<PositionalArgument>(serializer);
            }
            else if (type == "named")
            {
                return jsonObject.ToObject<NamedArgument>(serializer);
            }

            throw new JsonSerializationException($"Unknown argument type: {type}");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
