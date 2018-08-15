// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class TyposquattingCheckService : ITyposquattingCheckService
    {
        // TODO: Length of checklist will be saved in the configuration file.
        // https://github.com/NuGet/Engineering/issues/1645
        private static int TyposquattingCheckListLength = 20000;

        // TODO: Threshold parameters will be saved in the configuration file.
        // https://github.com/NuGet/Engineering/issues/1645
        private static List<ThresholdInfo> _thresholdsList = new List<ThresholdInfo>
        {
            new ThresholdInfo { LowerBound = 0, UpperBound = 30, Threshold = 0 },
            new ThresholdInfo { LowerBound = 30, UpperBound = 50, Threshold = 1 },
            new ThresholdInfo { LowerBound = 50, UpperBound = 120, Threshold = 2 }
        };

        private static List<string> PackagesCheckList { get; set; } = new List<string>();
        private static ConcurrentDictionary<string, string> NormalizedPackageIdDict { get; set; } = new ConcurrentDictionary<string, string>();

        private readonly ITyposquattingUserService _userTyposquattingService;
        private readonly ITyposquattingPackagesCheckListService _typosquattingPackagesCheckListService;
 
        public TyposquattingCheckService(ITyposquattingUserService typosquattingUserService, ITyposquattingPackagesCheckListService typosquattingPackageListService)
        {
            _userTyposquattingService = typosquattingUserService ?? throw new ArgumentNullException(nameof(typosquattingUserService));
            _typosquattingPackagesCheckListService = typosquattingPackageListService ?? throw new ArgumentNullException(nameof(typosquattingPackageListService));
        }

        public bool IsUploadedPackageIdTyposquatting(string uploadedPackageId, User uploadedPackageOwner)
        {
            if (uploadedPackageId == null)
            {
                throw new ArgumentNullException(nameof(uploadedPackageId));
            }

            if (uploadedPackageOwner == null)
            {
                throw new ArgumentNullException(nameof(uploadedPackageOwner));
            }

            PackagesCheckList = _typosquattingPackagesCheckListService.GetTyposquattingChecklist(TyposquattingCheckListLength);

            var threshold = GetThreshold(uploadedPackageId);
            uploadedPackageId = TyposquattingStringNormalization.NormalizeString(uploadedPackageId);

            var collisionPackageIds = new ConcurrentBag<string>();
            Parallel.ForEach(PackagesCheckList, (packageId, loopState) =>
            {
                // TODO: handle the package which is owned by an organization. 
                // https://github.com/NuGet/Engineering/issues/1656
                string normalizedPackageId = NormalizedPackageIdDict.GetOrAdd(packageId, TyposquattingStringNormalization.NormalizeString);
                if (TyposquattingDistanceCalculation.IsDistanceLessThanThreshold(uploadedPackageId, normalizedPackageId, threshold))
                {
                    collisionPackageIds.Add(packageId);
                }
            });

            foreach (var packageId in collisionPackageIds)
            {
                if (!_userTyposquattingService.CanUserTyposquat(packageId, uploadedPackageOwner.Username))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetThreshold(string packageId)
        {
            foreach (var thresholdInfo in _thresholdsList)
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
        public int LowerBound { get; set; }
        public int UpperBound { get; set; }
        public int Threshold { get; set; }
    }
}