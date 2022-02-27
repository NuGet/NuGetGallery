// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;

namespace Stats.PostProcessReports
{
    /// <summary>
    /// Blob processing statistics.
    /// </summary>
    public class BlobStatistics
    {
        private int _filesCreated = 0;
        private int _linesFailed = 0;

        public int TotalLineCount { get; set; }
        public int FilesCreated => _filesCreated;
        public int LinesFailed => _linesFailed;

        /// <summary>
        /// Thread-safe <see cref="FilesCreated"/> increment.
        /// </summary>
        public void IncrementFileCreated()
        {
            Interlocked.Increment(ref _filesCreated);
        }

        /// <summary>
        /// Thread-safe <see cref="LinesFailed"/> increment.
        /// </summary>
        public void IncrementLinesFailed()
        {
            Interlocked.Increment(ref _linesFailed);
        }
    };
}
