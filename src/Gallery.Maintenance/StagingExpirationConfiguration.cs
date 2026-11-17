// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Gallery.Maintenance
{
    /// <summary>
    /// Enables automatic staging expiration. Disabled unless explicitly enabled.
    /// </summary>
    public class StagingExpirationConfiguration
    {
        public bool Enabled { get; set; }
    }
}
