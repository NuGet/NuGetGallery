// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Jobs.Validation.Common.OData
{
    public class NuGetV2PackageEqualityComparer
        : IEqualityComparer<NuGetPackage>
    {
        public bool Equals(NuGetPackage x, NuGetPackage y)
        {
            return String.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase)
                   && String.Equals(x.NormalizedVersion, y.NormalizedVersion, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(NuGetPackage obj)
        {
            unchecked
            {
                var hashCode = (obj.Id != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Id) : 0);
                hashCode = (hashCode * 397) ^ (obj.NormalizedVersion != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(obj.NormalizedVersion) : 0);
                return hashCode;
            }
        }
    }
}