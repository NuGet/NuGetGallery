// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using ArrayNormalizer = System.Collections.Generic.KeyValuePair<NuGet.Jobs.RegistrationComparer.ShouldNormalizeByArray, System.Comparison<Newtonsoft.Json.Linq.JToken>>;
using ValueNormalizer = System.Collections.Generic.KeyValuePair<NuGet.Jobs.RegistrationComparer.ShouldNormalizeByPath, NuGet.Jobs.RegistrationComparer.NormalizeToken>;

namespace NuGet.Jobs.RegistrationComparer
{
    public class JsonComparerFacts
    {
        [Fact]
        public void AcceptsSameObject()
        {
            var a = Json(new { array = new[] { 0, 1, 3 } });
            var b = Json(new { array = new[] { 0, 1, 3 } });

            Target.Compare(a, b, Context);

            Assert.NotSame(a, b);
        }

        [Fact]
        public void DetectsMissingItemInArray()
        {
            var a = Json(new { array = new[] { 0, 1, 3 } });
            var b = Json(new { array = new[] { 0, 1, 2, 3 } });

            var ex = Assert.Throws<InvalidOperationException>(() => Target.Compare(a, b, Context));

            Assert.Contains("The JSON array item count is different.", ex.Message);
        }

        [Fact]
        public void DetectsDifferentItemsInArray()
        {
            var a = Json(new { array = new[] { 0, 1, 3 } });
            var b = Json(new { array = new[] { 0, 1, 2 } });

            var ex = Assert.Throws<InvalidOperationException>(() => Target.Compare(a, b, Context));

            Assert.Contains("The value of the JSON scalar is different.", ex.Message);
        }

        [Fact]
        public void DetectsOutOfOrderItemsInArray()
        {
            var a = Json(new { array = new[] { 0, 2, 1 } });
            var b = Json(new { array = new[] { 0, 1, 2 } });

            var ex = Assert.Throws<InvalidOperationException>(() => Target.Compare(a, b, Context));

            Assert.Contains("The value of the JSON scalar is different.", ex.Message);
        }

        [Theory]
        [InlineData("2", 2)]
        [InlineData(true, 1)]
        [InlineData("false", false)]
        [InlineData("null", null)]
        [InlineData(0, null)]
        public void DetectsDifferentTypesInArray(object valueA, object valueB)
        {
            var a = Json(new { array = new object[] { 0, 1, valueA } });
            var b = Json(new { array = new object[] { 0, 1, valueB } });

            var ex = Assert.Throws<InvalidOperationException>(() => Target.Compare(a, b, Context));

            Assert.Contains("The type of the JSON value is different.", ex.Message);
        }

        [Fact]
        public void DetectsDifferentProperties()
        {
            var a = Json(new { arrayA = new[] { 0, 1, 2 } });
            var b = Json(new { arrayB = new[] { 0, 1, 2 } });

            var ex = Assert.Throws<InvalidOperationException>(() => Target.Compare(a, b, Context));

            Assert.Contains("The JSON object property names are disjoint.", ex.Message);
        }

        [Fact]
        public void DetectsOutOfOrderProperties()
        {
            var a = Json(new { inner = new { a = "a", b = "b" } });
            var b = Json(new { inner = new { b = "b", a = "a" } });

            var ex = Assert.Throws<InvalidOperationException>(() => Target.Compare(a, b, Context));

            Assert.Contains("The JSON object property names are in a different order.", ex.Message);
        }

        [Fact]
        public void DetectsDifferentCaseOfPropertyNames()
        {
            var a = Json(new { array = new[] { 0, 1, 2 } });
            var b = Json(new { Array = new[] { 0, 1, 2 } });

            var ex = Assert.Throws<InvalidOperationException>(() => Target.Compare(a, b, Context));

            Assert.Contains("The JSON object property names are disjoint.", ex.Message);
        }

        [Fact]
        public void DetectsExtraProperty()
        {
            var a = Json(new { array = new[] { 0, 1, 2 } });
            var b = Json(new { array = new[] { 0, 1, 2 }, somethingElse = 2 });

            var ex = Assert.Throws<InvalidOperationException>(() => Target.Compare(a, b, Context));

            Assert.Contains("The JSON object property names are disjoint.", ex.Message);
        }

        [Fact]
        public void AllowsValueToBeNormalized()
        {
            var normalizers = new Normalizers(
                scalarNormalizers: new List<ValueNormalizer>
                {
                    new ValueNormalizer(
                        (path) => path == "random",
                        (token, isLeft, context) => "999"),
                },
                unsortedObjects: new List<ShouldNormalizeByPath>(),
                unsortedArrays: new List<ArrayNormalizer>());
            var a = Json(new { array = new[] { 0, 1, 2 }, random = 23 });
            var b = Json(new { array = new[] { 0, 1, 2 }, random = 42 });

            Target.Compare(a, b, GetContext(normalizers));
        }

        [Fact]
        public void AllowsObjectPropertyOrderToBeIgnored()
        {
            var normalizers = new Normalizers(
                scalarNormalizers: new List<ValueNormalizer>(),
                unsortedObjects: new List<ShouldNormalizeByPath>
                {
                    (path) => path == "inner",
                },
                unsortedArrays: new List<ArrayNormalizer>());
            var a = Json(new { inner = new { a = "a", b = "b" } });
            var b = Json(new { inner = new { b = "b", a = "a" } });

            Target.Compare(a, b, GetContext(normalizers));
        }

        [Fact]
        public void AllowsArrayItemOrderToBeIgnored()
        {
            var normalizers = new Normalizers(
                scalarNormalizers: new List<ValueNormalizer>(),
                unsortedObjects: new List<ShouldNormalizeByPath>(),
                unsortedArrays: new List<ArrayNormalizer>
                {
                    new ArrayNormalizer(
                        array => array.Path == "array",
                        (x, y) => Comparer<JToken>.Default.Compare(x, y)),
                });
            var a = Json(new { array = new[] { 0, 2, 1 } });
            var b = Json(new { array = new[] { 0, 1, 2 } });

            Target.Compare(a, b, GetContext(normalizers));
        }

        [Fact]
        public void AllowsPropertyNameWithAtToBeNormalized()
        {
            var normalizers = new Normalizers(
                scalarNormalizers: new List<ValueNormalizer>(),
                unsortedObjects: new List<ShouldNormalizeByPath>(),
                unsortedArrays: new List<ArrayNormalizer>
                {
                    new ArrayNormalizer(
                        array => array.Path == "@type",
                        (x, y) => StringComparer.Ordinal.Compare((string)x, (string)y)),
                });
            var a = Json(new Dictionary<string, object> { { "@type", new[] { "foo", "bar" } } });
            var b = Json(new Dictionary<string, object> { { "@type", new[] { "bar", "foo" } } });

            Target.Compare(a, b, GetContext(normalizers));
        }

        [Fact]
        public async Task NormalizesKnownUrl()
        {
            var leftBaseUrl = "https://api.nuget.org/v3/registration4-gz-semver2/";
            var rightBaseUrl = "https://api.nuget.org/v3/registration5-gz-semver2/";
            var packageId = "BaseTestPackage.SearchFilters";
            var relativePath = "/index.json";
            var context = new ComparisonContext(
                packageId,
                leftBaseUrl,
                rightBaseUrl,
                $"{leftBaseUrl}{packageId.ToLowerInvariant()}{relativePath}",
                $"{rightBaseUrl}{packageId.ToLowerInvariant()}{relativePath}",
                Normalizers.Index);
            var a = await DownloadAsync(context.LeftUrl);
            var b = await DownloadAsync(context.RightUrl);

            Target.Compare(a, b, context);
        }

        public JsonComparerFacts()
        {
            Context = GetContext();
            Target = new JsonComparer();
        }

        private ComparisonContext GetContext(Normalizers normalizers)
        {
            return new ComparisonContext(
                "NuGet.Versioning",
                "https://example/api/a",
                "https://example/api/b",
                "https://example/api/a/index.json",
                "https://example/api/b/index.json",
                normalizers);
        }

        private ComparisonContext GetContext()
        {
            return GetContext(
                new Normalizers(
                    new List<ValueNormalizer>(),
                    new List<ShouldNormalizeByPath>(),
                    new List<ArrayNormalizer>()));
        }

        public ComparisonContext Context { get; }
        public JsonComparer Target { get; }

        private JToken Json<T>(T obj)
        {
            return JsonConvert.DeserializeObject<JToken>(JsonConvert.SerializeObject(obj));
        }

        private async Task<JToken> DownloadAsync(string url)
        {
            using (var httpClientHandler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip })
            using (var httpClient = new HttpClient(httpClientHandler))
            using (var stream = await httpClient.GetStreamAsync(url))
            using (var streamReader = new StreamReader(stream))
            using (var jsonTextReader = new JsonTextReader(streamReader))
            {
                jsonTextReader.DateParseHandling = DateParseHandling.None;

                return await JObject.LoadAsync(jsonTextReader);
            }
        }
    }
}
