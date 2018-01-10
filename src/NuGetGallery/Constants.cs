// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;

namespace NuGetGallery
{
    public static class Constants
    {
        public const string AdminRoleName = "Admins";
        public const string AlphabeticSortOrder = "package-title";
        public const int DefaultPackageListPageSize = 20;
        public const string DefaultPackageListSortOrder = "package-download-count";
        public const int PasswordResetTokenExpirationHours = 1;

        public const int ColumnsAuthenticationSm = 6;
        public const int ColumnsAuthenticationMd = 4;
        public const int ColumnsWideAuthenticationSm = 8;
        public const int ColumnsWideAuthenticationMd = 6;
        public const int ColumnsFormMd = 10;

        public const int VisibleVersions = 5;

        public const int GravatarElementSize = 32;
        public const int GravatarImageSize = GravatarElementSize * 2;
        public const int GravatarImageSizeLarge = 332;

        /// <summary>
        /// Parameters for calculating account lockout period after 
        /// wrong password entry.
        /// </summary>
        public const double AccountLockoutMultiplierInMinutes = 10;
        public const double AllowedLoginAttempts = 10;

        public const int MaxEmailSubjectLength = 255;
        internal static readonly NuGetVersion MaxSupportedMinClientVersion = new NuGetVersion("4.1.0.0");
        public const string PackageFileDownloadUriTemplate = "packages/{0}/{1}/download";

        public const string ReadMeFileSavePathTemplateActive = "active/{0}/{1}{2}";
        public const string ReadMeFileSavePathTemplatePending = "pending/{0}/{1}{2}";

        public const string PackageFileBackupSavePathTemplate = "{0}/{1}/{2}.{3}";

        public const string MarkdownFileExtension = ".md";
        public const string HtmlFileExtension = ".html";
        public const string JsonFileExtension = ".json";

        public const string PopularitySortOrder = "package-download-count";
        public const string RecentSortOrder = "package-created";
        public const string RelevanceSortOrder = "relevance";

        public const string Sha1HashAlgorithmId = "SHA1";
        public const string Sha512HashAlgorithmId = "SHA512";
        public const string PBKDF2HashAlgorithmId = "PBKDF2";

        public const string UploadFileNameTemplate = "{0}{1}";
        public const string NuGetCommandLinePackageId = "NuGet.CommandLine";

        public static readonly string ReturnUrlViewDataKey = "ReturnUrl";
        public const string AbsoluteLatestUrlString = "absoluteLatest";

        public const string UrlValidationRegEx = @"(https?):\/\/[^ ""]+$";
        public const string UrlValidationErrorMessage = "This doesn't appear to be a valid HTTP/HTTPS URL";

        internal const string ApiKeyHeaderName = "X-NuGet-ApiKey";
        // X-NuGet-Client-Version header was deprecated and replaced with X-NuGet-Protocol-Version header
        // It stays here for backwards compatibility
        internal const string ClientVersionHeaderName = "X-NuGet-Client-Version";
        internal const string NuGetProtocolHeaderName = "X-NuGet-Protocol-Version";
        internal const string WarningHeaderName = "X-NuGet-Warning";
        internal const string UserAgentHeaderName = "User-Agent";

        public static readonly string ReturnUrlParameterName = "ReturnUrl";
        public static readonly string CurrentUserOwinEnvironmentKey = "nuget.user";

        public const string DevelopmentEnvironment = "Development";

        public static class ContentNames
        {
            public static readonly string ReadOnly = "ReadOnly";
            public static readonly string TermsOfUse = "Terms-Of-Use";
            public static readonly string PrivacyPolicy = "Privacy-Policy";
            public static readonly string Team = "Team";
        }

        public static class StatisticsDimensions
        {
            public const string Version = "Version";
            public const string ClientName = "ClientName";
            public const string ClientVersion = "ClientVersion";
            public const string Operation = "Operation";
        }
    }
}
