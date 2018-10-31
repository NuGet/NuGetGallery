// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Configuration
{
    /// <summary>
    /// Configuration values related to Service Bus integration.
    /// </summary>
    public interface IServiceBusConfiguration
    {
        /// <summary>
        /// The connection string to use when connecting to the validation topic. This connection string should not
        /// contain the topic name as the name is explicitly specified by <see cref="Validation_TopicName"/>. This
        /// connection string only needs to have the "Send" privilege. This topic is used for requesting asynchronous
        /// validation of packages.
        /// </summary>
        string Validation_ConnectionString { get; set; }

        /// <summary>
        /// The name of the Azure Service Bus topic to send validation messages to. This topic name is used at the same
        /// time as the <see cref="Validation_ConnectionString"/>.
        /// </summary>
        string Validation_TopicName { get; set; }

        /// <summary>
        /// The connection string to use when connecting to the symbols validation topic. This connection string should not
        /// contain the topic name as the name is explicitly specified by <see cref="SymbolsValidation_TopicName"/>. This
        /// connection string only needs to have the "Send" privilege. This topic is used for requesting asynchronous
        /// validation of symbol packages.
        /// </summary>
        string SymbolsValidation_ConnectionString { get; set; }

        /// <summary>
        /// The name of the Azure Service Bus topic to send validation messages to. This topic name is used at the same
        /// time as the <see cref="SymbolsValidation_ConnectionString"/>.
        /// </summary>
        string SymbolsValidation_TopicName { get; set; }

        /// <summary>
        /// The connection string to use when connecting to the email publisher topic. This connection string should not
        /// contain the topic name as the name is explicitly specified by <see cref="EmailPublisher_TopicName"/>. This
        /// connection string only needs to have the "Send" privilege. This topic is used for requesting asynchronous
        /// publishing of email messages.
        /// </summary>
        string EmailPublisher_ConnectionString { get; set; }

        /// <summary>
        /// The name of the Azure Service Bus topic to send email messages to. This topic name is used at the same
        /// time as the <see cref="EmailPublisher_ConnectionString"/>.
        /// </summary>
        string EmailPublisher_TopicName { get; set; }
    }
}