// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class AuxiliaryFiles
    {
        // List of all auxiliary files
        internal const string Owners = "owners.json";
        internal const string DownloadsV1 = "downloads.v1.json";
        internal const string CuratedFeeds = "curatedfeeds.json";
        internal const string RankingsV1 = "rankings.v1.json";
        internal const string SearchSettingsV1 = "searchSettings.v1.json";

        public virtual IDictionary<string, DateTime?> LastModifiedTimeForFiles { get; }

        private ILoader _loader;
        private readonly string[] fileList = new string[]
        {
            Owners,
            DownloadsV1,
            CuratedFeeds,
            RankingsV1,
            SearchSettingsV1
        };

        public AuxiliaryFiles(ILoader loader)
        {
            _loader = loader;
            LastModifiedTimeForFiles = new Dictionary<string, DateTime?>();
        }

        internal void UpdateLastModifiedTime()
        {
            foreach (string fileName in fileList)
            {
                LastModifiedTimeForFiles[fileName] = _loader.GetLastUpdateTime(fileName);
            }
        }
    }
}
