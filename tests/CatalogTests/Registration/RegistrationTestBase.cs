// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CatalogTests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Xunit;

namespace CatalogTests.Registration
{
    public abstract class RegistrationTestBase
    {
        protected const string _cacheControl = "no-store";
        protected const string _contentType = "application/json";

        protected static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings()
        {
            DateParseHandling = DateParseHandling.None,
            NullValueHandling = NullValueHandling.Ignore
        };

        protected static string GetRegistrationDateTime(string catalogDateTime)
        {
            return DateTimeOffset.Parse(catalogDateTime)
                .ToLocalTime()
                .ToString("yyyy-MM-ddTHH:mm:ss.FFFzzz");
        }

        protected static T GetStorageContent<T>(MemoryStorage registrationStorage, Uri contentUri)
        {
            Assert.True(registrationStorage.Content.TryGetValue(contentUri, out var content));

            var jTokenStorageContent = content as JTokenStorageContent;

            Assert.NotNull(jTokenStorageContent);
            Assert.Equal(_cacheControl, jTokenStorageContent.CacheControl);
            Assert.Equal(_contentType, jTokenStorageContent.ContentType);

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            // Verify that no new properties were added unexpectedly.
            Assert.Equal(properties.Length, ((JObject)jTokenStorageContent.Content).Count);

            var json = jTokenStorageContent.Content.ToString();

            return JsonConvert.DeserializeObject<T>(json, _jsonSettings);
        }

        protected static Uri GetPackageContentUri(Uri contentBaseUri, string packageId, string packageVersion)
        {
            return new Uri($"{contentBaseUri.AbsoluteUri}packages/{packageId}.{packageVersion}.nupkg");
        }

        protected static Uri GetRegistrationPackageIndexUri(Uri baseUri)
        {
            return new Uri($"{baseUri.AbsoluteUri}index.json");
        }

        protected static Uri GetRegistrationPackageIndexUri(Uri baseUri, string packageId)
        {
            return new Uri($"{baseUri.AbsoluteUri}{packageId}/index.json");
        }

        protected static Uri GetRegistrationPackageVersionUri(Uri baseUri, string packageId, string packageVersion)
        {
            return new Uri($"{baseUri.AbsoluteUri}{packageId}/{packageVersion}.json");
        }

        protected sealed class ExpectedPage
        {
            internal IReadOnlyList<CatalogIndependentPackageDetails> Details { get; }
            internal string LowerVersion { get; }
            internal string UpperVersion { get; }

            internal ExpectedPage(params CatalogIndependentPackageDetails[] packageDetails)
            {
                Details = packageDetails;

                var versions = packageDetails.Select(details => new NuGetVersion(details.Version)).ToArray();

                LowerVersion = versions.Min().ToNormalizedString().ToLowerInvariant();
                UpperVersion = versions.Max().ToNormalizedString().ToLowerInvariant();
            }
        }
    }
}