// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public static class DownloadsV1Reader
    {
        public static void Load(JsonReader jsonReader, Action<string, string, long> addCount)
        {
            // The data in downloads.v1.json will be an array of Package records - which has Id, Array of Versions and download count.
            // Sample.json : [["AutofacContrib.NSubstitute",["2.4.3.700",406],["2.5.0",137]],["Assman.Core",["2.0.7",138]]....
            jsonReader.Read();

            while (jsonReader.Read())
            {
                if (jsonReader.TokenType == JsonToken.StartArray)
                {
                    JToken record = JToken.ReadFrom(jsonReader);

                    // The second entry in each record should be an array of versions, if not move on to next entry.
                    // This is a check to safe guard against invalid entries.
                    if (record.Count() == 2 && record[1].Type != JTokenType.Array)
                    {
                        continue;
                    }

                    var id = record[0].ToString();

                    foreach (JToken token in record)
                    {
                        if (token != null && token.Count() == 2)
                        {
                            var version = token[0].ToString();

                            var count = token[1].ToObject<long>();

                            addCount.Invoke(id, version, count);
                        }
                    }
                }
            }
        }
    }
}