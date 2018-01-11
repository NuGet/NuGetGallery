// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing.AuditedEntities
{
    public class AuditedPackageRegistration
    {
        public string Id { get;  set; }
        public int DownloadCount { get;  set; }
        public int Key { get;  set; }
        public bool IsVerified { get;  set; }

        public static AuditedPackageRegistration CreateFrom(PackageRegistration packageRegistration)
        {
            return new AuditedPackageRegistration
            {
                Id = packageRegistration.Id,
                DownloadCount = packageRegistration.DownloadCount,
                Key = packageRegistration.Key,
                IsVerified = packageRegistration.IsVerified
            };
        }
    }
}