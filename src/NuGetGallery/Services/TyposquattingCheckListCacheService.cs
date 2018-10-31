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

        private int TyposquattingCheckListLength;

        public TyposquattingCheckListCacheService()
        {
            TyposquattingCheckListLength = -1;
            LastRefreshTime = DateTime.MinValue;
        }

        public IReadOnlyCollection<string> GetTyposquattingCheckList(int checkListLength, double checkListExpireTimeInHours, IPackageService packageService)
        {
            if (checkListLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(checkListLength), "Negative values are not supported.");
            }
            if (packageService == null)
            {
                throw new ArgumentNullException(nameof(packageService));
            }

            if (Cache == null || checkListLength != TyposquattingCheckListLength || IsCheckListCacheExpired(checkListExpireTimeInHours))
            {
                lock (Locker)
                {
                    if (Cache == null || checkListLength != TyposquattingCheckListLength || IsCheckListCacheExpired(checkListExpireTimeInHours))
                    {
                        TyposquattingCheckListLength = checkListLength;

                        Cache = packageService.GetAllPackageRegistrations()
                            .OrderByDescending(pr => pr.IsVerified)
                            .ThenByDescending(pr => pr.DownloadCount)
                            .Select(pr => pr.Id)
                            .Take(TyposquattingCheckListLength)
                            .ToList();

                        LastRefreshTime = DateTime.UtcNow;
                    }
                }
            }

            return Cache;
        }

        private bool IsCheckListCacheExpired(double checkListExpireTimeInHours)
        {
            return DateTime.UtcNow >= LastRefreshTime.Add(TimeSpan.FromHours(checkListExpireTimeInHours));
        }
    }
}