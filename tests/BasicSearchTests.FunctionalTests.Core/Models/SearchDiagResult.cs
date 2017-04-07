// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace BasicSearchTests.FunctionalTests.Core.Models
{
    public class SearchDiagResult
    {
        public int NumDocs { get; set; }

        public LastCommitMetadata CommitUserData { get; set; }

        public DateTime? LastReopen { get; set; }

        public string IndexName { get; set; }

        public string MachineName { get; set; }

        public DateTime? LastIndexReloadTime { get; set; }

        public long LastIndexReloadDurationInMilliseconds { get; set; }

        public DateTime? LastAuxiliaryDataLoadTime { get; set; }

        public AuxiliaryFilesUpdateTime LastAuxiliaryDataUpdateTime { get; set; }
    }
}