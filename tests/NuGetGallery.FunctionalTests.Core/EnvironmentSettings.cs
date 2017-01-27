// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// This class reads the various test run settings which are set through env variable.
    /// </summary>
    public class EnvironmentSettings
    {
        private static string _baseurl;
        private static string _searchServiceBaseurl;
        private static string _testAccountName;
        private static string _testAccountPassword;
        private static string _testAccountApiKey;
        private static string _testEmailServerHost;
        private static List<string> _trustedHttpsCertificates;
        
        /// <summary>
        /// The environment against which the test has to be run. The value would be picked from env variable.
        /// If nothing is specified, preview is used as default.
        /// </summary>
        public static string BaseUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_baseurl))
                {
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GalleryUrl",
                            EnvironmentVariableTarget.User)) &&
                        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GalleryUrl",
                            EnvironmentVariableTarget.Process)) &&
                        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GalleryUrl",
                            EnvironmentVariableTarget.Machine)))
                    {
                        _baseurl = "https://int.nugettest.org/";
                    }
                    else
                    {
                        // Check for the environment variable under all scopes. This is to make sure that the variables are acessed properly in teamcity where we cannot set process leve variables.
                        if (!string.IsNullOrEmpty(
                            Environment.GetEnvironmentVariable("GalleryUrl", EnvironmentVariableTarget.User)))
                        {
                            _baseurl = Environment.GetEnvironmentVariable("GalleryUrl",
                                EnvironmentVariableTarget.User);
                        }
                        else if (!string.IsNullOrEmpty(
                            Environment.GetEnvironmentVariable("GalleryUrl", EnvironmentVariableTarget.Process)))
                        {
                            _baseurl = Environment.GetEnvironmentVariable("GalleryUrl",
                                EnvironmentVariableTarget.Process);
                        }
                        else
                        {
                            _baseurl = Environment.GetEnvironmentVariable("GalleryUrl",
                                EnvironmentVariableTarget.Machine);
                        }
                    }
                }

                if (string.IsNullOrEmpty(_baseurl))
                {
                    _baseurl = "https://int.nugettest.org/";
                }

                return _baseurl;
            }
        }

        /// <summary>
        /// The environment against which the (search service) test has to be run. The value would be picked from env variable.
        /// If nothing is specified, preview is used as default.
        /// </summary>
        public static string SearchServiceBaseUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_searchServiceBaseurl))
                {
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SearchServiceUrl",
                            EnvironmentVariableTarget.User)) &&
                        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SearchServiceUrl",
                            EnvironmentVariableTarget.Process)) &&
                        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SearchServiceUrl",
                            EnvironmentVariableTarget.Machine)))
                    {
                        _searchServiceBaseurl = "http://nuget-int-0-v2v3search.cloudapp.net/";
                    }
                    else
                    {
                        // Check for the environment variable under all scopes. This is to make sure that the variables are acessed properly in teamcity where we cannot set process leve variables.
                        if (!string.IsNullOrEmpty(
                            Environment.GetEnvironmentVariable("SearchServiceUrl", EnvironmentVariableTarget.User)))
                        {
                            _searchServiceBaseurl = Environment.GetEnvironmentVariable("SearchServiceUrl",
                                EnvironmentVariableTarget.User);
                        }
                        else if (!string.IsNullOrEmpty(
                            Environment.GetEnvironmentVariable("SearchServiceUrl", EnvironmentVariableTarget.Process)))
                        {
                            _searchServiceBaseurl = Environment.GetEnvironmentVariable("SearchServiceUrl",
                                EnvironmentVariableTarget.Process);
                        }
                        else
                        {
                            _searchServiceBaseurl = Environment.GetEnvironmentVariable("SearchServiceUrl",
                                EnvironmentVariableTarget.Machine);
                        }
                    }
                }

                if (string.IsNullOrEmpty(_searchServiceBaseurl))
                {
                    _searchServiceBaseurl = "http://nuget-int-0-v2v3search.cloudapp.net/";
                }

                return _searchServiceBaseurl;
            }
        }

        /// <summary>
        /// The test nuget account name to be used for functional tests.
        /// </summary>
        public static string TestAccountName
        {
            get
            {
                if (string.IsNullOrEmpty(_testAccountName))
                {
                    _testAccountName = Environment.GetEnvironmentVariable("TestAccountName");
                }
                return _testAccountName;
            }
        }

        /// <summary>
        /// The password for the test account.
        /// </summary>
        public static string TestAccountPassword
        {
            get
            {
                if (string.IsNullOrEmpty(_testAccountPassword))
                {
                    _testAccountPassword = Environment.GetEnvironmentVariable("TestAccountPassword");
                }
                return _testAccountPassword;
            }
        }

        /// <summary>
        /// The password for the test account.
        /// </summary>
        public static string TestAccountApiKey
        {
            get
            {
                if (string.IsNullOrEmpty(_testAccountApiKey))
                {
                    _testAccountApiKey = Environment.GetEnvironmentVariable("TestAccountApiKey");
                }
                return _testAccountApiKey;
            }
        }

        public static string TestEmailServerHost
        {
            get
            {
                if (string.IsNullOrEmpty(_testEmailServerHost))
                {
                    _testEmailServerHost = Environment.GetEnvironmentVariable("TestEmailServerHost");
                }
                return _testEmailServerHost;
            }
        }

        public static IEnumerable<string> TrustedHttpsCertificates
        {
            get
            {
                if (_trustedHttpsCertificates == null)
                {
                    var unparsedValued = Environment.GetEnvironmentVariable("TrustedHttpsCertificates") ?? string.Empty;

                    List<string> pieces;
                    if (unparsedValued.Length == 0)
                    {
                        // This list will need to be modified as DEV, INT, and PROD certificates change and are
                        // renewed. These values are easily and publicly discoverable by inspecting the certificate
                        // returned from HTTPS browser interactions with the gallery.
                        pieces = new List<string>
                        {
                            "8c11c16610b7a147d10bbcc6a65ce23d321c12c2", // *.nugettest.org
                            "9d984f91f40d8b3a1fb29153179415523c4e64d1", // *.int.nugettest.org
                            "3751cb513b93ee67ec9f18a1f2aec1eac87af9bc"  // *.nuget.org
                        };
                    }
                    else
                    {
                        pieces = unparsedValued
                            .Split(',')
                            .Select(p => p.Trim())
                            .Where(p => p.Length > 0)
                            .ToList();
                    }

                    _trustedHttpsCertificates = pieces;
                }

                return _trustedHttpsCertificates;
            }
        }
    }
}
