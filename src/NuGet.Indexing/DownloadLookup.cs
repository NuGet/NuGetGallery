// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

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
                    if (jsonReader.TokenType == JsonToken.StartArray)
                    {
                        JToken record = JToken.ReadFrom(jsonReader);
                        string id = record[0].ToString().ToLowerInvariant();
                        IDictionary<string, int> versions = new Dictionary<string, int>();
                        foreach (JToken token in record)
                        {
                            if (token.Count() == 2)
                                versions.Add(token[0].ToString().ToLowerInvariant(), token[1].ToObject<int>());
                        }
                        result.Add(id, versions);
                    }
                }
            }

            return result;
        }
    }
}