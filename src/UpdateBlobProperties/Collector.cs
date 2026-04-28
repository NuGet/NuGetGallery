// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Entities;
using NuGetGallery;

namespace UpdateBlobProperties
{
    public class Collector : ICollector
    {
        private readonly BlobInfo _blobInfo;
        private readonly IEntityRepository<Package> _packageRepo;
        private readonly IOptionsSnapshot<UpdateBlobPropertiesConfiguration> _configuration;
        private readonly ILogger<Collector> _logger;

        public Collector(BlobInfo blobInfo,
            IEntityRepository<Package> packageRepo,
            IOptionsSnapshot<UpdateBlobPropertiesConfiguration> configuration,
            ILogger<Collector> logger)
        {
            _blobInfo = blobInfo;
            _packageRepo = packageRepo;
            _configuration = configuration;
            _logger = logger;
        }

        public async IAsyncEnumerable<IList<PackageInfo>> GetPagesOfPackageInfosAsync(int minKey, int maxKey)
        {
            int maxPageSize = _configuration.Value.MaxPageSize;

            int pageIndex = 1;
            int pageStartKey = minKey;
            while (pageStartKey <= maxKey)
            {
                _logger.LogInformation("Loading page: {pageIndex} of package infos from DB. The max size of each page is {maxPageSize}.", pageIndex, maxPageSize);

                var pis = await _blobInfo.GetPageOfPackageInfosToUpdateBlobsAsync(_packageRepo, pageStartKey, maxKey, maxPageSize);
                if (pis.Count > 0)
                {
                    _logger.LogInformation("Loaded page: {pageIndex} of {pageSize} package infos from DB. The page starts from key: {pageStartKey} and ends at key: {pageEndKey}.",
                        pageIndex, pis.Count, pis.First().Key, pis.Last().Key);

                    pageStartKey = pis.Last().Key + 1;
                    pageIndex++;

                    yield return pis;
                }
                else
                {
                    _logger.LogInformation("No more pages to load from DB.");

                    yield break;
                }
            }
        }
    }
}
