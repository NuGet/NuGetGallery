// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.KeyVault
{
    /// <summary>
    /// Wraps existing secret reader factory to provide a caching layer for the <see cref="ISecretReader"/>.
    /// </summary>
    public class CachingSecretReaderFactory : ISecretReaderFactory
    {
        private readonly ISecretReaderFactory _underlyingFactory;
        private readonly TimeSpan _cachingTimeout;

        /// <summary>
        /// Initializes the instance.
        /// </summary>
        /// <param name="underlyingFactory">Actual factory we are wrapping</param>
        /// <param name="cachingTimeout">The max caching time for secrets</param>
        public CachingSecretReaderFactory(ISecretReaderFactory underlyingFactory, TimeSpan cachingTimeout)
        {
            _underlyingFactory = underlyingFactory ?? throw new ArgumentNullException(nameof(underlyingFactory));
            _cachingTimeout = cachingTimeout;
        }

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
            => _underlyingFactory.CreateSecretInjector(secretReader);

        public ISecretReader CreateSecretReader()
            => new CachingSecretReader(_underlyingFactory.CreateSecretReader(), (int)_cachingTimeout.TotalSeconds);
    }
}
