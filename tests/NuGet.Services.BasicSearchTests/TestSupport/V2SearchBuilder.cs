// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.BasicSearchTests.TestSupport
{
    public class V2SearchBuilder
    {
        public string Query { get; set; }
        public bool Prerelease { get; set; }
        public bool IgnoreFilter { get; set; }
        public string SemVerLevel { get; set; }

        public Uri RequestUri
        {
            get
            {
                var queryStringBuilder = System.Web.HttpUtility.ParseQueryString(string.Empty);
                queryStringBuilder["prerelease"] = Prerelease.ToString();
                queryStringBuilder["ignoreFilter"] = IgnoreFilter.ToString();
                queryStringBuilder["semVerLevel"] = SemVerLevel;

                // Use Uri.EscapeDataString to handle obscure Unicode characters properly.
                var queryString = $"q={Uri.EscapeDataString(Query ?? string.Empty)}&{queryStringBuilder}";

                return new Uri("/search/query?" + queryString, UriKind.Relative);
            }
        }
    }
}