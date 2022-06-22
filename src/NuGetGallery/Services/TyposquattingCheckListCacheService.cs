// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class TyposquattingCheckListCacheService : ITyposquattingCheckListCacheService
    {
        private readonly object Locker = new object();

        private List<string> Cache;
        private DateTime LastRefreshTime;

        private int TyposquattingCheckListConfiguredLength;

        public TyposquattingCheckListCacheService()
        {
            TyposquattingCheckListConfiguredLength = -1;
            LastRefreshTime = DateTime.MinValue;
        }

        public IReadOnlyCollection<string> GetTyposquattingCheckList(int checkListConfiguredLength, TimeSpan checkListExpireTime, IPackageService packageService)
        {
            if (packageService == null)
            {
                throw new ArgumentNullException(nameof(packageService));
            }
            if (checkListConfiguredLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(checkListConfiguredLength), "Negative values are not supported.");
            }
            if (checkListExpireTime.CompareTo(TimeSpan.Zero) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(checkListExpireTime), "Negative values are not supported.");
            }

            if (ShouldCacheBeUpdated(checkListConfiguredLength, checkListExpireTime))
            {
                lock (Locker)
                {
                    if (ShouldCacheBeUpdated(checkListConfiguredLength, checkListExpireTime))
                    {
                        TyposquattingCheckListConfiguredLength = checkListConfiguredLength;

                        Cache = packageService.GetAllPackageRegistrations()
                            .OrderByDescending(pr => pr.IsVerified)
                            .ThenByDescending(pr => pr.DownloadCount)
                            .Select(pr => pr.Id)
                            .Take(TyposquattingCheckListConfiguredLength)
                            .ToList();

                        LastRefreshTime = DateTime.UtcNow;
                    }
                }
            }

            return Cache;
        }

        private bool ShouldCacheBeUpdated(int checkListConfiguredLength, TimeSpan checkListExpireTime)
        {
            return Cache == null || checkListConfiguredLength != TyposquattingCheckListConfiguredLength || IsCheckListCacheExpired(checkListExpireTime);
        }

        private bool IsCheckListCacheExpired(TimeSpan checkListExpireTime)
        {
            return DateTime.UtcNow >= LastRefreshTime.Add(checkListExpireTime);
        }
    }
}