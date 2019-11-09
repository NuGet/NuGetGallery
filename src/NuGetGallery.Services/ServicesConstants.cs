// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public static class ServicesConstants
    {
        public static readonly string CurrentUserOwinEnvironmentKey = "nuget.user";

        internal static readonly string UserAgentHeaderName = "User-Agent";

        // X-NuGet-Client-Version header was deprecated and replaced with X-NuGet-Protocol-Version header
        // It stays here for backwards compatibility
        public const string ClientVersionHeaderName = "X-NuGet-Client-Version";
        public const string NuGetProtocolHeaderName = "X-NuGet-Protocol-Version";

        public const string DevelopmentEnvironment = "Development";
        public const string GitRepository = "git";

        public const string MarkdownFileExtension = ".md";
        public const string HtmlFileExtension = ".html";
        public const string JsonFileExtension = ".json";

        // Note: regexes must be tested to work in JavaScript
        // We do NOT follow strictly the RFCs at this time, and we choose not to support many obscure email address variants.
        // Specifically the following are not supported by-design:
        //  * Addresses containing () or []
        //  * Second parts with no dots (i.e. foo@localhost or foo@com)
        //  * Addresses with quoted (" or ') first parts
        //  * Addresses with IP Address second parts (foo@[127.0.0.1])
        internal const string EmailValidationRegexFirstPart = @"[-A-Za-z0-9!#$%&'*+\/=?^_`{|}~\.]+";
        internal const string EmailValidationRegexSecondPart = @"[A-Za-z0-9]+[\w\.-]*[A-Za-z0-9]*\.[A-Za-z0-9][A-Za-z\.]*[A-Za-z]";
        public const string EmailValidationRegex = "^" + EmailValidationRegexFirstPart + "@" + EmailValidationRegexSecondPart + "$";
        public static TimeSpan EmailValidationRegexTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Parameters for calculating account lockout period after 
        /// wrong password entry.
        /// </summary>
        public const double AccountLockoutMultiplierInMinutes = 10;
        public const double AllowedLoginAttempts = 10;

        public const string Sha1HashAlgorithmId = "SHA1";

        public const string ApiKeyHeaderName = "X-NuGet-ApiKey";

        public static class ContentNames
        {
            public static readonly string LoginDiscontinuationConfiguration = "Login-Discontinuation-Configuration";
            public static readonly string CertificatesConfiguration = "Certificates-Configuration";
            public static readonly string SymbolsConfiguration = "Symbols-Configuration";
            public static readonly string TyposquattingConfiguration = "Typosquatting-Configuration";
            public static readonly string NuGetPackagesGitHubDependencies = "GitHubUsage.v1";
            public static readonly string ABTestConfiguration = "AB-Test-Configuration";
            public static readonly string ODataCacheConfiguration = "OData-Cache-Configuration";
        }
    }
}
