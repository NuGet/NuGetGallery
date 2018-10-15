// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation
{
    public class ServiceBusMessageSerializer : IServiceBusMessageSerializer
    {
        private const string PackageValidationSchemaName = "PackageValidationMessageData";

        private static readonly BrokeredMessageSerializer<PackageValidationMessageData1> _serializer = new BrokeredMessageSerializer<PackageValidationMessageData1>();

        public IBrokeredMessage SerializePackageValidationMessageData(PackageValidationMessageData message)
        {
            return _serializer.Serialize(new PackageValidationMessageData1
            {
                PackageId = message.PackageId,
                PackageVersion = message.PackageVersion,
                PackageNormalizedVersion = message.PackageNormalizedVersion,
                ValidationTrackingId = message.ValidationTrackingId,
                ValidatingType = message.ValidatingType,
                EntityKey = message.EntityKey
            });
        }

        public PackageValidationMessageData DeserializePackageValidationMessageData(IBrokeredMessage message)
        {
            var data = _serializer.Deserialize(message);

            return new PackageValidationMessageData(
                data.PackageId,
                data.PackageVersion,
                data.ValidationTrackingId,
                data.ValidatingType,
                message.DeliveryCount,
                data.EntityKey);
        }

        [Schema(Name = PackageValidationSchemaName, Version = 1)]
        private class PackageValidationMessageData1
        {
            public string PackageId { get; set; }
            public string PackageVersion { get; set; }
            public string PackageNormalizedVersion { get; set; }
            public Guid ValidationTrackingId { get; set; }
            public ValidatingType ValidatingType { get; set; }
            public int? EntityKey { get; set; }
        }
    }
}
