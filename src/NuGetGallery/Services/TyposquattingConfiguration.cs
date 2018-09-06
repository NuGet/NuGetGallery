// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGetGallery.Services
{
    public sealed class TyposquattingConfiguration : ITyposquattingConfiguration
    {
        private const int DefaultPackageIdCheckListLength = 20000;
        public int PackageIdChecklistLength { get; }
        public bool IsCheckEnabled { get; }
        public bool IsBlockUsersEnabled { get; }
        public TyposquattingConfiguration()
            : this(packageIdChecklistLength: DefaultPackageIdCheckListLength,
                  isCheckEnabled: false,
                  isBlockUsersEnabled: false)
        {
        }

        [JsonConstructor]
        public TyposquattingConfiguration(
            int packageIdChecklistLength,
            bool isCheckEnabled,
            bool isBlockUsersEnabled)
        {
            PackageIdChecklistLength = packageIdChecklistLength;
            IsCheckEnabled = isCheckEnabled;
            IsBlockUsersEnabled = isBlockUsersEnabled;
        }
    }
}