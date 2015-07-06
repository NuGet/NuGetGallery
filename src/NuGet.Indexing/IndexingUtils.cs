// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace NuGet.Indexing
{
    public static class IndexingUtils
    {
        public static IDictionary<string, HashSet<string>> Load(string name, ILoader loader)
        {
            try
            {
                using (JsonReader jsonReader = loader.GetReader(name))
                {
                    return IndexingUtils.CreateDictionary(jsonReader);
                }
            }
            catch (Exception e)
            {
                if (IndexingUtils.IsFatal(e))
                {
                    throw;
                }
                Trace.TraceInformation("Unable to load {0}. Exception Message : {1}", name, e.Message);
                return new Dictionary<string, HashSet<string>>();
            }
        }

        public static IDictionary<string, HashSet<string>> CreateDictionary(JsonReader jsonReader)
        {
            var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            jsonReader.Read();
            while (jsonReader.Read())
            {
                if (jsonReader.TokenType == JsonToken.StartArray)
                {
                    JArray record = (JArray)JToken.ReadFrom(jsonReader);
                    string id = record[0].ToString();
                    HashSet<string> data = new HashSet<string>(record[1].Select(t => t.ToString()));
                    result[id] = data;
                }
            }
            return result;
        }

        public static bool IsFatal(Exception e)
        {
            return (e is StackOverflowException) || (e is OutOfMemoryException) || (e is Win32Exception);
        }
    }
}
