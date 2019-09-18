// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public abstract class RegistrationLeafValidator : RegistrationValidator
    {
        public RegistrationLeafValidator(
            RegistrationEndpoint endpoint,
            ValidatorConfiguration config,
            ILogger<RegistrationLeafValidator> logger)
            : base(endpoint, config, logger)
        {
        }

        protected override async Task<ShouldRunTestResult> ShouldRunAsync(ValidationContext context)
        {
            var databaseIndex = await context.GetIndexDatabaseAsync();
            var v3Index = await context.GetIndexV3Async();
            var databaseLeaf = await context.GetLeafDatabaseAsync();
            var v3Leaf = await context.GetLeafV3Async();

            return await ShouldRunTestUtility.Combine(
                () => base.ShouldRunAsync(context),
                () => ShouldRunLeafAsync(context, databaseIndex, v3Index),
                () => ShouldRunLeafAsync(context, databaseLeaf, v3Leaf));
        }

        protected override async Task RunInternalAsync(ValidationContext context)
        {
            var exceptions = new List<Exception>();

            var databaseIndex = await context.GetIndexDatabaseAsync();
            var v3Index = await context.GetIndexV3Async();

            try
            {
                await CompareLeafAsync(context, databaseIndex, v3Index);
            }
            catch (Exception e)
            {
                exceptions.Add(new ValidationException("Registration index metadata does not match the database!", e));
            }

            var databaseLeaf = await context.GetLeafDatabaseAsync();
            var v3Leaf = await context.GetLeafV3Async();

            try
            {
                await CompareLeafAsync(context, databaseLeaf, v3Leaf);
            }
            catch (Exception e)
            {
                exceptions.Add(new ValidationException("Registration leaf metadata does not match the database!", e));
            }

            if (exceptions.Any())
            {
                throw new AggregateException(exceptions);
            }
        }

        public abstract Task<ShouldRunTestResult> ShouldRunLeafAsync(
            ValidationContext context,
            PackageRegistrationLeafMetadata database,
            PackageRegistrationLeafMetadata v3);

        public abstract Task CompareLeafAsync(
            ValidationContext context,
            PackageRegistrationLeafMetadata database,
            PackageRegistrationLeafMetadata v3);
    }
}