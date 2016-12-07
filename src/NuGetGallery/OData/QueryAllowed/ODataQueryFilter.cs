// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Http.OData.Query;
using Newtonsoft.Json;

namespace NuGetGallery.OData.QueryFilter
{
    /// <summary>
    /// IODataQueryFilter interface
    /// </summary>
    public class ODataQueryFilter
    {
        [Flags]
        public enum ODataOperators
        {
            none = 0,
            expand = 1,
            filter = 1 << 1,
            format = 1 << 2,
            inlinecount = 1 << 3,
            orderby = 1 << 4,
            select = 1 << 5,
            skip = 1 << 6,
            skiptoken = 1 << 7,
            top = 1 << 8
        }

        private static readonly string ResourcesNamespace = "NuGetGallery.OData.QueryAllowed.Data";
        private HashSet<ODataOperators> _allowedOperatorPatterns = null;

        /// <summary>
        /// Initialization for a query filter.
        /// </summary>
        /// <param name="fileName"></param>
        public ODataQueryFilter(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            StreamReader sr = new StreamReader(assembly.GetManifestResourceStream($"{ResourcesNamespace}.{fileName}"));
            string json = sr.ReadToEnd(); 
            ODataQueryRequest data = JsonConvert.DeserializeObject<ODataQueryRequest>(json);
            _allowedOperatorPatterns = new HashSet<ODataOperators>(data.AllowedOperatorPatterns
                .Select( (op) => { return (ODataOperators)Enum.Parse(typeof(ODataOperators), op, true); } ));
            if (!_allowedOperatorPatterns.Contains(ODataOperators.none)) { _allowedOperatorPatterns.Add(ODataOperators.none); }
        }

        public ODataQueryFilter()
        {
        }

        /// <summary>
        /// Verifies if queryFormat is allowed.
        /// </summary>
        /// <param name="odataOptions">The <see cref="ODataQueryOptions"/> to be validated.</param>
        /// <returns>Returns true if the queryFormat is allowed.</returns>
        public virtual bool IsAllowed<T>(ODataQueryOptions<T> odataOptions)
        {
            if(odataOptions == null)
            {
                return true;
            }
            return _allowedOperatorPatterns.Contains(ODataOptionsMap(odataOptions));
        }

        /// <summary>
        /// The allowed operators for this API
        /// </summary>
        public HashSet<ODataOperators> AllowedOperatorPatterns => _allowedOperatorPatterns;

        /// <summary>
        /// Reads the odataOptions used parameters and returns <see cref="ODataOperators"/> 
        /// that represents the set of operators used by this odataOptions.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="odataOptions"></param>
        /// <returns>The <see cref="ODataOperators"/> representation of the operators in the OData options. 
        /// If no operator is used the result will be <see cref="ODataOperators.none"/>.</returns>
        public static ODataOperators ODataOptionsMap<T>(ODataQueryOptions<T> odataOptions)
        {
            ODataOperators result = ODataOperators.none;
            if(odataOptions == null)
            {
                return 0;
            }
            if (odataOptions.RawValues.Expand != null)
            {
                result |= ODataOperators.expand;
            }
            if (odataOptions.RawValues.Filter != null)
            {
                result |= ODataOperators.filter;
            }
            if (odataOptions.RawValues.Format != null)
            {
                result |= ODataOperators.format;
            }
            if (odataOptions.RawValues.InlineCount != null)
            {
                result |= ODataOperators.inlinecount;
            }
            if (odataOptions.RawValues.OrderBy != null)
            {
                result |= ODataOperators.orderby;
            }
            if (odataOptions.RawValues.Select != null)
            {
                result |= ODataOperators.select;
            }
            if (odataOptions.RawValues.Skip != null)
            {
                result |= ODataOperators.skip;
            }
            if (odataOptions.RawValues.SkipToken != null)
            {
                result |= ODataOperators.skiptoken;
            }
            if (odataOptions.RawValues.Top != null)
            {
                result |= ODataOperators.top;
            }
            return result;
        }
    }
}
