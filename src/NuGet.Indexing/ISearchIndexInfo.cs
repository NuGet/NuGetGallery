// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public interface ISearchIndexInfo
    {
        string IndexName { get; }
        DateTime LastReopen { get; }
        int NumDocs { get; }
        IDictionary<string, string> CommitUserData { get; }
    }
}