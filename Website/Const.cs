
namespace NuGetGallery {
    public static class Const {
        public const int MaxEmailSubjectLength = 255;
        public const string PackageContentType = "application/zip";
        public const string PackageFileExtension = ".nupkg";
        public const string PackageFileDownloadUriTemplate = "packages/{0}/{1}/download";
        public const string PackageFileSavePathTemplate = "{0}.{1}{2}";
        public const string Sha512HashAlgorithmId = "SHA512";
    }
}