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
        public RegistrationLeafValidator(ValidatorConfiguration config, ILogger<RegistrationLeafValidator> logger)
            : base(config, logger)
        {
        }

        protected override async Task<bool> ShouldRunAsync(ValidationContext context)
        {
            var v2Index = await context.GetIndexV2Async();
            var v3Index = await context.GetIndexV3Async();
            var v2Leaf = await context.GetLeafV2Async();
            var v3Leaf = await context.GetLeafV3Async();

            return await base.ShouldRunAsync(context)
                && await ShouldRunLeafAsync(context, v2Index, v3Index)
                && await ShouldRunLeafAsync(context, v2Leaf, v3Leaf);
        }

        protected override async Task RunInternalAsync(ValidationContext context)
        {
            var exceptions = new List<Exception>();

            var v2Index = await context.GetIndexV2Async();
            var v3Index = await context.GetIndexV3Async();

            try
            {
                await CompareLeafAsync(context, v2Index, v3Index);
            }
            catch (Exception e)
            {
                exceptions.Add(new ValidationException("Registration index metadata does not match the FindPackagesById metadata!", e));
            }

            var v2Leaf = await context.GetLeafV2Async();
            var v3Leaf = await context.GetLeafV3Async();

            try
            {
                await CompareLeafAsync(context, v2Leaf, v3Leaf);
            }
            catch (Exception e)
            {
                exceptions.Add(new ValidationException("Registration leaf metadata does not match the Packages(Id='...',Version='...') metadata!", e));
            }

            if (exceptions.Any())
            {
                throw new AggregateException(exceptions);
            }
        }

        public abstract Task<bool> ShouldRunLeafAsync(
            ValidationContext context,
            PackageRegistrationLeafMetadata v2,
            PackageRegistrationLeafMetadata v3);

        public abstract Task CompareLeafAsync(
            ValidationContext context,
            PackageRegistrationLeafMetadata v2,
            PackageRegistrationLeafMetadata v3);
    }
}