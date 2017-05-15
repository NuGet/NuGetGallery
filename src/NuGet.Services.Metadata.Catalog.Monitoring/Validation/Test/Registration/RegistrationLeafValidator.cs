// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public abstract class RegistrationLeafValidator : RegistrationValidator
    {
        public RegistrationLeafValidator(
            IDictionary<FeedType, SourceRepository> feedToSource, 
            ILogger<RegistrationLeafValidator> logger) : base(feedToSource, logger)
        {
        }

        protected override async Task<bool> ShouldRun(ValidationContext data)
        {
            return 
                await ShouldRunLeaf(
                    data,
                    await GetIndex(V2Resource, data),
                    await GetIndex(V3Resource, data)) && 

                await ShouldRunLeaf(
                    data,
                    await GetLeaf(V2Resource, data),
                    await GetLeaf(V3Resource, data));
        }

        protected override async Task RunInternal(ValidationContext data)
        {
            var exceptions = new List<Exception>();

            try
            {
                await CompareLeaf(
                    data,
                    await GetIndex(V2Resource, data),
                    await GetIndex(V3Resource, data));
            }
            catch (Exception e)
            {
                exceptions.Add(new ValidationException("Registration index metadata does not match the FindPackagesById metadata!", e));
            }

            try
            {
                await CompareLeaf(
                    data,
                    await GetLeaf(V2Resource, data),
                    await GetLeaf(V3Resource, data));
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

        public abstract Task<bool> ShouldRunLeaf(ValidationContext data, PackageRegistrationLeafMetadata v2, PackageRegistrationLeafMetadata v3);

        public abstract Task CompareLeaf(ValidationContext data, PackageRegistrationLeafMetadata v2, PackageRegistrationLeafMetadata v3);
    }
}
