// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Indexing
{
    public static class ServiceInfoImpl
    {
        public static void Stats(JsonWriter jsonWriter, NuGetSearcherManager searcherManager)
        {
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
    }
}