// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;

namespace NuGetGallery
{
    public interface IPageableEnumerable<out T>
    {
        IEnumerable<T> Items { get; }
        int PageIndex { get; }
        int PageSize { get; }
    }
}