// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
    public class CheckValidatorData
    {
        public CheckValidatorData(Guid validationId)
        {
            if (validationId == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(validationId));
            }

            ValidationId = validationId;
        }

        public Guid ValidationId { get; }
    }
}
