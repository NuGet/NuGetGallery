// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// <see cref="IndexAction{T}"/> instances separated by whether they apply to the search index or the hijack index
    /// as well as the version list data to write to storage after the index actions have been applied.
    /// </summary>
    public class IndexActions
    {
        public IndexActions(
            IReadOnlyList<IndexAction<KeyedDocument>> search,
            IReadOnlyList<IndexAction<KeyedDocument>> hijack,
            ResultAndAccessCondition<VersionListData> versionListDataResult)
        {
            Search = search ?? throw new ArgumentNullException(nameof(search));
            Hijack = hijack ?? throw new ArgumentNullException(nameof(hijack));
            VersionListDataResult = versionListDataResult ?? throw new ArgumentNullException(nameof(versionListDataResult));
        }

        public IReadOnlyList<IndexAction<KeyedDocument>> Search { get; }
        public IReadOnlyList<IndexAction<KeyedDocument>> Hijack { get; }
        public ResultAndAccessCondition<VersionListData> VersionListDataResult { get; }
    }
}
