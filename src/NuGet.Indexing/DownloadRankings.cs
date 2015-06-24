// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NuGet.Indexing
{
    public static class DownloadRankings
    {
        public static IDictionary<string, int> Load(string name, ILoader loader)
        {
            try
            {
                using (JsonReader jsonReader = loader.GetReader(name))
                {
                    return CreateDictionary(jsonReader);
                }
            }
            catch (Exception e)
            {
                if (IndexingUtils.IsFatal(e))
                {
                    throw;
                }
                Trace.TraceInformation("Unable to load {0}. Exception Message : {1}", name, e.Message);
                return new Dictionary<string, int>();
            }
        }

        static void Read(JsonReader jsonReader, JsonToken expected)
        {
            jsonReader.Read();
            if (jsonReader.TokenType != expected)
            {
                throw new Exception(string.Format("expected {0}", expected));
            }
        }

        static IDictionary<string, int> CreateDictionary(JsonReader jsonReader)
        {
            Read(jsonReader, JsonToken.StartObject);
            Read(jsonReader, JsonToken.PropertyName);
            Read(jsonReader, JsonToken.StartArray);

            IDictionary<string, int> ranking = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int n = 1;

            while (jsonReader.Read() && jsonReader.TokenType == JsonToken.String)
            {
                ranking.Add(jsonReader.Value.ToString(), n++);
            }

            return ranking;
        }
    }
}
