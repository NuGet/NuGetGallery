﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Web;
using NuGetGallery;

namespace GitHubVulnerabilities2Db.Fakes
{
    public class FakeContentService : IContentService
    {
        public void ClearCache()
        {
            //no-op
        }

        public Task<IHtmlString> GetContentItemAsync(string name, TimeSpan expiresIn)
        {
            // no-op
            return Task.FromResult((IHtmlString)null);
        }
    }
}