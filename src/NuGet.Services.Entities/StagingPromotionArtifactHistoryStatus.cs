// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    public enum StagingPromotionArtifactHistoryStatus
    {
        Pending = 0,
        Processing = 1,
        Succeeded = 2,
        PromotionFailed = 3,
        Abandoned = 4,
    }
}
