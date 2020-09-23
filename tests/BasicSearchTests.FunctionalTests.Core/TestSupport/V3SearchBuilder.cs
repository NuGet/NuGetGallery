// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Specialized;

namespace BasicSearchTests.FunctionalTests.Core.TestSupport
{
    public class V3SearchBuilder : QueryBuilder
    {
        public int? Skip { get; set; }

        public int? Take { get; set; }

        public bool IncludeSemVer2 { get; set; }

        public string PackageType { get; set; }

        public V3SearchBuilder() : base("/query?") { }

        protected override NameValueCollection GetQueryString()
        {
            var queryString = base.GetQueryString();

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

            if (PackageType != null)
            {
                queryString["packageType"] = PackageType;
            }

            return queryString;
        }
    }
}