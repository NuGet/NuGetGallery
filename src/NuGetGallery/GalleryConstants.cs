// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;
using System;

namespace NuGetGallery
{
    public static class GalleryConstants
    {
        public const string AlphabeticSortOrder = "package-title";
        public const int DefaultPackageListPageSize = 20;
        public const string DefaultPackageListSortOrder = "package-download-count";
        public const int PasswordResetTokenExpirationHours = 1;

        public const int ColumnsAuthenticationSm = 6;
        public const int ColumnsAuthenticationMd = 4;
        public const int ColumnsWideAuthenticationSm = 8;
        public const int ColumnsWideAuthenticationMd = 6;
        public const int ColumnsFormMd = 12;

        public const int VisibleVersions = 5;

        public const int GravatarElementSize = 32;
        public const int GravatarImageSize = GravatarElementSize * 2;
        public const int GravatarImageSizeLarge = 332;
        public const int GravatarCacheDurationSeconds = 300;

        public const int MaxEmailSubjectLength = 255;
        internal static readonly NuGetVersion MaxSupportedMinClientVersion = new NuGetVersion("5.3.0.0");
        public const string PackageFileDownloadUriTemplate = "packages/{0}/{1}/download";

        public const string ReadMeFileSavePathTemplateActive = "active/{0}/{1}{2}";
        public const string ReadMeFileSavePathTemplatePending = "pending/{0}/{1}{2}";

        public const string PopularitySortOrder = "package-download-count";
        public const string RecentSortOrder = "package-created";
        public const string RelevanceSortOrder = "relevance";

        public const string PBKDF2HashAlgorithmId = "PBKDF2";

        public const string UploadFileNameTemplate = "{0}{1}";
        public const string NuGetCommandLinePackageId = "NuGet.CommandLine";

        public static readonly string ReturnUrlViewDataKey = "ReturnUrl";
        public static readonly string ReturnUrlMessageViewDataKey = "ReturnUrlMessage";
        public const string AbsoluteLatestUrlString = "absoluteLatest";
        public const string AskUserToEnable2FA = "AskUserToEnable2FA";

        public const string UrlValidationRegEx = @"(https?):\/\/[^ ""]+$";
        public const string UrlValidationErrorMessage = "This doesn't appear to be a valid HTTP/HTTPS URL";

        public const string PackageBaseAddress = "PackageBaseAddress/3.0.0";

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

        public const string EmailValidationErrorMessage = "This doesn't appear to be a valid email address.";

        public const string UsernameValidationRegex =
            @"[A-Za-z0-9][A-Za-z0-9_\.-]+[A-Za-z0-9]";

        public const string UsernameValidationErrorMessage =
            "User names must start and end with a letter or number, and may only contain letters, numbers, underscores, periods, and hyphens in between.";

        internal const string WarningHeaderName = "X-NuGet-Warning";
        
        /// <summary>
        /// This header is for internal use only. It indicates whether an OData query is "custom". Custom means not
        /// not optimized for search hijacking. Queries made by the official client should be optimized and therefore
        /// not marked as custom queries (as to not overload the database). The value is either "true" or "false". If
        /// the header is not present on an OData query response, that means that the search hijack detection is
        /// failing, perhaps due to search service outage. The value of this header corresponds to the
        /// <see cref="ITelemetryService.TrackODataCustomQuery(bool?)"/> telemetry emitted while generating the
        /// response.
        /// </summary>
        internal const string CustomQueryHeaderName = "X-NuGet-CustomQuery";

        public static readonly string ReturnUrlParameterName = "ReturnUrl";

        public const string LicenseDeprecationUrl = "https://aka.ms/deprecateLicenseUrl";

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

        public static class FAQLinks
        {
            public const string NuGetFAQ = "https://aka.ms/nuget-faq";
            public const string MSALinkedToAnotherAccount = "https://aka.ms/nuget-faq-msa-linked-another-account";
            public const string EmailLinkedToAnotherMSAAccount = "https://aka.ms/nuget-faq-email-linked-another-msa";
            public const string NuGetAccountManagement = "https://aka.ms/nuget-faq-account-management";
            public const string NuGetChangeUsername = "https://aka.ms/nuget-faq-change-username";
            public const string NuGetDeleteAccount = "https://aka.ms/nuget-faq-delete-account";
            public const string TransformToOrganization = "https://aka.ms/nuget-faq-transform-org";
        }
    }
}
