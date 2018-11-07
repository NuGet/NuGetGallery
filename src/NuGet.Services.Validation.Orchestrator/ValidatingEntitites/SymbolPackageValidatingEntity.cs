// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation.Orchestrator
{
    public class SymbolPackageValidatingEntity : IValidatingEntity<SymbolPackage>
    {
        public SymbolPackageValidatingEntity(SymbolPackage entity)
        {
            EntityRecord = entity ?? throw new ArgumentNullException(nameof(entity));
        }

        public int Key => EntityRecord.Key;

        public SymbolPackage EntityRecord { get; }

        public PackageStatus Status => EntityRecord.StatusKey;

        public DateTime Created => EntityRecord.Created;

        public ValidatingType ValidatingType => ValidatingType.SymbolPackage;
    }
}
