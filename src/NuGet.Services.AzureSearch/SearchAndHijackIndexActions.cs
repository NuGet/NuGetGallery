// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.Search.Models;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// <see cref="IndexAction{T}"/> instances seperated by whether they apply to the search index or the hijack index.
    /// </summary>
    public class SearchAndHijackIndexActions
    {
        public SearchAndHijackIndexActions(
            IReadOnlyList<IndexAction<KeyedDocument>> search,
            IReadOnlyList<IndexAction<KeyedDocument>> hijack)
        {
            Search = search ?? throw new ArgumentNullException(nameof(search));
            Hijack = hijack ?? throw new ArgumentNullException(nameof(hijack));
        }

        public IReadOnlyList<IndexAction<KeyedDocument>> Search { get; }
        public IReadOnlyList<IndexAction<KeyedDocument>> Hijack { get; }
    }
}
