// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


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
            // The data in downloads.v1.json will be an array of Package records - which has Id, Array of Versions and download count.
            // Sample.json : [["AutofacContrib.NSubstitute",["2.4.3.700",406],["2.5.0",137]],["Assman.Core",["2.0.7",138]]....
            using (JsonReader jsonReader = GetReader())
            {
                try
                {
                    jsonReader.Read();

                    while (jsonReader.Read())
                    {
                        try
                        {
                            if (jsonReader.TokenType == JsonToken.StartArray)
                            {
                                JToken record = JToken.ReadFrom(jsonReader);
                                string id = record[0].ToString().ToLowerInvariant();
                                // The second entry in each record should be an array of versions, if not move on to next entry.
                                // This is a check to safe guard against invalid entries.
                                if (record.Count() == 2 && record[1].Type != JTokenType.Array)
                                {
                                    continue;
                                }
                                IDictionary<string, int> versions = new Dictionary<string, int>();
                                foreach (JToken token in record)
                                {
                                    if (token != null && token.Count() == 2)
                                    {
                                        string version = token[0].ToString().ToLowerInvariant();
                                        // Check for duplicate versions before adding.
                                        if (!versions.ContainsKey(version))
                                        {
                                            versions.Add(version, token[1].ToObject<int>());
                                        }
                                    }
                                }
                                //Check for duplicate Ids before adding to dict.
                                if (!result.ContainsKey(id))
                                {
                                    result.Add(id, versions);
                                }

                            }
                        }
                        catch (JsonReaderException ex)
                        {
                            Trace.TraceInformation("Invalid entry found in downloads.v1.json. Exception Message : {0}", ex.Message);
                        }
                    }
                }
                catch (JsonReaderException ex)
                {
                    Trace.TraceError("Data present in downloads.v1.json is invalid. Couldn't get download data. Exception Message : {0}", ex.Message);
                }
            }
            return result;
        }
    }
}