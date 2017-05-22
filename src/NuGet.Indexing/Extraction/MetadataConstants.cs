// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Indexing
{
    public static class MetadataConstants
    {
        public static class LuceneMetadata
        {
            // Id Properties
            public const string IdPropertyName = "Id";
            public const string IdAutocompletePropertyName = "IdAutocomplete";
            public const string ShingledIdPropertyName = "ShingledId";
            public const string TokenizedIdPropertyName = "TokenizedId";

            // Version Properties
            public const string NormalizedVersionPropertyName = "Version";
            public const string FullVersionPropertyName = "FullVersion";
            public const string VerbatimVersionPropertyName = "OriginalVersion";

            // Date Properties
            public const string LastEditedDatePropertyName = "LastEditedDate";
            public const string OriginalCreatedPropertyName = "OriginalCreated";
            public const string OriginalPublishedPropertyName = "OriginalPublished";
            public const string OriginalLastEditedPropertyName = "OriginalLastEdited";
            public const string PublishedDatePropertyName = "PublishedDate";

            // Other Properties
            public const string AuthorsPropertyName = "Authors";
            public const string CopyrightPropertyName = "Copyright";
            public const string DependenciesPropertyName = "Dependencies";
            public const string DescriptionPropertyName = "Description";
            public const string FlattenedDependenciesPropertyName = "FlattenedDependencies";
            public const string IconUrlPropertyName = "IconUrl";
            public const string LanguagePropertyName = "Language";
            public const string LicenseUrlPropertyName = "LicenseUrl";
            public const string ListedPropertyName = "Listed";
            public const string MinClientVersionPropertyName = "MinClientVersion";
            public const string PackageHashPropertyName = "PackageHash";
            public const string PackageHashAlgorithmPropertyName = "PackageHashAlgorithm";
            public const string PackageSizePropertyName = "PackageSize";
            public const string ProjectUrlPropertyName = "ProjectUrl";
            public const string ReleaseNotesPropertyName = "ReleaseNotes";
            public const string RequiresLicenseAcceptancePropertyName = "RequiresLicenseAcceptance";
            public const string SemVerLevelPropertyName = "SemVerLevel";
            public const string SortableTitlePropertyName = "SortableTitle";
            public const string SummaryPropertyName = "Summary";
            public const string SupportedFrameworksPropertyName = "SupportedFrameworks";
            public const string TagsPropertyName = "Tags";
            public const string TitlePropertyName = "Title";
        }

        // These are here to account for the minor variations when extracting data from various sources.
        public static class NuPkgMetadata
        {
            public const string VersionPropertyName = "version";
        }

        public static class CatalogMetadata
        {
            public const string RequiresLicenseAcceptancePropertyName = "requireLicenseAcceptance";
        }

        // Shared Property names
        public const string AuthorsPropertyName = "authors";
        public const string CopyrightPropertyName = "copyright";
        public const string CreatedPropertyName = "created";
        public const string DescriptionPropertyName = "description";
        public const string FlattenedDependenciesPropertyName = "flattenedDependencies";
        public const string IconUrlPropertyName = "iconUrl";
        public const string IdPropertyName = "id";
        public const string LanguagePropertyName = "language";
        public const string LastEditedPropertyName = "lastEdited";
        public const string LicenseUrlPropertyName = "licenseUrl";
        public const string ListedPropertyName = "listed";
        public const string MinClientVersionPropertyName = "minClientVersion";
        public const string NormalizedVersionPropertyName = "version";
        public const string PackageHashPropertyName = "packageHash";
        public const string PackageHashAlgorithmPropertyName = "packageHashAlgorithm";
        public const string PackageSizePropertyName = "packageSize";
        public const string ProjectUrlPropertyName = "projectUrl";
        public const string PublishedPropertyName = "published";
        public const string ReleaseNotesPropertyName = "releaseNotes";
        public const string RequiresLicenseAcceptancePropertyName = "requiresLicenseAcceptance";
        public const string SemVerLevelKeyPropertyName = "semVerLevelKey";
        public const string SummaryPropertyName = "summary";
        public const string SupportedFrameworksPropertyName = "supportedFrameworks";
        public const string TagsPropertyName = "tags";
        public const string TitlePropertyName = "title";
        public const string VerbatimVersionPropertyName = "verbatimVersion";

        // Constant Values
        public const string DateTimeZeroStringValue = "01/01/0001 00:00:00";
        public const string SemVerLevel2Value = "2";
        public const string HashAlgorithmValue = "SHA512";
    }
}
