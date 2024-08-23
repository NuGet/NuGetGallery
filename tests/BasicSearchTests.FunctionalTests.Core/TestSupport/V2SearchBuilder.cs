﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Specialized;

namespace BasicSearchTests.FunctionalTests.Core.TestSupport
{
    public class V2SearchBuilder : QueryBuilder
    {
        public bool IgnoreFilter { get; set; }

        public int? Skip { get; set; }

        public int? Take { get; set; }

        public bool CountOnly { get; set; }

        public bool IncludeSemVer2 { get; set; }

        public string SortBy { get; set; }

        public bool? LuceneQuery { get; set; }

        public string Frameworks { get; set; }

        public string Tfms { get; set; }

        public string PackageType { get; set; }

        public V2SearchBuilder() : base("/search/query?") { }

        protected override NameValueCollection GetQueryString()
        {
            var queryString = base.GetQueryString();

            queryString["ignoreFilter"] = IgnoreFilter.ToString();
            queryString["CountOnly"] = CountOnly.ToString();

            if (Skip.HasValue)
            {
                queryString["Skip"] = Skip.ToString();
            }

            if (Take.HasValue)
            {
                queryString["Take"] = Take.ToString();
            }

            if (IncludeSemVer2)
            {
                queryString["semVerLevel"] = "2.0.0";
            }

            if (!string.IsNullOrWhiteSpace(SortBy))
            {
                queryString["sortBy"] = SortBy;
            }

            if (LuceneQuery.HasValue)
            {
                queryString["luceneQuery"] = LuceneQuery.ToString();
            }

            if (Frameworks != null)
            {
                queryString["frameworks"] = Frameworks;
            }

            if (Tfms != null)
            {
                queryString["tfms"] = Tfms;
            }

            if (PackageType != null)
            {
                queryString["packageType"] = PackageType;
            }

            return queryString;
        }
    }
}