// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// This converter is necessary since polymorphic deserialization is not supported by System.Text.Json and by
    /// extension Azure.Search.Documents. However we leverage polymorphism for performance and ergonomics reasons
    /// in <see cref="BatchPusher"/>. Azure Search supports a mixture document types and actions in a single "index
    /// documents" REST API call so it is possible from a service perspective just not from the SDK, out of the box.
    /// 
    /// If new model types are introduced, they will need to be added to this switch statement.
    /// 
    /// Polymorphism in System.Text.Json:
    /// https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-migrate-from-newtonsoft-how-to?pivots=dotnet-5-0#polymorphic-serialization
    /// 
    /// Azure Search REST API:
    /// https://docs.microsoft.com/en-us/rest/api/searchservice/addupdate-or-delete-documents
    /// </summary>
    public class KeyedDocumentConverter : JsonConverter<KeyedDocument>
    {
        public static KeyedDocumentConverter Instance { get; } = new KeyedDocumentConverter();

        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(KeyedDocument) == typeToConvert;
        }

        public override KeyedDocument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // We don't need to implement the read method because we never query for KeyedDocument instances
            // directly. Instead we query for full search document models, which doesn't require polymorphism.
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, KeyedDocument value, JsonSerializerOptions options)
        {
            // Each non-abstract class descending from KeyedDocument should be in this switch to allow serialization.
            switch (value)
            {
                case SearchDocument.Full searchFull:
                    JsonSerializer.Serialize(writer, searchFull, options);
                    break;
                case SearchDocument.UpdateLatest searchUpdateLatest:
                    JsonSerializer.Serialize(writer, searchUpdateLatest, options);
                    break;
                case SearchDocument.UpdateVersionList searchUpdateVersionList:
                    JsonSerializer.Serialize(writer, searchUpdateVersionList, options);
                    break;
                case SearchDocument.UpdateOwners searchUpdateOwners:
                    JsonSerializer.Serialize(writer, searchUpdateOwners, options);
                    break;
                case SearchDocument.UpdateDownloadCount searchUpdateDownloadCount:
                    JsonSerializer.Serialize(writer, searchUpdateDownloadCount, options);
                    break;
                case HijackDocument.Full hijackFull:
                    JsonSerializer.Serialize(writer, hijackFull, options);
                    break;
                case HijackDocument.Latest hijackLatest:
                    JsonSerializer.Serialize(writer, hijackLatest, options);
                    break;
                case BaseMetadataDocument baseMetadata:
                    JsonSerializer.Serialize(writer, baseMetadata, options);
                    break;
                case KeyedDocument keyed:
                    writer.WriteStartObject();
                    writer.WriteString("key", keyed.Key);
                    writer.WriteEndObject();
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
