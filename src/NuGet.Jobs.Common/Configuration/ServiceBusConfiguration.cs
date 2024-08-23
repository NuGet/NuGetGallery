﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Configuration
{
    public class ServiceBusConfiguration
    {
        public string ConnectionString { get; set; }
        public string TopicPath { get; set; }
        public string SubscriptionName { get; set; }
    }
}