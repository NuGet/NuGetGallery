﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Threading.Tasks;
using System.Web;

namespace NuGetGallery
{
    public interface IContentService
    {
        Task<IHtmlString> GetContentItemAsync(string name, TimeSpan expiresIn);
        Task<IHtmlString> GetContentItemAsync(string name, string extension, TimeSpan expiresIn);
        void ClearCache();
    }
}
