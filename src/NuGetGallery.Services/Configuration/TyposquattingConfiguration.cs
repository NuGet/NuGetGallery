// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGetGallery.Services
{
    public sealed class TyposquattingConfiguration : ITyposquattingConfiguration
    {
        public const int DefaultPackageIdCheckListLength = 10000;
        public const double DefaultPackageIdChecklistCacheExpireTimeInHours = 24.0;
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
            PackageIdChecklistLength = packageIdChecklistLength == default(int) ? DefaultPackageIdCheckListLength : packageIdChecklistLength;
            IsCheckEnabled = isCheckEnabled;
            IsBlockUsersEnabled = isBlockUsersEnabled;
            PackageIdChecklistCacheExpireTimeInHours = packageIdChecklistCacheExpireTimeInHours == default(double) ? DefaultPackageIdChecklistCacheExpireTimeInHours : packageIdChecklistCacheExpireTimeInHours;
        }
    }
}