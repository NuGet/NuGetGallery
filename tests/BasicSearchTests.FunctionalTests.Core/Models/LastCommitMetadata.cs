// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace BasicSearchTests.FunctionalTests.Core.Models
{
    public class LastCommitMetadata
    {
        public DateTime CommitTimeStamp { get; set; }
        
        public int Count { get; set; }

        public string Description { get; set; }

        public string Trace { get; set; }
    }
}