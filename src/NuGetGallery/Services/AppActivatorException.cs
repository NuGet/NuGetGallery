// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;

namespace NuGetGallery
{
    /// <summary>
    /// Exception thrown when application startup fails.
    /// </summary>
    [Serializable]
    public class AppActivatorException : Exception
    {
        public AppActivatorException() { }
        public AppActivatorException(string message) : base(message) { }
        public AppActivatorException(string message, Exception inner) : base(message, inner) { }
        protected AppActivatorException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        { }
    }
}