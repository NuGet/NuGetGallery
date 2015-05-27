// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public abstract class DownloadLookup
    {
        public static readonly string FileName = "downloads.v1.json";
        public abstract string Path { get; }
        protected abstract JsonReader GetReader();

        public IDictionary<string, IDictionary<string, int>> Load()
        {
            IDictionary<string, IDictionary<string, int>> result = new Dictionary<string, IDictionary<string, int>>();

            using (JsonReader jsonReader = GetReader())
            {
                jsonReader.Read();

                while (jsonReader.Read())
                {
                    if (jsonReader.TokenType == JsonToken.StartObject)
                    {
                        JToken record = JToken.ReadFrom(jsonReader);

                        string id = record["Id"].ToString().ToLowerInvariant();
                        string version = record["Version"].ToString();
                        int downloads = record["Downloads"].ToObject<int>();

                        IDictionary<string, int> versions;
                        if (!result.TryGetValue(id, out versions))
                        {
                            versions = new Dictionary<string, int>();
                            result.Add(id, versions);
                        }

                        versions.Add(version, downloads);
                    }
                }
            }

            return result;
        }
    }
}