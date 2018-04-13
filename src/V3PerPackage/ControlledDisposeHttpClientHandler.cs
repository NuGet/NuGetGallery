// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;

namespace NuGet.Services.V3PerPackage
{
    public class ControlledDisposeHttpClientHandler : HttpClientHandler
    {
        protected override void Dispose(bool disposing)
        {
            // Ignore the existing dispose signal. This is so that the handler can be used even if some bad product
            // code disposed it.
        }

        private void ForceDispose()
        {
            base.Dispose(disposing: true);
        }

        public IDisposable GetDisposable()
        {
            return new Disposable(this);
        }

        private class Disposable : IDisposable
        {
            private readonly ControlledDisposeHttpClientHandler _self;

            public Disposable(ControlledDisposeHttpClientHandler self)
            {
                _self = self;
            }

            public void Dispose()
            {
                _self.ForceDispose();
            }
        }
    }
}
