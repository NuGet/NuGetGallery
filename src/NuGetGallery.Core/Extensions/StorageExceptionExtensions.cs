// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using Microsoft.WindowsAzure.Storage;

namespace NuGetGallery
{
    public static class StorageExceptionExtensions
    {
        public static bool IsFileAlreadyExistsException(this StorageException e)
        {
            return e?.RequestInformation?.HttpStatusCode == (int?)HttpStatusCode.Conflict;
        }

        public static bool IsPreconditionFailedException(this StorageException e)
        {
            return e?.RequestInformation?.HttpStatusCode == (int?)HttpStatusCode.PreconditionFailed;
        }
    }
}
