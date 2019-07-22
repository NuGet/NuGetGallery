// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit.Sdk;

namespace NuGetGallery
{
    public class FolderNamesDataAttribute : DataAttribute
    {
        public FolderNamesDataAttribute(bool includePermissions = false, bool includeContentTypes = false)
        {
            IncludePermissions = includePermissions;
            IncludeContentTypes = includeContentTypes;
        }

        private bool IncludePermissions { get; }

        private bool IncludeContentTypes { get; }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            var folderNames = new List<object[]>
                {
                    // Folder name, is public, content type
                    new object[] { CoreConstants.Folders.ContentFolderName, false, CoreConstants.JsonContentType, },
                    new object[] { CoreConstants.Folders.DownloadsFolderName, true, CoreConstants.OctetStreamContentType },
                    new object[] { CoreConstants.Folders.PackageBackupsFolderName, true, CoreConstants.PackageContentType },
                    new object[] { CoreConstants.Folders.PackageReadMesFolderName, false, CoreConstants.TextContentType },
                    new object[] { CoreConstants.Folders.PackagesFolderName, true, CoreConstants.PackageContentType },
                    new object[] { CoreConstants.Folders.SymbolPackagesFolderName, true, CoreConstants.PackageContentType },
                    new object[] { CoreConstants.Folders.SymbolPackageBackupsFolderName, true, CoreConstants.PackageContentType },
                    new object[] { CoreConstants.Folders.UploadsFolderName, false, CoreConstants.PackageContentType },
                    new object[] { CoreConstants.Folders.UserCertificatesFolderName, false, CoreConstants.CertificateContentType },
                    new object[] { CoreConstants.Folders.ValidationFolderName, false, CoreConstants.PackageContentType },
                    new object[] { CoreConstants.Folders.PackagesContentFolderName, false, CoreConstants.OctetStreamContentType },
                    new object[] { CoreConstants.Folders.RevalidationFolderName, false, CoreConstants.JsonContentType },
                    new object[] { CoreConstants.Folders.StatusFolderName, false, CoreConstants.JsonContentType },
                    new object[] { CoreConstants.Folders.FlatContainerFolderName, false, CoreConstants.PackageContentType },
                };

            if (!IncludePermissions && !IncludeContentTypes)
            {
                folderNames = folderNames
                    .Select(fn => new[] { fn.ElementAt(0) })
                    .ToList();
            }
            else if (IncludePermissions && !IncludeContentTypes)
            {
                folderNames = folderNames
                    .Select(fn => new[] { fn[0], fn[1] })
                    .ToList();
            }
            else if (!IncludePermissions && IncludeContentTypes)
            {
                folderNames = folderNames
                    .Select(fn => new[] { fn[0], fn[2] })
                    .ToList();
            }

            return folderNames;
        }
    }
}
