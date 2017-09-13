// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using NuGet.Services.ServiceBus;

namespace NuGet.Services.Validation
{
    public class ServiceBusMessageSerializer : IServiceBusMessageSerializer
    {
        private const string SchemaVersionKey = "SchemaVersion";
        private const string TypeKey = "Type";
        private const string PackageValidationType = nameof(PackageValidationMessageData);
        private const int SchemaVersion1 = 1;

        public IBrokeredMessage SerializePackageValidationMessageData(PackageValidationMessageData message)
        {
            var body = new PackageValidationMessageData1
            {
                PackageId = message.PackageId,
                PackageVersion = message.PackageVersion,
                ValidationTrackingId = message.ValidationTrackingId,
            };

            var brokeredMessage = Serialize(body, PackageValidationType, SchemaVersion1);

            return brokeredMessage;
        }

        public PackageValidationMessageData DeserializePackageValidationMessageData(IBrokeredMessage message)
        {
            AssertTypeAndSchemaVersion(message, PackageValidationType, SchemaVersion1);

            var json = message.GetBody();
            var data = Deserialize<PackageValidationMessageData1>(json);

            return new PackageValidationMessageData(
                data.PackageId,
                data.PackageVersion,
                data.ValidationTrackingId);
        }

        private static void AssertTypeAndSchemaVersion(IBrokeredMessage message, string type, int schemaVersion)
        {
            if (GetType(message) != type)
            {
                throw new FormatException($"The provided message should have {TypeKey} property '{type}'.");
            }

            if (GetSchemaVersion(message) != schemaVersion)
            {
                throw new FormatException($"The provided message should have {SchemaVersionKey} property '{schemaVersion}'.");
            }
        }

        private static int GetSchemaVersion(IBrokeredMessage message)
        {
            return GetProperty<int>(message, SchemaVersionKey, "an integer");
        }

        private static string GetType(IBrokeredMessage message)
        {
            return GetProperty<string>(message, TypeKey, "a string");
        }

        private static T GetProperty<T>(IBrokeredMessage message, string key, string typeLabel)
        {
            object value;
            if (!message.Properties.TryGetValue(key, out value))
            {
                throw new FormatException($"The provided message does not have a {key} property.");
            }

            if (!(value is T))
            {
                throw new FormatException($"The provided message contains a {key} property that is not {typeLabel}.");
            }

            return (T)value;
        }

        private static IBrokeredMessage Serialize<T>(T data, string type, int schemaVersion)
        {
            var json = JsonConvert.SerializeObject(data);
            var brokeredMessage = new BrokeredMessageWrapper(json);
            brokeredMessage.Properties[TypeKey] = type;
            brokeredMessage.Properties[SchemaVersionKey] = schemaVersion;

            return brokeredMessage;
        }

        private static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        private class PackageValidationMessageData1
        {
            public string PackageId { get; set; }
            public string PackageVersion { get; set; }
            public Guid ValidationTrackingId { get; set; }
        }
    }
}
