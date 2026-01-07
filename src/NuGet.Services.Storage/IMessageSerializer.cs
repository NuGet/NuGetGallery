// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Storage
{
    /// <summary>
    /// Serializes and deserializes a message between <typeparamref name="T"/> and <see cref="string"/>.
    /// </summary>
    public interface IMessageSerializer<T>
    {
        /// <summary>
        /// Serializes <typeparamref name="T"/> to <see cref="string"/>.
        /// </summary>
        /// <param name="contents">The message to serialize.</param>
        /// <returns>The serialized message.</returns>
        string Serialize(T contents);

        /// <summary>
        /// Deserializes <see cref="string"/> to <typeparamref name="T"/>.
        /// </summary>
        /// <param name="contents">The message to deserialize.</param>
        /// <returns>The deserialized message.</returns>
        T Deserialize(string contents);
    }
}
