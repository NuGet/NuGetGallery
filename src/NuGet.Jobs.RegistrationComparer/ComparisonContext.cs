// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.RegistrationComparer
{
    public class ComparisonContext
    {
        public ComparisonContext(
            string packageId,
            string leftBaseUrl,
            string rightBaseUrl,
            string leftUrl,
            string rightUrl,
            Normalizers normalizers)
        {
            PackageId = packageId;
            LeftBaseUrl = leftBaseUrl;
            RightBaseUrl = rightBaseUrl;
            LeftUrl = leftUrl;
            RightUrl = rightUrl;
            Normalizers = normalizers;
        }

        public string PackageId { get; }
        public string LeftBaseUrl { get; }
        public string RightBaseUrl { get; }
        public string LeftUrl { get; }
        public string RightUrl { get; }
        public Normalizers Normalizers { get; }
    }
}
