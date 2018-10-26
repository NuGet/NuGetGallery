// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class TyposquattingService : ITyposquattingService
    {
        private static readonly IReadOnlyList<ThresholdInfo> ThresholdsList = new List<ThresholdInfo>
        {
            new ThresholdInfo (lowerBound: 0, upperBound: 30, threshold: 0),
            new ThresholdInfo (lowerBound: 30, upperBound: 50, threshold: 1),
            new ThresholdInfo (lowerBound: 50, upperBound: 129, threshold: 2)
        };
        private static int TyposquattingCheckListLength;

        private readonly IContentObjectService _contentObjectService;
        private readonly IPackageService _packageService;
        private readonly IReservedNamespaceService _reservedNamespaceService;
        private readonly ITelemetryService _telemetryService;
        private readonly TyposquattingCheckListCache _typosquattingCheckListCache;

        public TyposquattingService(IContentObjectService contentObjectService,
                                    IPackageService packageService,
                                    IReservedNamespaceService reservedNamespaceService,
                                    ITelemetryService telemetryService,
                                    TyposquattingCheckListCache typosquattingCheckListCache)
        {
            _contentObjectService = contentObjectService ?? throw new ArgumentNullException(nameof(contentObjectService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _typosquattingCheckListCache = typosquattingCheckListCache ?? throw new ArgumentNullException(nameof(typosquattingCheckListCache));

            TyposquattingCheckListLength = _contentObjectService.TyposquattingConfiguration.PackageIdChecklistLength;
            _typosquattingCheckListCache.DefaultExpireTime = TimeSpan.FromDays(1);
            _typosquattingCheckListCache.LastRefreshTime = DateTime.UtcNow;
        }

        public bool IsUploadedPackageIdTyposquatting(string uploadedPackageId, User uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds)
        {
            typosquattingCheckCollisionIds = new List<string>();
            var wasUploadBlocked = false;

            if (!_contentObjectService.TyposquattingConfiguration.IsCheckEnabled || _reservedNamespaceService.GetReservedNamespacesForId(uploadedPackageId).Any())
            {
                return wasUploadBlocked;
            }
            if (uploadedPackageId == null)
            {
                throw new ArgumentNullException(nameof(uploadedPackageId));
            }
            if (uploadedPackageOwner == null)
            {
                throw new ArgumentNullException(nameof(uploadedPackageOwner));
            }

            Stopwatch checklistRetrievalStopwatch = null;
            if (_typosquattingCheckListCache.Cache == null || IsCheckListCacheExpired())
            {
                lock(_typosquattingCheckListCache.Locker)
                {
                    if (_typosquattingCheckListCache.Cache == null || IsCheckListCacheExpired())
                    {
                        checklistRetrievalStopwatch = Stopwatch.StartNew();
                        _typosquattingCheckListCache.Cache = _packageService.GetAllPackageRegistrations()
                            .OrderByDescending(pr => pr.IsVerified)
                            .ThenByDescending(pr => pr.DownloadCount)
                            .Select(pr => pr.Id)
                            .Take(TyposquattingCheckListLength)
                            .ToList();
                        checklistRetrievalStopwatch.Stop();

                        _typosquattingCheckListCache.LastRefreshTime = DateTime.UtcNow;
                    }
                }
            }
            var packageIdsCheckList = _typosquattingCheckListCache.Cache;

            var algorithmProcessingStopwatch = Stopwatch.StartNew();
            var threshold = GetThreshold(uploadedPackageId);
            var normalizedUploadedPackageId = TyposquattingStringNormalization.NormalizeString(uploadedPackageId);
            var collisionIds = new ConcurrentBag<string>();
            Parallel.ForEach(packageIdsCheckList, (packageId, loopState) =>
            {
                string normalizedPackageId = TyposquattingStringNormalization.NormalizeString(packageId);
                if (TyposquattingDistanceCalculation.IsDistanceLessThanThreshold(normalizedUploadedPackageId, normalizedPackageId, threshold))
                {
                    collisionIds.Add(packageId);
                }
            });
            algorithmProcessingStopwatch.Stop();

            _telemetryService.TrackMetricForTyposquattingAlgorithmProcessingTime(uploadedPackageId, algorithmProcessingStopwatch.Elapsed);
            var totalTime = algorithmProcessingStopwatch.Elapsed;

            if (checklistRetrievalStopwatch != null)
            {
                _telemetryService.TrackMetricForTyposquattingChecklistRetrievalTime(uploadedPackageId, checklistRetrievalStopwatch.Elapsed);
                totalTime = totalTime.Add(checklistRetrievalStopwatch.Elapsed);
            }

            if (collisionIds.Count == 0)
            {
                _telemetryService.TrackMetricForTyposquattingCheckResultAndTotalTime(
                    uploadedPackageId,
                    totalTime,
                    wasUploadBlocked,
                    typosquattingCheckCollisionIds,
                    TyposquattingCheckListLength);

                return false;
            }

            var ownersCheckStopwatch = Stopwatch.StartNew();
            var collisionPackagesIdAndOwners = _packageService.GetAllPackageRegistrations()
                .Where(pr => collisionIds.Contains(pr.Id))
                .Select(pr => new { Id = pr.Id, Owners = pr.Owners.Select(x => x.Key).ToList() })
                .ToList();

            typosquattingCheckCollisionIds = collisionPackagesIdAndOwners
                .Where(pio => !pio.Owners.Any(k => k == uploadedPackageOwner.Key))
                .Select(pio => pio.Id)
                .ToList();

            /// <summary>
            /// The following statement is used to double check whether the collision Id belongs to the same user who is uploading the package.
            /// The current policy is that if the user has the ownership of any of the collision packages, we will pass the package.
            /// The reason is that maybe this user is trying to update an existing package who is owned by themselves.
            /// Example:
            /// User "a" is uploading a package named "xyz", while "xyz" collides with existing packages "xyzz" (owned by "a", "b", "c"), "xyyz" (owned by "b"), "xxyz" (owned by "b", "c").
            /// We will pass this package because "a" has the ownership of package "xyzz" even though this package Id collides with "xyyz" and "xxyz".
            /// The "typosquattingCheckCollisionIds" will be saved as "xyyz" and "xxyz" because this package collides with these two packages which are not owned by "a", while "xyzz" will not be saved as "a" owns it.
            /// </summary>
            var isUserAllowedTyposquatting = collisionPackagesIdAndOwners
                .Any(pio => pio.Owners.Any(k => k == uploadedPackageOwner.Key));

            wasUploadBlocked = _contentObjectService.TyposquattingConfiguration.IsBlockUsersEnabled && !isUserAllowedTyposquatting;
            ownersCheckStopwatch.Stop();

            _telemetryService.TrackMetricForTyposquattingOwnersCheckTime(uploadedPackageId, ownersCheckStopwatch.Elapsed);
            totalTime = totalTime.Add(ownersCheckStopwatch.Elapsed);

            _telemetryService.TrackMetricForTyposquattingCheckResultAndTotalTime(
                    uploadedPackageId,
                    totalTime,
                    wasUploadBlocked,
                    typosquattingCheckCollisionIds,
                    TyposquattingCheckListLength);

            return wasUploadBlocked;
        }
        private bool IsCheckListCacheExpired()
        {
            return DateTime.UtcNow >= _typosquattingCheckListCache.LastRefreshTime.Add(_typosquattingCheckListCache.DefaultExpireTime);
        }
        private static int GetThreshold(string packageId)
        {
            foreach (var thresholdInfo in ThresholdsList)
            {
                if (packageId.Length >= thresholdInfo.LowerBound && packageId.Length < thresholdInfo.UpperBound)
                {
                    return thresholdInfo.Threshold;
                }
            }

            throw new ArgumentException(String.Format("There is no predefined typo-squatting threshold for this package Id: {0}", packageId));
        }
    }
    public class ThresholdInfo
    {
        public int LowerBound { get; }
        public int UpperBound { get; }
        public int Threshold { get; }
        public ThresholdInfo(int lowerBound, int upperBound, int threshold)
        {
            LowerBound = lowerBound;
            UpperBound = upperBound;
            Threshold = threshold;
        }        
    }
}