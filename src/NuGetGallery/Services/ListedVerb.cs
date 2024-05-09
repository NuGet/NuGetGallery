// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public enum ListedVerb
    {
        /// <summary>
        /// Leave the current listed status as is (either unlisted or listed).
        /// </summary>
        Unchanged,

        /// <summary>
        /// Change the listed status to unlisted, allowing for no-op if the package is already unlisted.
        /// </summary>
        Unlist,

        /// <summary>
        /// Change the listed status to listed, allowing for no-op if the package is already listed.
        /// </summary>
        Relist,
    }
}