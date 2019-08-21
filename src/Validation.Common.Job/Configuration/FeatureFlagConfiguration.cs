// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation
{
    public class FeatureFlagConfiguration
    {
        public TimeSpan RefreshInternal { get; set; } = TimeSpan.FromMinutes(1);
        public string ConnectionString { get; set; }
    }
}
