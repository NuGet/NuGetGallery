// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;

namespace NuGetGallery.Configuration
{
    public class ServiceBusConfiguration : IServiceBusConfiguration
    {
        [DisplayName("Validation.ConnectionString")]
        public string Validation_ConnectionString { get; set; }

        [DisplayName("Validation.TopicName")]
        public string Validation_TopicName { get; set; }
    }
}