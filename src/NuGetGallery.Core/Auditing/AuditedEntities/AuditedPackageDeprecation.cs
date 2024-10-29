// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGetGallery.Auditing.Obfuscation;
using System;

namespace NuGetGallery.Auditing.AuditedEntities
{
    public class AuditedPackageDeprecation
    {
        public int Key { get; private set; }
        public int PackageKey { get; private set; }
        public int Status { get; private set; }
        public int? AlternatePackageRegistrationKey { get; private set; }
        public int? AlternatePackageKey { get; private set; }
        [Obfuscate(ObfuscationType.UserKey)]
        public int? DeprecatedByUserKey { get; private set; }
        public DateTime DeprecatedOn { get; private set; }
        public string CustomMessage { get; private set; }

        public static AuditedPackageDeprecation CreateFrom(PackageDeprecation packageDeprecation)
        {
            return new AuditedPackageDeprecation
            {
                Key = packageDeprecation.Key,
                PackageKey = packageDeprecation.PackageKey,
                Status = (int)packageDeprecation.Status,
                AlternatePackageRegistrationKey = packageDeprecation.AlternatePackageRegistrationKey,
                AlternatePackageKey = packageDeprecation.AlternatePackageKey,
                DeprecatedByUserKey = packageDeprecation.DeprecatedByUserKey,
                DeprecatedOn = packageDeprecation.DeprecatedOn,
                CustomMessage = packageDeprecation.CustomMessage
            };
        }
    }
}
