// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    public sealed class DisposableList<T> : List<T>, IDisposable
        where T : IDisposable
    {
        private bool _isDisposed;

        public void Dispose()
        {
            if (!_isDisposed)
            {
                foreach (var item in this)
                {
                    item.Dispose();
                }

                Clear();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }
    }
}
