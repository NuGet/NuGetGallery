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
                ShouldRunTestResult shouldRun;
                try
                {
                    shouldRun = await ShouldRunAsync(context);
                }
                catch (Exception e)
                {
                    throw new ValidationException("Threw an exception while trying to determine whether or not validation should run!", e);
                }

                switch (shouldRun)
                {
                    case ShouldRunTestResult.Yes:
                        await RunInternalAsync(context);
                        break;
                    case ShouldRunTestResult.No:
                        return new ValidationResult(this, TestResult.Skip);
                    case ShouldRunTestResult.RetryLater:
                        return new ValidationResult(this, TestResult.Pending);
                }
            }
            catch (Exception e)
            {
                return new ValidationResult(this, TestResult.Fail, e);
            }

            return new ValidationResult(this, TestResult.Pass);
        }

        /// <summary>
        /// Checks that the current batch of catalog entries contains the entry that was created from the current state of the database.
        /// </summary>
        /// <remarks>
        /// Our validations depend on the fact that the database and V3 are expected to have the same version of a package.
        /// If the catalog entry we're running validations on, which is supposed to represent the current state of V3, is less recent than the database, then we shouldn't run validations.
        /// </remarks>
        protected virtual async Task<ShouldRunTestResult> ShouldRunAsync(ValidationContext context)
        {
            if (context.Entries == null)
            {
                // If we don't have any catalog entries to use to compare timestamps, assume the database and V3 are in the same state and run validations anyway.
                return ShouldRunTestResult.Yes;
            }

            var timestampDatabase = await context.GetTimestampMetadataDatabaseAsync();
            var timestampCatalog = await PackageTimestampMetadata.FromCatalogEntries(context.Client, context.Entries);

            if (!timestampDatabase.Last.HasValue)
            {
                throw new TimestampComparisonException(timestampDatabase, timestampCatalog,
                    "Cannot get timestamp data for package from the database!");
            }

            if (!timestampCatalog.Last.HasValue)
            {
                throw new TimestampComparisonException(timestampDatabase, timestampCatalog,
                    "Cannot get timestamp data for package from the catalog!");
            }

            if (timestampCatalog.Last > timestampDatabase.Last)
            {
                throw new TimestampComparisonException(timestampDatabase, timestampCatalog,
                    "The timestamp in the catalog is newer than the timestamp in the database! This should never happen because all data flows from the feed into the catalog!");
            }

            return timestampCatalog.Last == timestampDatabase.Last
                // If the timestamp metadata in the catalog is EQUAL to that of the database, we are looking at the latest catalog entry that corresponds with this package, so run the test.
                ? ShouldRunTestResult.Yes
                // If the timestamp metadata in the catalog is LESS than that of the database, we must not be looking at the latest entry that corresponds with this package, so we must attempt this test again later with more information.
                : ShouldRunTestResult.RetryLater;
        }

        protected abstract Task RunInternalAsync(ValidationContext context);
    }

    /// <summary>
    /// Abstract class with the shared functionality between all <see cref="IValidator{T}"/> implementations.
    /// </summary>
    public abstract class Validator<T> : Validator, IValidator<T> where T : class, IEndpoint
    {
        protected Validator(T endpoint, ValidatorConfiguration config, ILogger<Validator> logger)
            : base(config, logger)
        {
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        }

        public T Endpoint { get; }
    }
}