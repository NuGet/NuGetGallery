// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch
{
    public static class AsDisposable
    {
        public static AsDisposable<T> Create<T>(T value) where T : class
        {
            return new AsDisposable<T>(value);
        }
    }

    /// <summary>
    /// This is invented because <see cref="IEntitiesContext"/> does not implement <see cref="IDisposable"/> but
    /// the primary implementation is disposable.
    /// </summary>
    public class AsDisposable<T> : IDisposable where T : class
    {
        public AsDisposable(T value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public T Value { get; }

        public void Dispose()
        {
            (Value as IDisposable)?.Dispose();
        }
    }
}
