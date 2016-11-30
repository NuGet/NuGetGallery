// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Http.OData.Query;
using Newtonsoft.Json;

namespace NuGetGallery.OData.QueryFilter
{
    /// <summary>
    /// IODataQueryFilter interface
    /// </summary>
    public class ODataQueryFilter
    {
        private static readonly string resourcesNamespace = "NuGetGallery.OData.QueryAllowed.Data";
        private static readonly int expand = 1;
        private static readonly int filter = 2;
        private static readonly int format = 4;
        private static readonly int inlinecount = 8;
        private static readonly int orderby = 16;
        private static readonly int select = 32;
        private static readonly int skip = 64;
        private static readonly int skiptoken = 128;
        private static readonly int top = 256;

        private HashSet<int> allowedOperatorPatterns = null;

        /// <summary>
        /// Initialization for a query filter
        /// </summary>
        /// <param name="fileName"></param>
        public ODataQueryFilter(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            StreamReader sr = new StreamReader(assembly.GetManifestResourceStream($"{resourcesNamespace}.{fileName}"));
            string json = sr.ReadToEnd(); 
            ODataQueryRequest data = JsonConvert.DeserializeObject<ODataQueryRequest>(json);
            allowedOperatorPatterns = new HashSet<int>(data.AllowedOperatorPatterns);
        }

        /// <summary>
        /// Returns true if the queryFormat is accepted
        /// </summary>
        /// <param name="queryFormat">The integer representing the odata operators in the request representing the query to be validated.</param>
        /// <returns></returns>
        public bool IsAllowed<T>(ODataQueryOptions<T> odataOptions)
        {
            if(odataOptions == null)
            {
                return true;
            }
            return allowedOperatorPatterns.Contains(ODataOptionsMap(odataOptions));
        }

        /// <summary>
        /// The allowed operators for this API
        /// </summary>
        public HashSet<int> AllowedOperatorPatterns
        {
            get
            {
                return allowedOperatorPatterns;
            }
        }

        /// <summary>
        /// Reads the odataOptions used parameters and returns an integer that represents the operators used by this options
        /// The integer values for the single operators are:
        ///    expand = 1;
        ///    filter = 2;
        ///    format = 4;
        ///    inlinecount = 8;
        ///    orderby = 16;
        ///    select = 32;
        ///    skip = 64;
        ///    skiptoken = 128;
        ///    top = 256;
        /// An option that will have "expand" and "filter" will have a return result with value of 3
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="odataOptions"></param>
        /// <returns>The integer represantation of the operators in the OData options. If no opertor used the result will be 0.</returns>
        public static int ODataOptionsMap<T>(ODataQueryOptions<T> odataOptions)
        {
            int result = 0;
            if(odataOptions == null)
            {
                return 0;
            }
            if (odataOptions.RawValues.Expand != null)
            {
                result |= expand;
            }
            if (odataOptions.RawValues.Filter != null)
            {
                result |= filter;
            }
            if (odataOptions.RawValues.Format != null)
            {
                result |= format;
            }
            if (odataOptions.RawValues.InlineCount != null)
            {
                result |= inlinecount;
            }
            if (odataOptions.RawValues.OrderBy != null)
            {
                result |= orderby;
            }
            if (odataOptions.RawValues.Select != null)
            {
                result |= select;
            }
            if (odataOptions.RawValues.Skip != null)
            {
                result |= skip;
            }
            if (odataOptions.RawValues.SkipToken != null)
            {
                result |= skiptoken;
            }
            if (odataOptions.RawValues.Top != null)
            {
                result |= top;
            }
            return result;
        }

        /// <summary>
        /// For an integer representing the OData option operators set, 
        /// will return the string readable version. 
        /// For example for a value of 6 will return "filter format"
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public static string GetFriendlyReadQueryPattern(int pattern)
        {
            StringBuilder sb = new StringBuilder();
            if ((pattern & expand) != 0)
            {
                sb.Append($"{nameof(expand)}");
            }
            if ((pattern & filter) != 0)
            {
                sb.Append($"{nameof(filter)}");
            }
            if ((pattern & format) != 0)
            {
                sb.Append($"{nameof(format)}");
            }
            if ((pattern & inlinecount) != 0)
            {
                sb.Append($"{nameof(inlinecount)}");
            }
            if ((pattern & orderby) != 0)
            {
                sb.Append($"{nameof(orderby)}");
            }
            if ((pattern & select) != 0)
            {
                sb.Append($"{nameof(select)}");
            }
            if ((pattern & skip) != 0)
            {
                sb.Append($"{nameof(skip)}");
            }
            if ((pattern & skiptoken) != 0)
            {
                sb.Append($"{nameof(skiptoken)}");
            }
            if ((pattern & top) != 0)
            {
                sb.Append($"{nameof(top)}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns a readable form of the supported operators
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public static string GetFriendlyReadQueryPattern(IEnumerable<int> patterns)
        {
            var result = string.Join("\n", patterns);
            return result;
        }
    }
}
