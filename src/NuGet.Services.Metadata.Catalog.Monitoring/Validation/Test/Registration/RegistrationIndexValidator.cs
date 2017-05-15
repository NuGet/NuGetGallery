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
    public abstract class RegistrationIndexValidator : RegistrationValidator
    {
        public RegistrationIndexValidator(
            IDictionary<FeedType, SourceRepository> feedToSource,
            ILogger<RegistrationIndexValidator> logger) : base(feedToSource, logger)
        {
        }

        protected override async Task<bool> ShouldRun(ValidationContext data)
        {
            return await ShouldRunIndex(
                data, 
                await GetIndex(V2Resource, data), 
                await GetIndex(V3Resource, data));
        }

        protected override async Task RunInternal(ValidationContext data)
        {
            try
            {
                await CompareIndex(
                    data,
                    await GetIndex(V2Resource, data),
                    await GetIndex(V3Resource, data));
            }
            catch (Exception e)
            {
                 throw new ValidationException("Registration index metadata does not match the FindPackagesById metadata!", e);
            }
        }

        public abstract Task<bool> ShouldRunIndex(ValidationContext data, PackageRegistrationIndexMetadata v2, PackageRegistrationIndexMetadata v3);

        public abstract Task CompareIndex(ValidationContext data, PackageRegistrationIndexMetadata v2, PackageRegistrationIndexMetadata v3);
    }
}
