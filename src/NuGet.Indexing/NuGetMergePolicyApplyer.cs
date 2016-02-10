// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Index;

namespace NuGet.Indexing
{
    public class NuGetMergePolicyApplyer
    {
        //  Define the size of a file in a level (exponentially) and the count of files that constitue a level
        public const int MergeFactor = 10;

        // Except never merge segments that have more docs than this
        public const int MaxMergeDocs = 7999;

        // What is the size a segment must be in order to be merged?
        // Don't set this too high or we'll end up with lots of small segments.
        // ReSharper disable once InconsistentNaming
        public const double MinMergeMB = 1.0;

        // From what segment size do we want to ignore merges?
        // (or put differently: if the max segment size we want is 100 MB, MaxMergeMB should be 100 MB / 2)
        // Note this does not apply when calling .Optimize()
        // ReSharper disable once InconsistentNaming
        public const double MaxMergeMB = 50.0;

        public static void ApplyTo(IndexWriter writer)
        {
            writer.MergeFactor = MergeFactor;
            writer.MaxMergeDocs = MaxMergeDocs;

            var mergePolicy = new LogByteSizeMergePolicy(writer)
            {
                MaxMergeDocs = MaxMergeDocs,
                MergeFactor = MergeFactor,
                MinMergeMB = MinMergeMB,
                MaxMergeMB = MaxMergeMB
            };
            writer.SetMergePolicy(mergePolicy);
        }           
    }
}