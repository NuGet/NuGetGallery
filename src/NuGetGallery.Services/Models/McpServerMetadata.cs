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
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("version_detail")]
        public VersionDetail VersionDetail { get; set; }

        [JsonProperty("repository")]
        public Repository Repository { get; set; }

        [JsonProperty("packages")]
        public List<McpPackage> Packages { get; set; }

        [JsonProperty("remotes")]
        public List<McpRemote> Remotes { get; set; }
    }

    public class VersionDetail
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("release_date")]
        public DateTime? ReleaseDate { get; set; }

        [JsonProperty("is_latest")]
        public bool? IsLatest { get; set; }
    }

    public class Repository
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class McpPackage
    {
        [JsonProperty("registry_name")]
        public string RegistryName { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("runtime_hint")]
        public string RuntimeHint { get; set; }

        [JsonProperty("runtime_arguments")]
        public List<RuntimeArgument> RuntimeArguments { get; set; }

        [JsonProperty("package_arguments")]
        public List<Argument> PackageArguments { get; set; }

        [JsonProperty("environment_variables")]
        public List<EnvironmentVariable> EnvironmentVariables { get; set; }
    }

    public abstract class RuntimeArgument
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("is_required")]
        public bool? IsRequired { get; set; }

        [JsonProperty("is_repeated")]
        public bool? IsRepeated { get; set; }
    }

    public class RuntimePositionalArgument : RuntimeArgument
    {
        public RuntimePositionalArgument() { Type = "positional"; }

        [JsonProperty("value_hint")]
        public string ValueHint { get; set; }

        [JsonProperty("default")]
        public string Default { get; set; }
    }

    public class RuntimeNamedArgument : RuntimeArgument
    {
        public RuntimeNamedArgument() { Type = "named"; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("variables")]
        public Dictionary<string, RuntimeNamedArgumentVariable> Variables { get; set; }
    }

    public class RuntimeNamedArgumentVariable
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("is_required")]
        public bool? IsRequired { get; set; }

        [JsonProperty("default")]
        public string Default { get; set; }
    }

    public abstract class Argument
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("is_required")]
        public bool? IsRequired { get; set; }

        [JsonProperty("is_repeated")]
        public bool? IsRepeated { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("choices")]
        public List<string> Choices { get; set; }
    }

    public class PositionalArgument : Argument
    {
        public PositionalArgument() { Type = "positional"; }

        [JsonProperty("value_hint")]
        public string ValueHint { get; set; }

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

        [JsonProperty("is_required")]
        public bool? IsRequired { get; set; }

        [JsonProperty("is_secret")]
        public bool? IsSecret { get; set; }

        [JsonProperty("choices")]
        public List<string> Choices { get; set; }
    }

    public class McpRemote
    {
        [JsonProperty("transport_type")]
        public string TransportType { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("headers")]
        public List<McpRemoteHeader> Headers { get; set; }
    }

    public class McpRemoteHeader
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("is_required")]
        public bool? IsRequired { get; set; }

        [JsonProperty("is_secret")]
        public bool? IsSecret { get; set; }
    }

    // Custom converters for handling oneOf deserialization
    public class RuntimeArgumentConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(RuntimeArgument);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);
            var type = jsonObject["type"]?.Value<string>();
            if (type == "positional")
            {
                return jsonObject.ToObject<RuntimePositionalArgument>(serializer);
            }
            else if (type == "named")
            {
                return jsonObject.ToObject<RuntimeNamedArgument>(serializer);
            }

            throw new JsonSerializationException($"Unknown runtime argument type: {type}");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
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
