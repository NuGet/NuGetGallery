// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
    public class PackageValidationMessageData
    {
        public PackageValidationMessageData(string packageId, string packageVersion, Guid validationTrackingId)
        {
            if (validationTrackingId == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(validationTrackingId));
            }

            PackageId = packageId ?? throw new ArgumentNullException(nameof(packageId));
            PackageVersion = packageVersion ?? throw new ArgumentNullException(nameof(packageVersion));
            ValidationTrackingId = validationTrackingId;
        }

        public string PackageId { get; }
        public string PackageVersion { get; }
        public Guid ValidationTrackingId { get; }
    }
}
