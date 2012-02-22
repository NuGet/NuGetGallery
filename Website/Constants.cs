
namespace NuGetGallery
{
    public static class Constants
    {
        public const string AdminRoleName = "Admins";
        public const string AlphabeticSortOrder = "package-title";
        public const int DefaultPackageListPageSize = 20;
        public const string DefaultPackageListSortOrder = "package-download-count";
        public const int DefaultPasswordResetTokenExpirationHours = 24;
        public const int MaxEmailSubjectLength = 255;
        public const string PackageContentType = "application/zip";
        public const string NuGetPackageFileExtension = ".nupkg";
        public const string PackageFileDownloadUriTemplate = "packages/{0}/{1}/download";
        public const string PackageFileSavePathTemplate = "{0}.{1}{2}";
        public const string PackagesFolderName = "packages";
        public const string PopularitySortOrder = "package-download-count";
        public const string RecentSortOrder = "package-created";
        public const string RelevanceSortOrder = "relevance";
        public const string Sha1HashAlgorithmId = "SHA1";
        public const string Sha512HashAlgorithmId = "SHA512";
        public const string PBKDF2HashAlgorithmId = "PBKDF2";
        public const string UploadFileNameTemplate = "{0}{1}";
        public const string UploadsFolderName = "uploads";
    }
}