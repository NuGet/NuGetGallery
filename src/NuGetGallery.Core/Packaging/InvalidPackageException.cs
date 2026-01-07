// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Packaging
{
    [Serializable]
    public class InvalidPackageException : Exception
    {
        public InvalidPackageException() { }
        public InvalidPackageException(string message) : base(message) { }
        public InvalidPackageException(string message, Exception inner) : base(message, inner) { }
        protected InvalidPackageException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
