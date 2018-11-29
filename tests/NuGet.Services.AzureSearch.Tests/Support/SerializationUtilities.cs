// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using NuGet.Protocol.Catalog;
using NuGet.Services.Entities;
using PackageDependency = NuGet.Protocol.Catalog.PackageDependency;

namespace NuGet.Services.AzureSearch.Support
{
    public static class Data
    {
        public const string PackageId = "WindowsAzure.Storage";
        public const string NormalizedVersion = "7.1.2-alpha";
        public const string FullVersion = "7.1.2-alpha+git";

        public static Package PackageEntity => new Package
        {
            FlattenedAuthors = "Microsoft",
            Copyright = "© Microsoft Corporation. All rights reserved.",
            Created = new DateTime(2017, 1, 1),
            Description = "Description.",
            FlattenedDependencies = "Microsoft.Data.OData:5.6.4:net40-client|Newtonsoft.Json:6.0.8:net40-client",
            Hash = "oMs9XKzRTsbnIpITcqZ5XAv1h2z6oyJ33+Z/PJx36iVikge/8wm5AORqAv7soKND3v5/0QWW9PQ0ktQuQu9aQQ==",
            HashAlgorithm = "SHA512",
            IconUrl = "http://go.microsoft.com/fwlink/?LinkID=288890",
            IsPrerelease = true,
            Language = "en-US",
            LastEdited = new DateTime(2017, 1, 2),
            LicenseUrl = "http://go.microsoft.com/fwlink/?LinkId=331471",
            Listed = true,
            MinClientVersion = "2.12",
            NormalizedVersion = "7.1.2-alpha",
            PackageFileSize = 3039254,
            ProjectUrl = "https://github.com/Azure/azure-storage-net",
            Published = new DateTime(2017, 1, 3),
            ReleaseNotes = "Release notes.",
            RequiresLicenseAcceptance = true,
            SemVerLevelKey = 2,
            Summary = "Summary.",
            Tags = "Microsoft Azure Storage Table Blob File Queue Scalable windowsazureofficial",
            Title = "Windows Azure Storage",
            Version = "7.1.2.0-alpha+git",
        };

        public static PackageDetailsCatalogLeaf Leaf => new PackageDetailsCatalogLeaf
        {
            Authors = "Microsoft",
            Copyright = "© Microsoft Corporation. All rights reserved.",
            Created = new DateTimeOffset(new DateTime(2017, 1, 1), TimeSpan.Zero),
            Description = "Description.",
            DependencyGroups = new List<PackageDependencyGroup>
            {
                new PackageDependencyGroup
                {
                    TargetFramework = ".NETFramework4.0-Client",
                    Dependencies = new List<PackageDependency>
                    {
                        new PackageDependency
                        {
                            Id = "Microsoft.Data.OData",
                            Range = "[5.6.4, )",
                        },
                        new PackageDependency
                        {
                            Id = "Newtonsoft.Json",
                            Range = "[6.0.8, )",
                        },
                    },
                },
            },
            IconUrl = "http://go.microsoft.com/fwlink/?LinkID=288890",
            IsPrerelease = true,
            Language = "en-US",
            LastEdited = new DateTimeOffset(new DateTime(2017, 1, 2), TimeSpan.Zero),
            LicenseUrl = "http://go.microsoft.com/fwlink/?LinkId=331471",
            Listed = true,
            MinClientVersion = "2.12",
            PackageHash = "oMs9XKzRTsbnIpITcqZ5XAv1h2z6oyJ33+Z/PJx36iVikge/8wm5AORqAv7soKND3v5/0QWW9PQ0ktQuQu9aQQ==",
            PackageHashAlgorithm = "SHA512",
            PackageId = PackageId,
            PackageSize = 3039254,
            PackageVersion = FullVersion,
            ProjectUrl = "https://github.com/Azure/azure-storage-net",
            Published = new DateTimeOffset(new DateTime(2017, 1, 3), TimeSpan.Zero),
            ReleaseNotes = "Release notes.",
            RequireLicenseAgreement = true,
            Summary = "Summary.",
            Tags = new List<string> { "Microsoft", "Azure", "Storage", "Table", "Blob", "File", "Queue", "Scalable", "windowsazureofficial" },
            Title = "Windows Azure Storage",
            VerbatimVersion = "7.1.2.0-alpha+git",
        };
    }



    public class SerializationUtilities
    {
        public static async Task<string> SerializeToJsonAsync<T>(T obj) where T : class
        {
            using (var testHandler = new TestHttpClientHandler())
            using (var serviceClient = new SearchServiceClient(
                "unit-test-service",
                new SearchCredentials("unit-test-api-key"),
                testHandler))
            {
                var indexClient = serviceClient.Indexes.GetClient("unit-test-index");
                await indexClient.Documents.IndexAsync(IndexBatch.Upload(new[] { obj }));
                return testHandler.LastRequestBody;
            }
        }

        private class TestHttpClientHandler : HttpClientHandler
        {
            public string LastRequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.Content != null)
                {
                    LastRequestBody = await request.Content.ReadAsStringAsync();
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(LastRequestBody ?? string.Empty),
                };
            }
        }
    }
}
