// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public abstract class RegistrationIndexValidator : RegistrationValidator
    {
        public RegistrationIndexValidator(
            RegistrationEndpoint endpoint,
            ValidatorConfiguration config,
            ILogger<RegistrationIndexValidator> logger)
            : base(endpoint, config, logger)
        {
        }

        protected override async Task<ShouldRunTestResult> ShouldRunAsync(ValidationContext context)
        {
            var databaseIndex = await context.GetIndexDatabaseAsync();
            var v3Index = await context.GetIndexV3Async();

            return await ShouldRunTestUtility.Combine(
                () => base.ShouldRunAsync(context),
                () => ShouldRunIndexAsync(context, databaseIndex, v3Index));
        }

        protected override async Task RunInternalAsync(ValidationContext context)
        {
            var databaseIndex = await context.GetIndexDatabaseAsync();
            var v3Index = await context.GetIndexV3Async();

            try
            {
                await CompareIndexAsync(context, databaseIndex, v3Index);
            }
            catch (Exception e)
            {
                throw new ValidationException("Registration index metadata does not match the database metadata!", e);
            }
        }

        public Task<ShouldRunTestResult> ShouldRunIndexAsync(
            ValidationContext context,
            PackageRegistrationIndexMetadata database,
            PackageRegistrationIndexMetadata v3)
        {
            return Task.FromResult(database != null && v3 != null ? ShouldRunTestResult.Yes : ShouldRunTestResult.No);
        }

        public abstract Task CompareIndexAsync(
            ValidationContext context,
            PackageRegistrationIndexMetadata database,
            PackageRegistrationIndexMetadata v3);
    }
}