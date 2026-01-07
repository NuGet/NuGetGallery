// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs.Monitoring.PackageLag;
using System;

namespace NuGet.Monitoring.PackageLag.Tests
{
    public static class TestHelpers
    {
        public static SearchResultResponse GetTestSearchResponse(DateTimeOffset indexStamp, DateTimeOffset createdStamp, DateTimeOffset editedStamp, bool listed = true)
        {
            return new SearchResultResponse
            {
                Index = "test",
                IndexTimeStamp = indexStamp,
                TotalHits = 1,
                Data = new SearchResult[1]
                {
                    new SearchResult
                    {
                        Created = createdStamp,
                        LastEdited = editedStamp,
                        Listed = listed
                    }
                }
            };
        }

        public static SearchResultResponse GetEmptyTestSearchResponse(DateTimeOffset indexStamp)
        {
            return new SearchResultResponse
            {
                Index = "test",
                IndexTimeStamp = indexStamp,
                TotalHits = 0,
                Data = new SearchResult[0]
            };
        }
    }
}
