// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// A copy of <see cref="Microsoft.WindowsAzure.Storage.Blob.SharedAccessBlobPermissions"/> so that the caller
    /// does not need a direct dependency on the WindowsAzure.Storage package.
    /// </summary>
    [Flags]
    public enum FileUriPermissions
    {
        [Obsolete]
        None = 0,

        Read = 1,

        Write = 2,

        Delete = 4,

        [Obsolete]
        List = 8,

        [Obsolete]
        Add = 16,

        Create = 32,
    }
}
