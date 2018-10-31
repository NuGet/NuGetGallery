// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    [Serializable]
    public sealed class PackageAlreadyExistsException : Exception
    {
        public PackageAlreadyExistsException() { }
        public PackageAlreadyExistsException(string message) : base(message) { }
        public PackageAlreadyExistsException(string message, Exception inner) : base(message, inner) { }
    }
}