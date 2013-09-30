﻿using System;

namespace NuGetGallery
{
    [Serializable]
    public class BlobCopyFailedException : Exception
    {
        public BlobCopyFailedException(string message)
            : base(message)
        {
        }
    }
}