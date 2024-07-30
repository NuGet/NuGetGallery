// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Monitoring.Model;
using NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Registration
{
    public class RegistrationDeprecationValidator : RegistrationIndexValidator
    {
        public RegistrationDeprecationValidator(
            RegistrationEndpoint endpoint,
            ValidatorConfiguration config,
            ILogger<RegistrationDeprecationValidator> logger)
            : base(endpoint, config, logger)
        {
        }

        public override Task CompareIndexAsync(ValidationContext context, PackageRegistrationIndexMetadata database, PackageRegistrationIndexMetadata v3)
        {
            var exceptions = new List<MetadataFieldInconsistencyException<PackageRegistrationIndexMetadata>>();

            if (database.Deprecation == null && v3.Deprecation == null)
            {
                return Task.CompletedTask;
            }
            else if (database.Deprecation == null || v3.Deprecation == null)
            {
                throw new MetadataFieldInconsistencyException<PackageRegistrationIndexMetadata>(
                    database, v3,
                    nameof(PackageRegistrationIndexMetadata.Deprecation),
                    i => i.Deprecation);
            }

            if (!database.Deprecation.Reasons.OrderBy(r => r).SequenceEqual(v3.Deprecation.Reasons.OrderBy(r => r)))
            {
                AddDeprecationInconsistencyException(
                    exceptions,
                    database, v3,
                    nameof(PackageRegistrationDeprecationMetadata.Reasons),
                    d => d.Reasons);
            }

            if (database.Deprecation.Message != v3.Deprecation.Message)
            {
                AddDeprecationInconsistencyException(
                    exceptions,
                    database, v3,
                    nameof(PackageRegistrationDeprecationMetadata.Message),
                    d => d.Message);
            }

            CompareIndexAlternatePackage(exceptions, database, v3);

            if (exceptions.Any())
            {
                throw new AggregateMetadataInconsistencyException<PackageRegistrationIndexMetadata>(exceptions);
            }

            return Task.CompletedTask;
        }

        private void CompareIndexAlternatePackage(
            List<MetadataFieldInconsistencyException<PackageRegistrationIndexMetadata>> exceptions,
            PackageRegistrationIndexMetadata database,
            PackageRegistrationIndexMetadata v3)
        {
            if (database.Deprecation.AlternatePackage == null && v3.Deprecation.AlternatePackage == null)
            {
                return;
            }
            else if (database.Deprecation.AlternatePackage == null || v3.Deprecation.AlternatePackage == null)
            {
                AddDeprecationInconsistencyException(
                    exceptions,
                    database, v3,
                    nameof(PackageRegistrationDeprecationMetadata.AlternatePackage),
                    d => d.AlternatePackage);

                return;
            }

            if (database.Deprecation.AlternatePackage.Id != v3.Deprecation.AlternatePackage.Id)
            {
                AddAlternatePackageInconsistencyException(
                    exceptions,
                    database, v3,
                    nameof(PackageRegistrationAlternatePackageMetadata.Id),
                    a => a.Id);
            }

            if (database.Deprecation.AlternatePackage.Range != v3.Deprecation.AlternatePackage.Range)
            {
                AddAlternatePackageInconsistencyException(
                    exceptions,
                    database, v3,
                    nameof(PackageRegistrationAlternatePackageMetadata.Range),
                    a => a.Range);
            }
        }

        private void AddDeprecationInconsistencyException(
            List<MetadataFieldInconsistencyException<PackageRegistrationIndexMetadata>> list,
            PackageRegistrationIndexMetadata database,
            PackageRegistrationIndexMetadata v3,
            string deprecationFieldName,
            Func<PackageRegistrationDeprecationMetadata, object> getDeprecationField)
        {
            var exception = new MetadataFieldInconsistencyException<PackageRegistrationIndexMetadata>(
                database, v3,
                nameof(PackageRegistrationIndexMetadata.Deprecation) + "." + deprecationFieldName,
                i => getDeprecationField(i.Deprecation));

            list.Add(exception);
        }

        private void AddAlternatePackageInconsistencyException(
            List<MetadataFieldInconsistencyException<PackageRegistrationIndexMetadata>> list,
            PackageRegistrationIndexMetadata database,
            PackageRegistrationIndexMetadata v3,
            string alternatePackageField,
            Func<PackageRegistrationAlternatePackageMetadata, object> getAlternatePackageField)
        {
            AddDeprecationInconsistencyException(
                list,
                database, v3,
                nameof(PackageRegistrationDeprecationMetadata.AlternatePackage) + "." + alternatePackageField,
                d => getAlternatePackageField(d.AlternatePackage));
        }
    }
}
