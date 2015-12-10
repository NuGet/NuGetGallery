// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Indexing
{
    public static class IndexingUtils
    {
        public static IDictionary<string, HashSet<string>> Load(string name, ILoader loader, FrameworkLogger logger)
        {
            try
            {
                using (var jsonReader = loader.GetReader(name))
                {
                    return CreateDictionary(jsonReader);
                }
            }
            catch (Exception e)
            {
                if (IsFatal(e))
                {
                    throw;
                }

                logger.LogError($"Unable to load {name}.", e);
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
                    var record = (JArray)JToken.ReadFrom(jsonReader);
                    var id = record[0].ToString();
                    var data = new HashSet<string>(record[1].Select(t => t.ToString()), StringComparer.OrdinalIgnoreCase);
                    result[id] = data;
                }
            }
            return result;
        }

        public static bool IsFatal(Exception e)
        {
            return e is StackOverflowException || e is OutOfMemoryException || e is Win32Exception;
        }
    }
}
