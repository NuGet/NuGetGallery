// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Adapts a staged symbol attempt to the validation entity contract.
    /// </summary>
    public class StagedSymbolPackageValidatingEntity : IValidatingEntity<StagedSymbolPackage>
    {
        public StagedSymbolPackageValidatingEntity(StagedSymbolPackage entity)
        {
            EntityRecord = entity ?? throw new ArgumentNullException(nameof(entity));
        }

        public int Key => EntityRecord.Key;

        public StagedSymbolPackage EntityRecord { get; }

        public PackageStatus Status
        {
            get
            {
                switch (EntityRecord.Status)
                {
                    case StagedPackageStatus.Validating:
                        return PackageStatus.Validating;
                    case StagedPackageStatus.FailedValidation:
                        return PackageStatus.FailedValidation;
                    case StagedPackageStatus.Ready:
                    case StagedPackageStatus.Superseded:
                    case StagedPackageStatus.Deleted:
                        return PackageStatus.Available;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(EntityRecord.Status));
                }
            }
        }

        public DateTime Created => EntityRecord.UploadedDate;

        public ValidatingType ValidatingType => ValidatingType.StagedSymbolPackage;
    }
}
