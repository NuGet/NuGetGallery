// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation.Orchestrator
{
    public class PackageValidationMessageDataSerializationAdapter : IBrokeredMessageSerializer<PackageValidationMessageData>
    {
        private readonly IServiceBusMessageSerializer _serializer;

        public PackageValidationMessageDataSerializationAdapter(IServiceBusMessageSerializer serializer)
        {
            _serializer = serializer;
        }

        public PackageValidationMessageData Deserialize(IReceivedBrokeredMessage message)
            => _serializer.DeserializePackageValidationMessageData(message);

        public IBrokeredMessage Serialize(PackageValidationMessageData message)
            => _serializer.SerializePackageValidationMessageData(message);
    }
}
