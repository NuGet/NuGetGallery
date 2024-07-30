// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;

namespace NgTests.Infrastructure
{
    public static class TestUtility
    {
        private static readonly Random _random = new Random();

        public static string CreateRandomAlphanumericString()
        {
            return CreateRandomAlphanumericString(_random);
        }

        public static string CreateRandomAlphanumericString(Random random)
        {
            const string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            return new string(
                Enumerable.Repeat(characters, count: 16)
                    .Select(s => s[random.Next(s.Length)])
                    .ToArray());
        }

        public static JObject CreateCatalogContextJObject()
        {
            return new JObject(
                new JProperty(CatalogConstants.VocabKeyword, CatalogConstants.NuGetSchemaUri),
                new JProperty(CatalogConstants.NuGet, CatalogConstants.NuGetSchemaUri),
                new JProperty(CatalogConstants.Items,
                    new JObject(
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.Item),
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))),
                new JProperty(CatalogConstants.Parent,
                    new JObject(
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.IdKeyword))),
                new JProperty(CatalogConstants.CommitTimeStamp,
                    new JObject(
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))),
                new JProperty(CatalogConstants.NuGetLastCreated,
                    new JObject(
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))),
                new JProperty(CatalogConstants.NuGetLastEdited,
                    new JObject(
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))),
                new JProperty(CatalogConstants.NuGetLastDeleted,
                    new JObject(
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))));
        }

        public static JObject CreateCatalogCommitItemJObject(
            DateTime commitTimeStamp,
            PackageIdentity packageIdentity,
            string commitId = null)
        {
            return new JObject(
                new JProperty(CatalogConstants.IdKeyword, $"https://nuget.test/{packageIdentity.Id}"),
                new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.NuGetPackageDetails),
                new JProperty(CatalogConstants.CommitTimeStamp, commitTimeStamp.ToString("O")),
                new JProperty(CatalogConstants.CommitId, commitId ?? Guid.NewGuid().ToString()),
                new JProperty(CatalogConstants.NuGetId, packageIdentity.Id),
                new JProperty(CatalogConstants.NuGetVersion, packageIdentity.Version.ToNormalizedString()));
        }

        public static CatalogCommitItem CreateCatalogCommitItem(
            DateTime commitTimeStamp,
            PackageIdentity packageIdentity,
            string commitId = null)
        {
            var context = CreateCatalogContextJObject();
            var commitItem = CreateCatalogCommitItemJObject(commitTimeStamp, packageIdentity, commitId);

            return CatalogCommitItem.Create(context, commitItem);
        }
    }
}