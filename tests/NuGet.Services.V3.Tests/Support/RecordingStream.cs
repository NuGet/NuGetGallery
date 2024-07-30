// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Services
{
    public class RecordingStream : MemoryStream
    {
        private readonly object _lock = new object();
        private Action<byte[]> _onDispose;

        public RecordingStream(Action<byte[]> onDispose)
        {
            _onDispose = onDispose;
        }

        protected override void Dispose(bool disposing)
        {
            lock (_lock)
            {
                if (_onDispose != null)
                {
                    _onDispose(ToArray());
                    _onDispose = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
