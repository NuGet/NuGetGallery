// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    public enum StagedPackageStatus
    {
        Validating = 0,
        Ready = 1,
        ValidationFailed = 2,
    }
}
