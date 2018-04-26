// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation
{
    /// <summary>
    /// A temp file interface that provides stream for reading its content.
    /// </summary>
    public interface ITempReadOnlyFile : ITempFile
    {
        Stream ReadStream { get; }

        /// <summary>
        /// Reads the remaining content of the <see cref="ReadStream"/> as string and returns it.
        /// </summary>
        Task<string> ReadToEndAsync();
    }
}
