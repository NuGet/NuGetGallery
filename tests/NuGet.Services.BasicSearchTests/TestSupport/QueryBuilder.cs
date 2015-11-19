// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.BasicSearchTests.TestSupport
{
    public class QueryBuilder
    {
        public string Query { get; set; }
        public bool Prerelease { get; set; }

        public Uri RequestUri
        {
            get
            {
                var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
                queryString["q"] = Query;
                queryString["prerelease"] = Prerelease.ToString();

                return new Uri("/query?" + queryString, UriKind.Relative);
            }
        }
    }
}