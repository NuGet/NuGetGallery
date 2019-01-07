// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public abstract class RegistrationIndexValidator : RegistrationValidator
    {
        public RegistrationIndexValidator(ValidatorConfiguration config, ILogger<RegistrationIndexValidator> logger)
            : base(config, logger)
        {
        }

        protected override async Task<bool> ShouldRunAsync(ValidationContext context)
        {
            var v2Index = await context.GetIndexV2Async();
            var v3Index = await context.GetIndexV3Async();

            return await base.ShouldRunAsync(context) && await ShouldRunIndexAsync(context, v2Index, v3Index);
        }

        protected override async Task RunInternalAsync(ValidationContext context)
        {
            var v2Index = await context.GetIndexV2Async();
            var v3Index = await context.GetIndexV3Async();

            try
            {
                await CompareIndexAsync(context, v2Index, v3Index);
            }
            catch (Exception e)
            {
                throw new ValidationException("Registration index metadata does not match the FindPackagesById metadata!", e);
            }
        }

        public Task<bool> ShouldRunIndexAsync(
            ValidationContext context,
            PackageRegistrationIndexMetadata v2,
            PackageRegistrationIndexMetadata v3)
        {
            return Task.FromResult(v2 != null && v3 != null);
        }

        public abstract Task CompareIndexAsync(
            ValidationContext context,
            PackageRegistrationIndexMetadata v2,
            PackageRegistrationIndexMetadata v3);
    }
}