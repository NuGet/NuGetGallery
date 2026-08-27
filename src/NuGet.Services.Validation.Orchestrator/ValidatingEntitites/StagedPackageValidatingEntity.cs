// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation.Orchestrator
{
    public class StagedPackageValidatingEntity : IValidatingEntity<StagedPackage>
    {
        public StagedPackageValidatingEntity(StagedPackage entity)
        {
            EntityRecord = entity ?? throw new ArgumentNullException(nameof(entity));
        }

        public int Key => EntityRecord.PackageKey;

        public StagedPackage EntityRecord { get; }

        public PackageStatus Status
        {
            get
            {
                switch (EntityRecord.Status)
                {
                    case StagedPackageStatus.Validating:
                        return PackageStatus.Validating;
                    case StagedPackageStatus.Ready:
                        return PackageStatus.Available;
                    case StagedPackageStatus.FailedValidation:
                        return PackageStatus.FailedValidation;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(EntityRecord.Status));
                }
            }
        }

        public DateTime Created => EntityRecord.UploadedDate;

        public ValidatingType ValidatingType => ValidatingType.StagedPackage;
    }
}
