// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace NuGetGallery
{
    public class TyposquattingCheckService : ITyposquattingCheckService
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

        public TyposquattingCheckService(IContentObjectService contentObjectService, IPackageService packageService, IReservedNamespaceService reservedNamespaceService)
        {
            _contentObjectService = contentObjectService ?? throw new ArgumentNullException(nameof(contentObjectService));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));

            TyposquattingCheckListLength = _contentObjectService.TyposquattingConfiguration.PackageIdChecklistLength;
        }

        public bool IsUploadedPackageIdTyposquatting(string uploadedPackageId, User uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds)
        {
            typosquattingCheckCollisionIds = new List<string>();
            if (!_contentObjectService.TyposquattingConfiguration.IsCheckEnabled || _reservedNamespaceService.GetReservedNamespacesForId(uploadedPackageId).Count != 0)
            {
                return false;
            }

            if (uploadedPackageId == null)
            {
                throw new ArgumentNullException(nameof(uploadedPackageId));
            }

            if (uploadedPackageOwner == null)
            {
                throw new ArgumentNullException(nameof(uploadedPackageOwner));
            }

            var packageRegistrations = _packageService.GetAllPackageRegistrations();
            var packagesCheckList = packageRegistrations
                .OrderByDescending(pr => pr.IsVerified)
                .ThenByDescending(pr => pr.DownloadCount)
                .Select(pr => pr.Id)
                .Take(TyposquattingCheckListLength)
                .ToList();

            var threshold = GetThreshold(uploadedPackageId);
            uploadedPackageId = TyposquattingStringNormalization.NormalizeString(uploadedPackageId);

            var collisionIds = new ConcurrentBag<string>();
            Parallel.ForEach(packagesCheckList, (packageId, loopState) =>
            {
                string normalizedPackageId = TyposquattingStringNormalization.NormalizeString(packageId);
                if (TyposquattingDistanceCalculation.IsDistanceLessThanThreshold(uploadedPackageId, normalizedPackageId, threshold))
                {
                    collisionIds.Add(packageId);
                }
            });

            if (collisionIds.Count == 0)
            {
                return false;
            }

            var collisionPackagesIdAndOwners = packageRegistrations
                .Where(pr => collisionIds.Contains(pr.Id))
                .Select(pr => new { Id = pr.Id, Owners = pr.Owners.Select(x => x.Key).ToList() })
                .ToList();

            typosquattingCheckCollisionIds = collisionPackagesIdAndOwners
                .Where(pio => !pio.Owners.Any(k => k == uploadedPackageOwner.Key))
                .Select(pio => pio.Id)
                .ToList();

            var isUserAllowedTyposquatting = collisionPackagesIdAndOwners
                .Any(pio => pio.Owners.Any(k => k == uploadedPackageOwner.Key));

            return _contentObjectService.TyposquattingConfiguration.IsBlockUsersEnabled && !isUserAllowedTyposquatting;
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

            throw new ArgumentException("There is no predefined typo-squatting threshold for this package Id: " + packageId);
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