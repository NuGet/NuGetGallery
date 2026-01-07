// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;

namespace NuGet.Services.KeyVault
{
    /// <summary>
    /// The purpose of this class is to allow a <see cref="RefreshableSecretReaderFactory"/> to dynamically control the
    /// settings of the <see cref="RefreshableSecretReader"/> instance that it creates. This does not follow the 
    /// Microsoft.Extensions.Options (e.g. IOptionsSnapshot, IOptions) pattern because this is meant to initialized and
    /// modified at runtime.
    /// </summary>
    public class RefreshableSecretReaderSettings
    {
        /// <summary>
        /// Prevent <see cref="RefreshableSecretReader.GetSecretAsync(string)"/> or
        /// <see cref="RefreshableSecretReader.GetSecretObjectAsync(string)"/> from getting secrets from the underlying
        /// secret reader. If one of these methods is executed and the provided secret is not found, an
        /// <see cref="InvalidOperationException"/> will be thrown. In a web application, this should be enabled during
        /// startup so that requests encounter an exception instead of reading a secret from KeyVault in a context that
        /// may cause a deadlock. It's better to throw an exception than deadlock.
        /// </summary>
        public bool BlockUncachedReads { get; set; }
    }
}