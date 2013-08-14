﻿using System;

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