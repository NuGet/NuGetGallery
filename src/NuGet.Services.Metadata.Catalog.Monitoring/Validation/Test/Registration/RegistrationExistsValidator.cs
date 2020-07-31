// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class RegistrationExistsValidator : RegistrationLeafValidator
    {
        public RegistrationExistsValidator(
            RegistrationEndpoint endpoint,
            ValidatorConfiguration config,
            ILogger<RegistrationExistsValidator> logger)
            : base(endpoint, config, logger)
        {
        }

        public override Task<ShouldRunTestResult> ShouldRunLeafAsync(
            ValidationContext context,
            PackageRegistrationLeafMetadata database,
            PackageRegistrationLeafMetadata v3)
        {
            return Task.FromResult(ShouldRunTestResult.Yes);
        }

        public override Task CompareLeafAsync(
            ValidationContext context,
            PackageRegistrationLeafMetadata database,
            PackageRegistrationLeafMetadata v3)
        {
            var databaseExists = database != null;
            var v3Exists = v3 != null;
            var completedTask = Task.FromResult(0);

            if (databaseExists != v3Exists)
            {
                // Currently, leaf nodes are not deleted after a package is deleted.
                // This is a known bug. Do not fail validations because of it.
                // See https://github.com/NuGet/NuGetGallery/issues/4475
                if (v3Exists && !(v3 is PackageRegistrationIndexMetadata))
                {
                    Logger.LogInformation("{PackageId} {PackageVersion} doesn't exist in the database but has a leaf node in V3!", context.Package.Id, context.Package.Version);
                    return completedTask;
                }

                const string existsString = "exists";
                const string doesNotExistString = "doesn't exist";

                throw new MetadataInconsistencyException<PackageRegistrationLeafMetadata>(
                    database,
                    v3,
                    $"Database {(databaseExists ? existsString : doesNotExistString)} but V3 {(v3Exists ? existsString : doesNotExistString)}!");
            }

            return completedTask;
        }
    }
}