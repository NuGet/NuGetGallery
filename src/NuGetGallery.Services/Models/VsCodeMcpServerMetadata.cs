// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGetGallery.Services.Models
{
    public class VsCodeMcpServerEntry
    {
        [JsonProperty("inputs")]
        public List<VsCodeInput> Inputs { get; set; }

        [JsonProperty("servers")]
        public Dictionary<string, VsCodeServer> Servers { get; set; }
    }

    public class VsCodeInput
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("password")]
        public bool? Password { get; set; }

        [JsonProperty("default")]
        public string Default { get; set; }

        [JsonProperty("options")]
        [JsonConverter(typeof(InlineStringListConverter))]
        public List<string> Options { get; set; }
    }

    public class VsCodeServer
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("command")]
        public string Command { get; set; }

        [JsonProperty("args")]
        [JsonConverter(typeof(InlineStringListConverter))]
        public List<string> Args { get; set; }

        [JsonProperty("env")]
        public Dictionary<string, string> Env { get; set; }
    }

    internal class InlineStringListConverter : JsonConverter<List<string>>
    {
        public override void WriteJson(JsonWriter writer, List<string> value, JsonSerializer serializer)
        {
            var arrayString = "[" + string.Join(", ", value.ConvertAll(s => JsonConvert.ToString(s))) + "]";
            writer.WriteRawValue(arrayString);
        }

        public override List<string> ReadJson(JsonReader reader, Type objectType, List<string> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return JArray.Load(reader).ToObject<List<string>>();
        }
    }
}
