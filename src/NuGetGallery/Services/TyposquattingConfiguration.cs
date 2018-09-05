// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGetGallery.Services
{
    public sealed class TyposquattingConfiguration : ITyposquattingConfiguration
    {
        public int ChecklistLength { get; }
        public bool IsCheckEnabled { get; }
        public bool IsBlockUsersEnabled { get; }
        public TyposquattingConfiguration()
            : this(checklistLength: 20000,
                  isCheckEnabled: false,
                  isBlockUsersEnabled: false)
        {
        }

        [JsonConstructor]
        public TyposquattingConfiguration(
            int checklistLength,
            bool isCheckEnabled,
            bool isBlockUsersEnabled)
        {
            ChecklistLength = checklistLength;
            IsCheckEnabled = isCheckEnabled;
            IsBlockUsersEnabled = isBlockUsersEnabled;
        }
    }
}