// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RegistrationCollector : SortingGraphCollector
    {
        public const int PartitionSize = 64;
        public const int PackageCountThreshold = 128;

        private readonly StorageFactory _legacyStorageFactory;
        private readonly StorageFactory _semVer2StorageFactory;
        private readonly ShouldIncludeRegistrationPackage _shouldIncludeSemVer2;

        public RegistrationCollector(
            Uri index,
            StorageFactory legacyStorageFactory,
            StorageFactory semVer2StorageFactory,
            Func<HttpMessageHandler> handlerFunc = null)
            : base(index, new Uri[] { Schema.DataTypes.PackageDetails, Schema.DataTypes.PackageDelete }, handlerFunc)
        {
            if (legacyStorageFactory == null)
            {
                throw new ArgumentNullException(nameof(legacyStorageFactory));
            }

            _legacyStorageFactory = legacyStorageFactory;
            _semVer2StorageFactory = semVer2StorageFactory;
            _shouldIncludeSemVer2 = GetShouldIncludeRegistrationPackage(_semVer2StorageFactory);

            ContentBaseAddress = new Uri("http://tempuri.org");
        }

        public Uri ContentBaseAddress { get; set; }

        protected override Task<IEnumerable<CatalogItemBatch>> CreateBatches(IEnumerable<CatalogItem> catalogItems)
        {
            // Grouping batches by commit is slow if it contains
            // the same package registration id over and over again.
            // This happens when, for example, a package publish "wave"
            // occurs.
            //
            // If one package registration id is part of 20 batches,
            // we'll have to process all registration leafs 20 times.
            // It would be better to process these leafs only once.
            //
            // So let's batch by package registration id here,
            // ensuring we never write a commit timestamp to the cursor
            // that is higher than the last item currently processed.
            //
            // So, group by id, then make sure the batch key is the
            // *lowest*  timestamp of all commits in that batch.
            // This ensures that on retries, we will retry
            // from the correct location (even though we may have
            // a little rework).

            var batches = catalogItems
                .GroupBy(item => GetKey(item.Value))
                .Select(group => new CatalogItemBatch(
                    group.Min(item => item.CommitTimeStamp),
                    group));

            // TODO: do we want to limit the number of batches that can be processed at once?

            return Task.FromResult(batches);
        }

        private string GetKey(JToken item)
        {
            return item["nuget:id"].ToString();
        }

        protected override async Task ProcessGraphs(
            KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs,
            CancellationToken cancellationToken)
        {
            await RegistrationMaker.Process(
                registrationKey: new RegistrationKey(sortedGraphs.Key),
                newItems: sortedGraphs.Value,
                shouldInclude: _shouldIncludeSemVer2,
                storageFactory: _legacyStorageFactory,
                contentBaseAddress: ContentBaseAddress,
                partitionSize: PartitionSize,
                packageCountThreshold: PackageCountThreshold,
                cancellationToken: cancellationToken);

            if (_semVer2StorageFactory != null)
            {
                await RegistrationMaker.Process(
                   registrationKey: new RegistrationKey(sortedGraphs.Key),
                   newItems: sortedGraphs.Value,
                   storageFactory: _semVer2StorageFactory,
                   contentBaseAddress: ContentBaseAddress,
                   partitionSize: PartitionSize,
                   packageCountThreshold: PackageCountThreshold,
                   cancellationToken: cancellationToken);
            }
        }

        public static ShouldIncludeRegistrationPackage GetShouldIncludeRegistrationPackage(StorageFactory semVer2StorageFactory)
        {
            // If SemVer 2.0.0 storage is disabled, put SemVer 2.0.0 registration in the legacy storage factory. In no
            // case should a package be completely ignored. That is, if a package is SemVer 2.0.0 but SemVer 2.0.0
            // storage is not enabled, our only choice is to put SemVer 2.0.0 packages in the legacy storage.
            if (semVer2StorageFactory == null)
            {
                return (k, u, g) => true;
            }
            else
            {
                return (k, u, g) => !NuGetVersionUtility.IsGraphSemVer2(k.Version, u, g);
            }
        }
    }
}
