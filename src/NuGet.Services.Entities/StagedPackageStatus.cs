// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    public enum StagedPackageStatus
    {
        Validating = 0,
        Ready = 1,
        FailedValidation = 2,
        Superseded = 3,
        Deleted = 4,
    }
}
