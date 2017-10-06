// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Storage
{
    /// <summary>
    /// Serializes and deserializes messages and attaches a type and version to the message so that changes to <typeparamref name="T"/> can be caught.
    /// </summary>
    /// <remarks>
    /// For this to work properly, the version property of the constructor must be incremented as changes to <typeparamref name="T"/> occur.
    /// </remarks>
    public class TypedMessageSerializer<T> : IMessageSerializer<T>
    {
        private readonly IMessageSerializer<T> _contentsSerializer;
        private readonly IMessageSerializer<TypedMessage> _messageSerializer;

        private readonly string _type = typeof(T).FullName;
        private readonly int _version;

        public TypedMessageSerializer(IMessageSerializer<T> contentsSerializer, IMessageSerializer<TypedMessage> messageSerializer, int version)
        {
            _contentsSerializer = contentsSerializer ?? throw new ArgumentNullException(nameof(contentsSerializer));
            _messageSerializer = messageSerializer ?? throw new ArgumentNullException(nameof(messageSerializer));
            _version = version;
        }

        public string Serialize(T contents)
        {
            var message = _contentsSerializer.Serialize(contents);
            var typedMessage = new TypedMessage(message, _type, _version);
            return _messageSerializer.Serialize(typedMessage);
        }

        public T Deserialize(string contents)
        {
            var typedMessage = _messageSerializer.Deserialize(contents);
            AssertTypeAndSchema(typedMessage);
            return _contentsSerializer.Deserialize(typedMessage.Message);
        }

        private void AssertTypeAndSchema(TypedMessage message)
        {
            if (message.Type != _type)
            {
                throw new FormatException($"The provided message has the wrong type! Expected: {_type}, found {message.Type}");
            }

            if (message.Version != _version)
            {
                throw new FormatException($"The provided message has the wrong version! Expected: {_version}, found {message.Version}");
            }
        }
    }
}
