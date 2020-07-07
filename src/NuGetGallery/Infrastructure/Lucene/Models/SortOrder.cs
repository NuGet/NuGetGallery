// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery.Infrastructure.Search.Models
{
    public enum SortOrder
    {
        Relevance,
        LastEdited,
        Published,
        TitleAscending,
        TitleDescending,
        CreatedAscending,
        CreatedDescending,
        TotalDownloadsAscending,
        TotalDownloadsDescending
    }
}
