// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Specialized;

namespace BasicSearchTests.FunctionalTests.Core.TestSupport
{
    public class V2SearchBuilder : QueryBuilder
    {
        public bool IgnoreFilter { get; set; }

        public V2SearchBuilder() : base("/search/query?") { }

        protected override NameValueCollection GetQueryString()
        {
            var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
            queryString["q"] = Query;
            queryString["prerelease"] = Prerelease.ToString();
            queryString["ignoreFilter"] = IgnoreFilter.ToString();
            return queryString;
        }
    }
}