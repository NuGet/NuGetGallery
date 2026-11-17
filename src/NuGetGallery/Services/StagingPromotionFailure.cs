// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Shares the current promotion-failure explanation between staging controls and API responses.
    /// </summary>
    internal static class StagingPromotionFailure
    {
        internal static StagingBlockerResponse GetBlocker(StagedPackageStatus status, bool symbols, bool grouped)
        {
            if (status != StagedPackageStatus.PromotionFailed)
            {
                return null;
            }

            string message;
            if (grouped)
            {
                message = "Some packages failed promotion. Replace or remove them before promoting this group again.";
            }
            else if (symbols)
            {
                message = "Symbol promotion failed. Its parent package is unchanged. Replace or remove the staged symbols before trying again.";
            }
            else
            {
                message = "Package promotion failed. Replace or remove the staged package before trying again.";
            }

            return new StagingBlockerResponse(symbols ? "SymbolPromotionFailed" : "PackagePromotionFailed", message);
        }
    }
}
