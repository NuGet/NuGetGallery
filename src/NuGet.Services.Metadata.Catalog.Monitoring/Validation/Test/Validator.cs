// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Abstract class with the shared functionality between all <see cref="IValidator"/> implementations.
    /// </summary>
    public abstract class Validator : IValidator
    {
        protected readonly ValidatorConfiguration Config;
        protected readonly ILogger<Validator> Logger;
        protected readonly Common.ILogger CommonLogger;

        public virtual string Name
        {
            get
            {
                return GetType().FullName;
            }
        }

        protected Validator(ValidatorConfiguration config, ILogger<Validator> logger)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            CommonLogger = logger.AsCommon();
        }

        public async Task<ValidationResult> ValidateAsync(ValidationContext context)
        {
            try
            {
                bool shouldRun = false;
                try
                {
                    shouldRun = await ShouldRunAsync(context);
                }
                catch (Exception e)
                {
                    throw new ValidationException("Threw an exception while trying to determine whether or not validation should run!", e);
                }

                if (shouldRun)
                {
                    await RunInternalAsync(context);
                }
                else
                {
                    return new ValidationResult(this, TestResult.Skip);
                }
            }
            catch (Exception e)
            {
                return new ValidationResult(this, TestResult.Fail, e);
            }

            return new ValidationResult(this, TestResult.Pass);
        }

        /// <summary>
        /// Checks that the current batch of catalog entries contains the entry that was created from the current state of the V2 feed.
        /// </summary>
        protected virtual async Task<bool> ShouldRunAsync(ValidationContext context)
        {
            var timestampV2 = await context.GetTimestampMetadataV2Async();
            var timestampCatalog = await PackageTimestampMetadata.FromCatalogEntries(context.Client, context.Entries);

            if (!timestampV2.Last.HasValue)
            {
                throw new TimestampComparisonException(timestampV2, timestampCatalog,
                    "Cannot get timestamp data for package from the V2 feed!");
            }

            if (!timestampCatalog.Last.HasValue)
            {
                throw new TimestampComparisonException(timestampV2, timestampCatalog,
                    "Cannot get timestamp data for package from the catalog!");
            }

            if (timestampCatalog.Last > timestampV2.Last)
            {
                throw new TimestampComparisonException(timestampV2, timestampCatalog,
                    "The timestamp in the catalog is newer than the timestamp in the feed! This should never happen because all data flows from the feed into the catalog!");
            }

            // If the timestamp metadata in the catalog is LESS than that of the feed, we must not be looking at the latest entry that corresponds with this package, so skip the test for now.
            // If the timestamp metadata in the catalog is EQUAL to that of the feed, we are looking at the latest catalog entry that corresponds with this package, so run the test.
            return timestampCatalog.Last == timestampV2.Last;
        }

        protected abstract Task RunInternalAsync(ValidationContext context);
    }

    /// <summary>
    /// Abstract class with the shared functionality between all <see cref="IValidator{T}"/> implementations.
    /// </summary>
    public abstract class Validator<T> : Validator, IValidator<T> where T : IEndpoint
    {
        protected Validator(ValidatorConfiguration config, ILogger<Validator> logger)
            : base(config, logger)
        {
        }
    }
}