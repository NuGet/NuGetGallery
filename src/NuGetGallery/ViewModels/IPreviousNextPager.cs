// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery
{
    public interface IPreviousNextPager
    {
        bool HasNextPage { get; }
        bool HasPreviousPage { get; }
        string NextPageUrl { get; }
        string PreviousPageUrl { get; }
    }
}