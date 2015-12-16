// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Indexing
{
    public static class ServiceInfoImpl
    {
        public static void Stats(JsonWriter jsonWriter, NuGetSearcherManager searcherManager)
        {
            searcherManager.MaybeReopen();
            var searcher = searcherManager.Get();
            try
            {
                ResponseFormatter.WriteStatsResult(jsonWriter, searcher);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public static void Rankings(JsonWriter jsonWriter, NuGetSearcherManager searcherManager)
        {
            var searcher = searcherManager.Get();
            try
            {
                ResponseFormatter.WriteRankingsResult(jsonWriter, searcher.Rankings);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }
    }
}