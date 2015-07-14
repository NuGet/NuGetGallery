// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Stats.AzureCdnLogs.Common
{
    [DataContract]
    public class PackageStatisticsQueueMessage
    {
        [IgnoreDataMember]
        public string Id { get; set; }

        [IgnoreDataMember]
        public string PopReceipt { get; set; }

        [IgnoreDataMember]
        public int DequeueCount { get; set; }

        [DataMember]
        public IEnumerable<KeyValuePair<string, string>> PartitionAndRowKeys { get; set; }
    }
}