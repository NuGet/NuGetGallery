// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class LuceneCommitMetadata
    {
        public DateTime CommitTimeStamp { get; set; }
        public string Description { get; set; }
        public int Count { get; set; }
        public string Trace { get; set; }

        public LuceneCommitMetadata(DateTime commitTimeStamp, string description, int count, string trace)
        {
            CommitTimeStamp = commitTimeStamp;
            Description = description;
            Count = count;
            Trace = trace;
        }

        public IDictionary<string, string> ToDictionary()
        {
            IDictionary<string, string> commitMetadata = new Dictionary<string, string>();
            commitMetadata.Add("commitTimeStamp", CommitTimeStamp.ToString("O"));
            commitMetadata.Add("commit-time-stamp", CommitTimeStamp.ToString("O"));
            commitMetadata.Add("description", Description);
            commitMetadata.Add("count", Count.ToString());
            commitMetadata.Add("trace", Trace);
            return commitMetadata;
        }
    }
}