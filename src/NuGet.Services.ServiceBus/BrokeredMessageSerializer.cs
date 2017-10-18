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
        private const string SchemaNameKey = "SchemaName";
        private const string SchemaVersionKey = "SchemaVersion";

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
            var brokeredMessage = new BrokeredMessageWrapper(json);

            brokeredMessage.Properties[SchemaNameKey] = SchemaName;
            brokeredMessage.Properties[SchemaVersionKey] = SchemaVersion;

            return brokeredMessage;
        }

        public TMessage Deserialize(IBrokeredMessage message)
        {
            AssertTypeAndSchemaVersion(message, SchemaName, SchemaVersion);

            return JsonConvert.DeserializeObject<TMessage>(message.GetBody());
        }

        private static void AssertTypeAndSchemaVersion(IBrokeredMessage message, string type, int schemaVersion)
        {
            if (GetType(message) != type)
            {
                throw new FormatException($"The provided message should have {SchemaNameKey} property '{type}'.");
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
            return GetProperty<string>(message, SchemaNameKey, "a string");
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
    }
}
