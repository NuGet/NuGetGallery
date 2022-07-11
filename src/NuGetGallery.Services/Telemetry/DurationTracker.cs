// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace NuGetGallery
{
    public class DurationTracker : IDisposable
    {
        private readonly Stopwatch _timer;

        private Action<TimeSpan> _trackAction;

        public DurationTracker(
            Action<TimeSpan> trackAction)
        {
            _trackAction = trackAction ?? throw new ArgumentNullException(nameof(trackAction));
            _timer = Stopwatch.StartNew();
        }

        public void Dispose() => _trackAction(_timer.Elapsed);
    }
}
