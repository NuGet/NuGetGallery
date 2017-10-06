// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Services.Storage
{
    /// <summary>
    /// Uses <see cref="JsonConvert"/> to serialize and deserialize.
    /// </summary>
    public class JsonMessageSerializer<T> : IMessageSerializer<T>
    {
        private JsonSerializerSettings _settings;

        public JsonMessageSerializer(JsonSerializerSettings settings = null)
        {
            _settings = settings;
        }

        public string Serialize(T contents)
        {
            return JsonConvert.SerializeObject(contents, _settings);
        }

        public T Deserialize(string contents)
        {
            return JsonConvert.DeserializeObject<T>(contents, _settings);
        }
    }
}