// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation
{
    /// <summary>
    /// Configuration for enqueuing to the package validation Service Bus topic. The primary subscription on this topic
    /// is for the package or symbol package orchestrator.
    /// </summary>
    public class PackageValidationServiceBusConfiguration
    {
        public string ConnectionString { get; set; }
        public string TopicPath { get; set; }
    }
}