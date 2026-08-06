// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IStagingBlobService
    {
        Task<StagingBlobReference> CreateAsync(Stream content, StagingBlobType blobType);

        Task<Stream> OpenReadAsync(StagingBlobReference blob);

        Task CopyAsync(
            StagingBlobReference source,
            string destinationFolderName,
            string destinationFileName,
            IAccessCondition destinationAccessCondition);

        Task DeleteAsync(string blobPath, string expectedETag);
    }
}
