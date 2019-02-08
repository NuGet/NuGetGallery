// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Features
{
    /// <summary>
    /// The result of calling <see cref="FeatureFlagFileStorageService.TrySaveAsync(string, string)"/>.
    /// </summary>
    public enum FeatureFlagSaveResultType
    {
        /// <summary>
        /// The flags were saved successfully.
        /// </summary>
        Ok,

        /// <summary>
        /// The flags are malformed.
        /// </summary>
        Invalid,

        /// <summary>
        /// The flags were modified by someone else.
        /// </summary>
        Conflict,
    }
}
