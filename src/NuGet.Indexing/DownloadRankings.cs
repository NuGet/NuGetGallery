// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Indexing
{
    public static class DownloadRankings
    {
        public static IReadOnlyDictionary<string, int> Load(string name, ILoader loader, FrameworkLogger logger)
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

                logger.LogInformation("Unable to load {0}. Exception Message : {1}", name, e.Message);

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

        static IReadOnlyDictionary<string, int> CreateDictionary(JsonReader jsonReader)
        {
            Read(jsonReader, JsonToken.StartObject);
            Read(jsonReader, JsonToken.PropertyName);
            Read(jsonReader, JsonToken.StartArray);

            var ranking = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int n = 1;

            while (jsonReader.Read() && jsonReader.TokenType == JsonToken.String)
            {
                ranking.Add(String.Intern(jsonReader.Value.ToString()), n++);
            }

            return ranking;
        }
    }
}
