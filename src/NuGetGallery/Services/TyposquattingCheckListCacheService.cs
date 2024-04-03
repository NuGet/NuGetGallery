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
        private readonly ITyposquattingServiceHelper _typosquattingServiceHelper;

        private List<string> Cache;
        private DateTime LastRefreshTime;

        private int TyposquattingCheckListConfiguredLength;

        public TyposquattingCheckListCacheService(ITyposquattingServiceHelper typosquattingServiceHelper)
        {
            TyposquattingCheckListConfiguredLength = -1;
            LastRefreshTime = DateTime.MinValue;
            _typosquattingServiceHelper = typosquattingServiceHelper;
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
                        List<string> cachedPackages = packageService.GetAllPackageRegistrations()
                            .OrderByDescending(pr => pr.IsVerified)
                            .ThenByDescending(pr => pr.DownloadCount)
                            .Select(pr => pr.Id)
                            .Take(TyposquattingCheckListConfiguredLength)
                            .ToList();

                        Cache = cachedPackages
                            .Select(pr => _typosquattingServiceHelper.NormalizeString(pr))
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