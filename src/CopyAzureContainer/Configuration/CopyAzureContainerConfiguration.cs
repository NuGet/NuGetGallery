// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace CopyAzureContainer
{
    public class CopyAzureContainerConfiguration
    {
        public int? BackupDays { get; set; }
        public string DestStorageAccountName { get; set; }
        public string DestStorageKeyValue { get; set; }
        public List<AzureContainerInfo> SourceContainers { get; set; }
    }
}
