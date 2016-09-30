// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Specialized;

namespace BasicSearchTests.FunctionalTests.Core.TestSupport
{
    public class QueryBuilder
    {
        protected string Endpoint;

        public string Query { get; set; }
        public bool Prerelease { get; set; }

        public QueryBuilder(string endpoint)
        {
            Endpoint = endpoint;
        }

        protected virtual NameValueCollection GetQueryString()
        {
            var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
            queryString["q"] = Query;
            queryString["prerelease"] = Prerelease.ToString();
            return queryString;
        }

        public Uri RequestUri
        {
            get
            {
                return new Uri(Endpoint + GetQueryString(), UriKind.Relative);
            }
        }
    }
}