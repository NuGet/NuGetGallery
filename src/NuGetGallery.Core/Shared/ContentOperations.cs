// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Shared
{
    /// <summary>
    /// The operation of content in file storage, operation like add user to enable password authentication for log in.
    /// </summary>
    public enum ContentOperations
    {
        /// <summary>
        /// Add the content.
        /// </summary>
        Add,

        /// <summary>
        /// Remove the content.
        /// </summary>
        Remove,
    }
}
