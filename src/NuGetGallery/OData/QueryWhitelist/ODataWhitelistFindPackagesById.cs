// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Web;

namespace NuGetGallery.OData.QueryWhitelist
{
    /// <summary>
    /// Query whitelist for the /api/v2/FindPackagesById
    /// </summary>
    public class ODataWhitelistFindPackagesById : IODataQueryOptionsWhitelist
    {
        private const string WhitelistQueriesFileName = "OData/QueryWhitelist/Data/ODataWhitelistFindPackagesById.txt";
        private static readonly object _instancelock = new object();
        static ODataWhitelistFindPackagesById _instance = null;
        HashSet<string> data = null;

        private ODataWhitelistFindPackagesById(string[] patterns)
        {
            Initialize(patterns);
        }

        private ODataWhitelistFindPackagesById()
        {
            string filePath = $"~/{WhitelistQueriesFileName}";
            string filePathMap = HttpContext.Current.Server.MapPath(filePath);
            Initialize(File.ReadAllLines(filePathMap));
        }

        bool IODataQueryOptionsWhitelist.IsWhitelisted(string queryFormat)
        {
            return data.Contains(queryFormat);
        }

        /// <summary>
        /// Create an instance from a list of patterns
        /// </summary>
        /// <param name="patterns"></param>
        /// <returns></returns>
        public static ODataWhitelistFindPackagesById GetInstance(string[] patterns)
        {
            if (_instance == null)
            {
                lock (_instancelock)
                {
                    if (_instance == null)
                    {
                        _instance = new ODataWhitelistFindPackagesById(patterns);
                    }
                }
            }
            return _instance;
        }

        internal static ODataWhitelistFindPackagesById Instance
        {
            get
            {
                if(_instance == null)
                {
                    lock(_instancelock)
                    {
                        if(_instance == null)
                        {
                            _instance = new ODataWhitelistFindPackagesById();
                        }
                    }
                }
                return _instance;
            }
        }

        private void Initialize(string[] patterns)
        {
            data = new HashSet<string>();
            foreach (string pattern in patterns)
            {
                data.Add(pattern);
            }
        }
    }
}