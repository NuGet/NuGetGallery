// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Shared
{
    /// <summary>
    /// The result of saving content in file storage, it includes saving feature flags, user log in authentication.
    /// </summary>
    public enum ContentSaveResult
    {
        /// <summary>
        /// The content were saved successfully.
        /// </summary>
        Ok,

        /// <summary>
        /// The content were modified by someone else.
        /// </summary>
        Conflict,
    }
}