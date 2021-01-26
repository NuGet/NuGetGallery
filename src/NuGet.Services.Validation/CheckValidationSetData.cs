// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The message to check an entire validation set.
    /// </summary>
    public class CheckValidationSetData
    {
        public CheckValidationSetData(Guid validationTrackingId, bool extendExpiration)
        {
            if (validationTrackingId == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(validationTrackingId));
            }

            ValidationTrackingId = validationTrackingId;
            ExtendExpiration = extendExpiration;
        }

        public Guid ValidationTrackingId { get; set; }
        public bool ExtendExpiration { get; set; }
    }
}
