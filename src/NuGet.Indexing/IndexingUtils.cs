// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Indexing
{
    public static class IndexingUtils
    {
        public static void Load(string name, ILoader loader, FrameworkLogger logger, IDictionary<string, HashSet<string>> targetDictionary)
        {
            try
            {
                using (var jsonReader = loader.GetReader(name))
                {
                    UpdateDictionary(jsonReader, targetDictionary);
                }
            }
            catch (Exception e)
            {
                if (IsFatal(e))
                {
                    throw;
                }

                logger.LogError($"Unable to load {name}.", e);
            }
        }

        public static void UpdateDictionary(JsonReader jsonReader, IDictionary<string, HashSet<string>> targetDictionary)
        {
            jsonReader.Read();

            while (jsonReader.Read())
            {
                if (jsonReader.TokenType == JsonToken.StartArray)
                {
                    var record = (JArray)JToken.ReadFrom(jsonReader);
                    var id = String.Intern(record[0].ToString());
                    var data = new HashSet<string>(record[1].Select(t => String.Intern(t.ToString())), StringComparer.OrdinalIgnoreCase);
                    targetDictionary[id] = data;
                }
            }
        }

        public static bool IsFatal(Exception e)
        {
            return e is StackOverflowException || e is OutOfMemoryException || e is Win32Exception;
        }
    }
}
