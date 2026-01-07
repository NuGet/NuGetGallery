// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation
{
    public class SubscriptionProcessorConfiguration
    {
        public int MaxConcurrentCalls { get; set; }
        public TimeSpan ProcessDuration { get; set; } = TimeSpan.FromDays(1);
    }
}