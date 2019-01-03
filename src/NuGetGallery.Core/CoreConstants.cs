// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public static class CoreConstants
    {
        public const string AdminRoleName = "Admins";

        public const string PackageFileSavePathTemplate = "{0}.{1}{2}";
        public const string PackageFileBackupSavePathTemplate = "{0}/{1}/{2}.{3}";
        public const string PackageContentFileSavePathTemplate = "{0}/{1}";

        public const string NuGetPackageFileExtension = ".nupkg";
        public const string CertificateFileExtension = ".cer";

        public const string Sha512HashAlgorithmId = "SHA512";

        public const string PackageContentType = "binary/octet-stream";
        public const string OctetStreamContentType = "application/octet-stream";
        public const string TextContentType = "text/plain";
        public const string MarkdownContentType = "text/markdown"; // rfc7763
        public const string CertificateContentType = "application/pkix-cert";
        public const string JsonContentType = "application/json";

        public const string DefaultCacheControl = "max-age=120";

        public static class Folders
        {
            public const string UserCertificatesFolderName = "user-certificates";
            public const string ContentFolderName = "content";
            public const string DownloadsFolderName = "downloads";
            public const string PackageBackupsFolderName = "package-backups";
            public const string PackageReadMesFolderName = "readmes";
            public const string PackagesFolderName = "packages";
            public const string PackagesContentFolderName = "packages-content";
            public const string UploadsFolderName = "uploads";
            public const string ValidationFolderName = "validation";
            public const string RevalidationFolderName = "revalidation";
            public const string StatusFolderName = "status";
            public const string SymbolPackagesFolderName = "symbol-packages";
            public const string SymbolPackageBackupsFolderName = "symbol-package-backups";
            public const string FlatContainerFolderName = "v3-flatcontainer";
            public const string FeatureFlagsContainerFolderName ="feature-flags";
        }

        public const string NuGetSymbolPackageFileExtension = ".snupkg";

        public const string UploadTracingKeyHeaderName = "upload-id";

        public const string LicenseFileName = "license";

        public const string FeatureFlagsFileName = "flags.json";
    }
}