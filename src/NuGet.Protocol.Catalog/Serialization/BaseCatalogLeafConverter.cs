// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Protocol.Catalog
{
    internal abstract class BaseCatalogLeafConverter : JsonConverter
    {
        private readonly IReadOnlyDictionary<CatalogLeafType, string> _fromType;

        public BaseCatalogLeafConverter(IReadOnlyDictionary<CatalogLeafType, string> fromType)
        {
            _fromType = fromType;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(CatalogLeafType);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            string output;
            if (_fromType.TryGetValue((CatalogLeafType)value, out output))
            {
                writer.WriteValue(output);
                return;
            }

            throw new NotSupportedException($"The catalog leaf type '{value}' is not supported.");
        }
    }
}
