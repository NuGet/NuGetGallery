// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Lucene.Net.Analysis;
using Lucene.Net.Index;
using Lucene.Net.Store;

namespace NgTests
{
    public class StuckIndexWriter : IndexWriter
    {
        private readonly TimeSpan _stuckDuration;

        private StuckIndexWriter(Directory directory, Analyzer analyzer, TimeSpan stuckDuration)
            : base(directory, analyzer, MaxFieldLength.UNLIMITED)
        {
            _stuckDuration = stuckDuration;
        }

        public static IndexWriter FromIndexWriter(IndexWriter originalWriter, TimeSpan stuckDuration)
        {
            var directory = originalWriter.Directory;
            var analyzer = originalWriter.Analyzer;
            originalWriter.Dispose();

            return new StuckIndexWriter(directory, analyzer, stuckDuration);
        }

        public override void ExpungeDeletes()
        {
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < _stuckDuration)
            {
            }

            stopwatch.Stop();
        }
    }
}
