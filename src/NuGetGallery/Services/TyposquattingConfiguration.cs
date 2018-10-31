// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGetGallery.Services
{
    public sealed class TyposquattingConfiguration : ITyposquattingConfiguration
    {
        private const int DefaultPackageIdCheckListLength = 20000;
        private const double DefaultPackageIdChecklistCacheExpireTimeInHours = 24;
        public int PackageIdChecklistLength { get; }
        public bool IsCheckEnabled { get; }
        public bool IsBlockUsersEnabled { get; }
        public double PackageIdChecklistCacheExpireTimeInHours { get; }

        public TyposquattingConfiguration()
            : this(packageIdChecklistLength: DefaultPackageIdCheckListLength,
                  isCheckEnabled: false,
                  isBlockUsersEnabled: false,
                  packageIdChecklistCacheExpireTimeInHours: DefaultPackageIdChecklistCacheExpireTimeInHours)
        {
        }

        [JsonConstructor]
        public TyposquattingConfiguration(
            int packageIdChecklistLength,
            bool isCheckEnabled,
            bool isBlockUsersEnabled,
            double packageIdChecklistCacheExpireTimeInHours)
        {
            PackageIdChecklistLength = packageIdChecklistLength;
            IsCheckEnabled = isCheckEnabled;
            IsBlockUsersEnabled = isBlockUsersEnabled;
            PackageIdChecklistCacheExpireTimeInHours = packageIdChecklistCacheExpireTimeInHours;
        }
    }
}