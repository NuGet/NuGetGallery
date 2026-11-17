// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Gallery.Maintenance
{
    /// <summary>
    /// Configures cleanup of terminal private staging content. Disabled unless explicitly enabled.
    /// </summary>
    public class StagingBlobCleanupConfiguration
    {
        public bool Enabled { get; set; }

        public string StorageConnectionString { get; set; }
    }
}
