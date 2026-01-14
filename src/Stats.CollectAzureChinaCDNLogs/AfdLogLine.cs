// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.CollectAzureChinaCDNLogs
{
    public class AfdLogLine
    {
        public DateTime Time { get; set; }
        public AfdProperties Properties { get; set; }
    }
}
