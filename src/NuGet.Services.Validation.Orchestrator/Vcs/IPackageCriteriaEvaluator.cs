// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery;

namespace NuGet.Services.Validation.Vcs
{
    /// <summary>
    /// Evaluates whether a given package matches some criteria.
    /// </summary>
    public interface IPackageCriteriaEvaluator
    {
        bool IsMatch(IPackageCriteria criteria, Package package);
    }
}
