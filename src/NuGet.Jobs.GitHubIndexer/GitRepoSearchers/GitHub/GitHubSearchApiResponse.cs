// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Jobs.GitHubIndexer
{
    public class GitHubSearchApiResponse
    {
        public GitHubSearchApiResponse(IReadOnlyList<WritableRepositoryInformation> result, DateTimeOffset date, DateTimeOffset throttleResetTime)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Date = date;
            ThrottleResetTime = throttleResetTime;
        }

        public IReadOnlyList<WritableRepositoryInformation> Result { get; }
        public DateTimeOffset Date { get; }
        public DateTimeOffset ThrottleResetTime { get; }
    }
}
