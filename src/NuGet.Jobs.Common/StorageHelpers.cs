// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.Net;

namespace NuGet.Jobs
{
    public static class StorageHelpers
    {
        private const string _packageBackupsDirectory = "packages";
        private const string _packageBlobNameFormat = "{0}.{1}.nupkg";
        private const string _packageBackupBlobNameFormat = _packageBackupsDirectory + "/{0}/{1}/{2}.nupkg";

        private const string ReadMeBlobTemplate = "{0}/{1}/{2}{3}";
        private const string PendingReadMeFolder = "pending";
        private const string ActiveReadMeFolder = "active";

        public static string GetPackageBlobName(string id, string version)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                _packageBlobNameFormat,
                id,
                version).ToLowerInvariant();
        }

        public static string GetPackageBackupBlobName(string id, string version, string hash)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                _packageBackupBlobNameFormat,
                id.ToLowerInvariant(),
                version.ToLowerInvariant(),
                WebUtility.UrlEncode(hash));
        }

        private static string GetReadMeBlobPath(string folder, string id, string version, string extension)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                ReadMeBlobTemplate,
                folder,
                id.ToLowerInvariant(),
                version.ToLowerInvariant(),
                extension);
        }

        public static string GetPendingReadMeBlobNamePath(string id, string version, string extension)
        {
            return GetReadMeBlobPath(PendingReadMeFolder, id, version, extension);
        }

        public static string GetActiveReadMeBlobNamePath(string id, string version, string extension)
        {
            return GetReadMeBlobPath(ActiveReadMeFolder, id, version, extension);
        }
    }
}

