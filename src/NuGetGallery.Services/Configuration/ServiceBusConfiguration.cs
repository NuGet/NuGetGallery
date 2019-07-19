// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;

namespace NuGetGallery.Configuration
{
    public class ServiceBusConfiguration : IServiceBusConfiguration
    {
        [DisplayName("AccountDeleter.ConnectionString")]
        public string AccountDeleter_ConnectionString { get; set; }

        [DisplayName("AccountDeleter.TopicName")]
        public string AccountDeleter_TopicName { get; set; }

        [DisplayName("Validation.ConnectionString")]
        public string Validation_ConnectionString { get; set; }

        [DisplayName("Validation.TopicName")]
        public string Validation_TopicName { get; set; }

        [DisplayName("SymbolsValidation.ConnectionString")]
        public string SymbolsValidation_ConnectionString { get; set; }

        [DisplayName("SymbolsValidation.TopicName")]
        public string SymbolsValidation_TopicName { get; set; }

        [DisplayName("EmailPublisher.ConnectionString")]
        public string EmailPublisher_ConnectionString { get; set; }

        [DisplayName("EmailPublisher.TopicName")]
        public string EmailPublisher_TopicName { get; set; }
    }
}