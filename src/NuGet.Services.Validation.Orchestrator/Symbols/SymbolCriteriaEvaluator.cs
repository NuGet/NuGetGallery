// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation
{
    public class SymbolCriteriaEvaluator : ICriteriaEvaluator<SymbolPackage>
    {
        private readonly ICriteriaEvaluator<Package> _packageCriteriaEvaluator;

        public SymbolCriteriaEvaluator(ICriteriaEvaluator<Package> packageCriteriaEvaluator)
        {
            _packageCriteriaEvaluator = packageCriteriaEvaluator ?? throw new ArgumentNullException(nameof(packageCriteriaEvaluator));
        }

        public bool IsMatch(ICriteria criteria, SymbolPackage entity)
        {
            return _packageCriteriaEvaluator.IsMatch(criteria, entity.Package);
        }
    }
}
