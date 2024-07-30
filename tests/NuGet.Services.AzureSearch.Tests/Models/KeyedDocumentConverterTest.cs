// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    public class KeyedDocumentConverterTest
    {
        public static IEnumerable<object[]> SerializesAllConcreteKeyedDocumentsTestData = typeof(KeyedDocument)
            .Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(KeyedDocument).IsAssignableFrom(t))
            .Where(t => t != typeof(KeyedDocument))
            .Select(t => new object[] { t });

        [Theory]
        [MemberData(nameof(SerializesAllConcreteKeyedDocumentsTestData))]
        public void SerializesAllConcreteKeyedDocuments(Type type)
        {
            var model = (KeyedDocument)Activator.CreateInstance(type);

            Target.Write(Writer, model, Options);

            var jsonDocument = JsonDocument.Parse(GetJson());
            var properties = jsonDocument.RootElement.EnumerateObject().Select(x => x.Name).Distinct().ToList();
            Assert.Contains("key", properties);
            Assert.True(properties.Count > 1);
        }

        [Fact]
        public void SerializesKeyedDocument()
        {
            var model = new KeyedDocument { Key = "skeleton" };

            Target.Write(Writer, model, Options);

            Assert.Equal("{\"key\":\"skeleton\"}", GetJson());
        }

        private string GetJson()
        {
            Writer.Flush();
            Stream.Flush();
            var json = Encoding.UTF8.GetString(Stream.ToArray());
            return json;
        }

        public KeyedDocumentConverterTest()
        {
            Stream = new MemoryStream();
            Writer = new Utf8JsonWriter(Stream);
            Options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            Target = new KeyedDocumentConverter();
        }

        private readonly KeyedDocumentConverter Target;
        private readonly MemoryStream Stream;
        private readonly Utf8JsonWriter Writer;
        private readonly JsonSerializerOptions Options;
    }
}
