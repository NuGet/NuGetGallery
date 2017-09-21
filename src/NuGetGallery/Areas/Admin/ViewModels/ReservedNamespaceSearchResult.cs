// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    /// <summary>
    /// Reserved namespace search results for the reserve namespace admin view.
    /// </summary>
    public sealed class ReservedNamespaceSearchResult
    {
        /// <summary>
        /// Found prefixes matching the search query
        /// </summary>
        public IEnumerable<ReservedNamespaceResultModel> Prefixes { get; set; }
    }
}