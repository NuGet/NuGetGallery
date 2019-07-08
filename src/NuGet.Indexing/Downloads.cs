// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Indexing
{
    public class Downloads
    {
        private IDictionary<string, DownloadsByVersion> _downloads =
            new Dictionary<string, DownloadsByVersion>(StringComparer.OrdinalIgnoreCase);

        private IDictionary<int, DownloadsByVersion> _byDocId =
            new Dictionary<int, DownloadsByVersion>();

        public DownloadsByVersion this[string packageId]
        {
            get
            {
                DownloadsByVersion result = null;
                _downloads.TryGetValue(packageId, out result);

                return result;
            }

            set
            {
                _downloads[packageId] = value;
            }
        }

        public DownloadsByVersion this[int docId]
        {
            get
            {
                DownloadsByVersion result = null;
                _byDocId.TryGetValue(docId, out result);

                return result;
            }

            set
            {
                _byDocId[docId] = value;
            }
        }

        public static void Load(string name, ILoader loader, Action<string, string, long> addCount)
        {
            // The data in downloads.v1.json will be an array of Package records - which has Id, Array of Versions and download count.
            // Sample.json : [["AutofacContrib.NSubstitute",["2.4.3.700",406],["2.5.0",137]],["Assman.Core",["2.0.7",138]]....
            using (var jsonReader = loader.GetReader(name))
            {
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

        public void Load(string name, ILoader loader, FrameworkLogger logger)
        {
            // The data in downloads.v1.json will be an array of Package records - which has Id, Array of Versions and download count.
            // Sample.json : [["AutofacContrib.NSubstitute",["2.4.3.700",406],["2.5.0",137]],["Assman.Core",["2.0.7",138]]....
            using (var jsonReader = loader.GetReader(name))
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
                                string id = String.Intern(record[0].ToString().ToLowerInvariant());

                                // The second entry in each record should be an array of versions, if not move on to next entry.
                                // This is a check to safe guard against invalid entries.
                                if (record.Count() == 2 && record[1].Type != JTokenType.Array)
                                {
                                    continue;
                                }

                                if (!_downloads.ContainsKey(id))
                                {
                                    _downloads.Add(id, new DownloadsByVersion());
                                }
                                var versions = _downloads[id];

                                foreach (JToken token in record)
                                {
                                    if (token != null && token.Count() == 2)
                                    {
                                        string version = String.Intern(token[0].ToString().ToLowerInvariant());
                                        versions[version] = token[1].ToObject<int>();
                                    }
                                }
                            }
                        }
                        catch (JsonReaderException ex)
                        {
                            logger.LogInformation("Invalid entry found in downloads.v1.json. Exception Message : {0}", ex.Message);
                        }
                    }
                }
                catch (JsonReaderException ex)
                {
                    logger.LogError("Data present in downloads.v1.json is invalid. Couldn't get download data.", ex);
                }
            }
        }
    }
}