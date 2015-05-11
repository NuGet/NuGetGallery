// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;

namespace NuGetGallery.Operations.Tasks
{
    [Serializable]
    public class BlobBackupFailedException : Exception
    {
        public BlobBackupFailedException(string message)
            : base(message)
        {
        }
    }
} 