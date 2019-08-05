// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Specialized;
using System.Web;

namespace BasicSearchTests.FunctionalTests.Core.TestSupport
{
    public class V3SearchBuilder : QueryBuilder
    {
        public int? Skip { get; set; }

        public int? Take { get; set; }

        public bool IncludeSemVer2 { get; set; }

        public V3SearchBuilder() : base("/query?") { }

        protected override NameValueCollection GetQueryString()
        {
            var queryString = HttpUtility.ParseQueryString(string.Empty);
            queryString["q"] = Query;
            queryString["prerelease"] = Prerelease.ToString();

            if (Skip.HasValue)
            {
                queryString["skip"] = Skip.ToString();
            }

            if (Take.HasValue)
            {
                queryString["take"] = Take.ToString();
            }

            if (IncludeSemVer2)
            {
                queryString["semVerLevel"] = "2.0.0";
            }

            return queryString;
        }
    }
}