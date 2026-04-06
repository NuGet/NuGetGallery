// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace UpdateBlobProperties
{
    public class UpdateBlobPropertiesConfiguration
    {
        public string StorageConnectionString { get; set; }
        public int MaxPageSize { get; set; }
        public int MaxKey { get; set; }
    }
}
