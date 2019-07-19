// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace NuGetGallery.Diagnostics
{

    /// <summary>
    /// ILogger implementation based on https://github.com/aspnet/Logging/tree/master/src/Microsoft.Extensions.Logging.TraceSource 
    /// </summary>
    public class TraceDiagnosticsSourceScope : IDisposable
    {
        private bool _isDisposed;

        public TraceDiagnosticsSourceScope(object state)
        {
            Trace.CorrelationManager.StartLogicalOperation(state);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                Trace.CorrelationManager.StopLogicalOperation();
                _isDisposed = true;
            }
        }
    }
}