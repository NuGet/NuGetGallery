// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Provides the data to generate paths to store content files (licenses, icons, documentation)
    /// </summary>
    public interface IContentFileMetadataService
    {
        /// <summary>
        /// The name of the public folder where bits of package content can be extracted to.
        /// </summary>
        string PackageContentFolderName { get; }

        /// <summary>
        /// The template for the path to save user content. File name will be appended to it.
        /// </summary>
        string PackageContentPathTemplate { get; }
    }
}
