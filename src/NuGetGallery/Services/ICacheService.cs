// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;

namespace NuGetGallery
{
    public interface ICacheService
    {
        object GetItem(string key);
        void SetItem(string key, object item, TimeSpan timeout);
        void RemoveItem(string key);
    }
}