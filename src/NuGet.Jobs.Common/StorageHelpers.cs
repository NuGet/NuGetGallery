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
    }
}

