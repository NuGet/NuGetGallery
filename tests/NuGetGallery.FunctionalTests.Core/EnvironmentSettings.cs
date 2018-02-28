﻿// Copyright (c) .NET Foundation. All rights reserved.
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
        private static string _externalBrandingMessage;
        private static string _externalBrandingUrl;
        private static string _externalAboutUrl;
        private static string _externalPrivacyPolicyUrl;
        private static string _externalTermsOfUseUrl;
        private static string _externalTrademarksUrl;
        private static string _testAccountEmail;
        private static string _testAccountName;
        private static string _testAccountPassword;
        private static string _testAccountApiKey;
        private static string _testAccountApiKey_Unlist;
        private static string _testAccountApiKey_PushPackage;
        private static string _testAccountApiKey_PushVersion;
        private static string _testOrganizationAdminAccountName;
        private static string _testOrganizationAdminAccountApiKey;
        private static string _testOrganizationCollaboratorAccountName;
        private static string _testOrganizationCollaboratorAccountApiKey;
        private static string _testEmailServerHost;
        private static List<string> _trustedHttpsCertificates;
        private static bool? _defaultSecurityPoliciesEnforced;
        private static bool? _testPackageLock;

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
                        _searchServiceBaseurl = "https://nuget-int-0-v2v3search.cloudapp.net/";
                    }
                    else
                    {
                        // Check for the environment variable under all scopes. This is to make sure that the variables are acessed properly in teamcity where we cannot set process level variables.
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
                    _searchServiceBaseurl = "https://nuget-int-0-v2v3search.cloudapp.net/";
                }

                return _searchServiceBaseurl;
            }
        }
        
        /// <summary>
        /// External branding settings
        /// </summary>
        public static string ExternalBrandingMessage
        {
            get
            {
                if (string.IsNullOrEmpty(_externalBrandingMessage))
                {
                    _externalBrandingMessage  = Environment.GetEnvironmentVariable("ExternalBrandingMessage");
                }
                return _externalBrandingMessage;
            }
        }

        public static string ExternalBrandingUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_externalBrandingUrl))
                {
                    _externalBrandingUrl = Environment.GetEnvironmentVariable("ExternalBrandingUrl");
                }
                return _externalBrandingUrl;
            }
        }

        public static string ExternalAboutUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_externalAboutUrl))
                {
                    _externalAboutUrl = Environment.GetEnvironmentVariable("ExternalAboutUrl");
                }
                return _externalAboutUrl;
            }
        }

        public static string ExternalPrivacyPolicyUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_externalPrivacyPolicyUrl))
                {
                    _externalPrivacyPolicyUrl = Environment.GetEnvironmentVariable("ExternalPrivacyPolicyUrl");
                }
                return _externalPrivacyPolicyUrl;
            }
        }

        public static string ExternalTermsOfUseUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_externalTermsOfUseUrl))
                {
                    _externalTermsOfUseUrl = Environment.GetEnvironmentVariable("ExternalTermsOfUseUrl");
                }
                return _externalTermsOfUseUrl;
            }
        }

        public static string ExternalTrademarksUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_externalTrademarksUrl))
                {
                    _externalTrademarksUrl = Environment.GetEnvironmentVariable("ExternalTrademarksUrl");
                }
                return _externalTrademarksUrl;
            }
        }

        /// <summary>
        /// The test nuget account name to be used for functional tests.
        /// </summary>
        public static string TestAccountEmail
        {
            get
            {
                if (string.IsNullOrEmpty(_testAccountEmail))
                {
                    _testAccountEmail = Environment.GetEnvironmentVariable("TestAccountEmail");
                }
                return _testAccountEmail;
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
        /// The full access API key for the test account.
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

        /// <summary>
        /// Scoped API key for account. Permission: unlist
        /// </summary>
        public static string TestAccountApiKey_Unlist
        {
            get
            {
                if (string.IsNullOrEmpty(_testAccountApiKey_Unlist))
                {
                    _testAccountApiKey_Unlist = Environment.GetEnvironmentVariable("TestAccountApiKey_Unlist");
                }
                return _testAccountApiKey_Unlist;
            }
        }

        /// <summary>
        /// Scoped API key for account. Permission: push
        /// </summary>
        public static string TestAccountApiKey_Push
        {
            get
            {
                if (string.IsNullOrEmpty(_testAccountApiKey_PushPackage))
                {
                    _testAccountApiKey_PushPackage = Environment.GetEnvironmentVariable("TestAccountApiKey_Push");
                }
                return _testAccountApiKey_PushPackage;
            }
        }

        /// <summary>
        /// Scoped API key for account. Permission: push version
        /// </summary>
        public static string TestAccountApiKey_PushVersion
        {
            get
            {
                if (string.IsNullOrEmpty(_testAccountApiKey_PushVersion))
                {
                    _testAccountApiKey_PushVersion = Environment.GetEnvironmentVariable("TestAccountApiKey_PushVersion");
                }
                return _testAccountApiKey_PushVersion;
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

        /// <summary>
        /// The name of the test nuget organization that <see cref="TestAccountName"/> is an admin of.
        /// </summary>
        public static string TestOrganizationAdminAccountName
        {
            get
            {
                if (string.IsNullOrEmpty(_testOrganizationAdminAccountName))
                {
                    _testOrganizationAdminAccountName = Environment.GetEnvironmentVariable("TestOrganizationAdminAccountName");
                }
                return _testOrganizationAdminAccountName;
            }
        }

        /// <summary>
        /// An API key for the test account scoped to <see cref="TestOrganizationAdminAccountName"/>.
        /// </summary>
        public static string TestOrganizationAdminAccountApiKey
        {
            get
            {
                if (string.IsNullOrEmpty(_testOrganizationAdminAccountApiKey))
                {
                    _testOrganizationAdminAccountApiKey = Environment.GetEnvironmentVariable("TestOrganizationAdminAccountApiKey");
                }
                return _testOrganizationAdminAccountApiKey;
            }
        }

        /// <summary>
        /// The name of the test nuget organization that <see cref="TestAccountName"/> is a collaborator of.
        /// </summary>
        public static string TestOrganizationCollaboratorAccountName
        {
            get
            {
                if (string.IsNullOrEmpty(_testOrganizationCollaboratorAccountName))
                {
                    _testOrganizationCollaboratorAccountName = Environment.GetEnvironmentVariable("TestOrganizationCollaboratorAccountName");
                }
                return _testOrganizationCollaboratorAccountName;
            }
        }

        /// <summary>
        /// An API key for the test account scoped to <see cref="TestOrganizationCollaboratorAccountName"/>.
        /// </summary>
        public static string TestOrganizationCollaboratorAccountApiKey
        {
            get
            {
                if (string.IsNullOrEmpty(_testOrganizationCollaboratorAccountApiKey))
                {
                    _testOrganizationCollaboratorAccountApiKey = Environment.GetEnvironmentVariable("TestOrganizationCollaboratorAccountApiKey");
                }
                return _testOrganizationCollaboratorAccountApiKey;
            }
        }

        public static bool DefaultSecurityPoliciesEnforced
        {
            get
            {
                if (!_defaultSecurityPoliciesEnforced.HasValue)
                {
                    // Try to get the setting from EnvironmentVariable. If fail, fallback to false
                    bool temp;
                    if (bool.TryParse(Environment.GetEnvironmentVariable("DefaultSecurityPoliciesEnforced"), out temp))
                    {
                        _defaultSecurityPoliciesEnforced = temp;
                    }
                    else
                    {
                        _defaultSecurityPoliciesEnforced = false;
                    }
                }

                return _defaultSecurityPoliciesEnforced.Value;
            }
        }

        public static bool TestPackageLock
        {
            get
            {
                if (!_testPackageLock.HasValue)
                {
                    // Try to get the setting from EnvironmentVariable. If fail, fallback to false
                    bool temp;
                    if (bool.TryParse(Environment.GetEnvironmentVariable("TestPackageLock"), out temp))
                    {
                        _testPackageLock = temp;
                    }
                    else
                    {
                        _testPackageLock = false;
                    }
                }

                return _testPackageLock.Value;
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
                            "6cd4e9738ae52ba11e7b81da8caafbeadf89488f", // *.nugettest.org
                            "9d984f91f40d8b3a1fb29153179415523c4e64d1", // *.int.nugettest.org
                            "03984834f27d5c94f46b3bb190e5a8099787268a", // *.nuget.org (old)
                            "a238919da29991914de5066ae4712db1bc41d3b5"  // *.nuget.org (new)
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
