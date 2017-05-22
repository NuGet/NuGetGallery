// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Abstract class with the shared functionality between all <see cref="IValidator"/> implementations.
    /// </summary>
    public abstract class Validator : IValidator
    {
        /// <summary>
        /// Constructor for testing purposes.
        /// </summary>
        protected Validator()
        {
        }

        protected Validator(
            IDictionary<FeedType, SourceRepository> feedToSource,
            ILogger<Validator> logger)
        {
            _timestampMetadataResourceV2 = feedToSource[FeedType.HttpV2].GetResource<IPackageTimestampMetadataResource>();
            Logger = logger;
            CommonLogger = logger.AsCommon();
        }

        protected readonly ILogger<Validator> Logger;
        protected readonly Common.ILogger CommonLogger;

        private readonly IPackageTimestampMetadataResource _timestampMetadataResourceV2;

        public async Task<ValidationResult> Validate(ValidationContext data)
        {
            var validationResult = new ValidationResult
            {
                Validator = this,
                Result = TestResult.Pass
            };
            
            try
            {
                bool shouldRun = false;
                try
                {
                    shouldRun = await ShouldRun(data);
                }
                catch (Exception e)
                {
                    throw new ValidationException("Threw an exception while trying to determine whether or not validation should run!", e);
                }

                if (shouldRun)
                {
                    await RunInternal(data);
                }
                else
                {
                    validationResult.Result = TestResult.Skip;
                }
            }
            catch (Exception e)
            {
                validationResult.Result = TestResult.Fail;
                validationResult.Exception = e;
            }

            return validationResult;
        }

        /// <summary>
        /// Checks that the current batch of catalog entries contains the entry that was created from the current state of the V2 feed.
        /// </summary>
        protected virtual async Task<bool> ShouldRun(ValidationContext data)
        {
            var timestampV2 = await _timestampMetadataResourceV2.GetAsync(data);
            var timestampCatalog = await PackageTimestampMetadata.FromCatalogEntries(data.Client, data.Entries);
            
            if (!timestampV2.Last.HasValue)
            {
                throw new ValidationException("Cannot get timestamp data for package from the V2 feed!");
            }

            if (!timestampCatalog.Last.HasValue)
            {
                throw new ValidationException("Cannot get timestamp data for package from the catalog!");
            }

            if (timestampCatalog.Last > timestampV2.Last)
            {
                throw new ValidationException("The timestamp in the catalog is newer than the timestamp in the feed! This should never happen because all data flows from the catalog into the feed!");
            }

            // If the timestamp metadata in the catalog is LESS than that of the feed, we must not be looking at the latest entry that corresponds with this package, so skip the test for now.
            // If the timestamp metadata in the catalog is EQUAL to that of the feed, we are looking at the latest catalog entry that corresponds with this package, so run the test.
            return timestampCatalog.Last == timestampV2.Last;
        }

        protected abstract Task RunInternal(ValidationContext data);
    }

    /// <summary>
    /// Abstract class with the shared functionality between all <see cref="IValidator{T}"/> implementations.
    /// </summary>
    public abstract class Validator<T> : Validator, IValidator<T> where T : EndpointValidator
    {
        protected Validator(
            IDictionary<FeedType, SourceRepository> feedToSource,
            ILogger<Validator> logger)
            : base(feedToSource, logger)
        {
        }
    }
}
