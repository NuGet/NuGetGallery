// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class TyposquattingCheckListCacheService : ITyposquattingCheckListCacheService
    {
        private static readonly object Locker = new object();

        private static List<string> Cache;
        private static DateTime LastRefreshTime;
        private static TimeSpan DefaultExpireTime;

        private static int TyposquattingCheckListLength;

        public TyposquattingCheckListCacheService()
        {
            TyposquattingCheckListLength = -1;
            DefaultExpireTime = TimeSpan.FromDays(1);
            LastRefreshTime = DateTime.UtcNow;
        }

        public IReadOnlyCollection<string> GetTyposquattingCheckList(int checkListLength, IPackageService packageService)
        {
            if (checkListLength < 0)
            {
                throw new ArgumentNullException(nameof(checkListLength));
            }

            if (Cache == null || checkListLength != TyposquattingCheckListLength || IsCheckListCacheExpired())
            {
                lock (Locker)
                {
                    if (Cache == null || checkListLength != TyposquattingCheckListLength || IsCheckListCacheExpired())
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
        private bool IsCheckListCacheExpired()
        {
            return DateTime.UtcNow >= LastRefreshTime.Add(DefaultExpireTime);
        }
    }
}