// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    [Serializable]
    public sealed class FileAlreadyExistsException : Exception
    {
        public FileAlreadyExistsException() { }
        public FileAlreadyExistsException(string message) : base(message) { }
        public FileAlreadyExistsException(string message, Exception inner) : base(message, inner) { }
    }
}