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
        // TODO: Length of checklist will be saved in the configuration file.
        // https://github.com/NuGet/Engineering/issues/1645
        private const int TyposquattingCheckListLength = 20000;

        // TODO: Threshold parameters will be saved in the configuration file.
        // https://github.com/NuGet/Engineering/issues/1645
        private static readonly IReadOnlyList<ThresholdInfo> ThresholdsList = new List<ThresholdInfo>
        {
            new ThresholdInfo (0, 30, 0),
            new ThresholdInfo (30, 50, 1 ),
            new ThresholdInfo (50, 121, 2)
        };

        private readonly ITyposquattingUserService _userTyposquattingService;
        private readonly IEntityRepository<PackageRegistration> _packageRegistrationRepository;

        public TyposquattingCheckService(ITyposquattingUserService typosquattingUserService, IEntityRepository<PackageRegistration> packageRegistrationRepository)
        {
            _userTyposquattingService = typosquattingUserService ?? throw new ArgumentNullException(nameof(typosquattingUserService));
            _packageRegistrationRepository = packageRegistrationRepository ?? throw new ArgumentNullException(nameof(packageRegistrationRepository));
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

            var packagesCheckList = _packageRegistrationRepository.GetAll()
                .OrderByDescending(pr => pr.IsVerified)
                .ThenByDescending(pr => pr.DownloadCount)
                .Select(pr => pr.Id)
                .Take(TyposquattingCheckListLength)
                .ToList();

            var threshold = GetThreshold(uploadedPackageId);
            uploadedPackageId = TyposquattingStringNormalization.NormalizeString(uploadedPackageId);

            var collisionPackageIds = new ConcurrentBag<string>();
            Parallel.ForEach(packagesCheckList, (packageId, loopState) =>
            {
                // TODO: handle the package which is owned by an organization. 
                // https://github.com/NuGet/Engineering/issues/1656
                string normalizedPackageId = TyposquattingStringNormalization.NormalizeString(packageId);
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
        public ThresholdInfo(int lowerBound, int upperBound, int threshod)
        {
            LowerBound = lowerBound;
            UpperBound = upperBound;
            Threshold = Threshold;
        }        
    }
}