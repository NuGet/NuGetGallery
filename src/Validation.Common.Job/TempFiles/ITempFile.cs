// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation
{
    /// <summary>
    /// A wrapper for a temp file that tries to delete it on dispose
    /// </summary>
    public interface ITempFile : IDisposable
    {
        /// <summary>
        /// Full path to a temporary file
        /// </summary>
        string FullName { get; }
    }
}
