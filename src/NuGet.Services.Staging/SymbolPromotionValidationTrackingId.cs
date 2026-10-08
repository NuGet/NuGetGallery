// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NuGet.Services.Staging
{
    /// <summary>
    /// Derives a stable validation-set identity without persisting a second promotion identifier.
    /// Retries of the same accepted promotion reuse this identity so the symbol orchestrator
    /// can resume the existing ingestion validation set instead of creating another.
    /// </summary>
    public static class SymbolPromotionValidationTrackingId
    {
        public static Guid Create(Guid promotionId, int stagedSymbolPackageKey)
        {
            if (promotionId == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(promotionId));
            }

            if (stagedSymbolPackageKey <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stagedSymbolPackageKey));
            }

            var name = string.Format(CultureInfo.InvariantCulture, "symbol-promotion:{0:N}:{1}", promotionId, stagedSymbolPackageKey);
            using (var hash = SHA256.Create())
            {
                var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(name));
                var identity = new byte[16];
                Array.Copy(bytes, identity, identity.Length);
                return new Guid(identity);
            }
        }
    }
}
