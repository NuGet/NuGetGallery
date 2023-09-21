// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Services.ServiceBus
{
    /// <summary>
    /// Serializes objects into Service Bus <see cref="IBrokeredMessage"/>. This serializer will
    /// throw <see cref="FormatException"/> if the message does not contain a message with the expected
    /// type and schema version.
    /// </summary>
    /// <typeparam name="TMessage">A type decorated with a <see cref="SchemaAttribute"/>.</typeparam>
    public class BrokeredMessageSerializer<TMessage> : IBrokeredMessageSerializer<TMessage>
    {
        private static readonly string SchemaName;
        private static readonly int SchemaVersion;

        static BrokeredMessageSerializer()
        {
            var attributes = typeof(TMessage).GetCustomAttributes(typeof(SchemaAttribute), inherit: false);

            if (attributes.Length != 1)
            {
                throw new InvalidOperationException($"{typeof(TMessage)} must have exactly one {nameof(SchemaAttribute)}");
            }

            var schemaAttribute = (SchemaAttribute)attributes[0];

            SchemaName = schemaAttribute.Name;
            SchemaVersion = schemaAttribute.Version;
        }

        public IBrokeredMessage Serialize(TMessage message)
        {
            var json = JsonConvert.SerializeObject(message);
            var brokeredMessage = new ServiceBusMessageWrapper(json);

            brokeredMessage.Properties[BrokeredMessageSerializer.SchemaNameKey] = SchemaName;
            brokeredMessage.Properties[BrokeredMessageSerializer.SchemaVersionKey] = SchemaVersion;

            return brokeredMessage;
        }

        public TMessage Deserialize(IReceivedBrokeredMessage message)
        {
            message.AssertTypeAndSchemaVersion(SchemaName, SchemaVersion);

            return JsonConvert.DeserializeObject<TMessage>(message.GetBody());
        }
    }

    public static class BrokeredMessageSerializer
    {
        public const string SchemaNameKey = "SchemaName";
        public const string SchemaVersionKey = "SchemaVersion";

        public static void AssertTypeAndSchemaVersion(this IReceivedBrokeredMessage message, string type, int schemaVersion)
        {
            if (message.GetSchemaName() != type)
            {
                throw new FormatException($"The provided message should have {SchemaNameKey} property '{type}'.");
            }

            if (message.GetSchemaVersion() != schemaVersion)
            {
                throw new FormatException($"The provided message should have {SchemaVersionKey} property '{schemaVersion}'.");
            }
        }

        public static int GetSchemaVersion(this IReceivedBrokeredMessage message)
        {
            return GetProperty<int>(message, SchemaVersionKey, "an integer");
        }

        public static string GetSchemaName(this IReceivedBrokeredMessage message)
        {
            return GetProperty<string>(message, SchemaNameKey, "a string");
        }

        private static T GetProperty<T>(this IReceivedBrokeredMessage message, string key, string typeLabel)
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
    }
}
