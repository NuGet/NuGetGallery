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

        private readonly ITyposquattingUserService _userTyposquattingService;
        private readonly IEntityRepository<PackageRegistration> _packageRegistrationRepository;
        private readonly IContentObjectService _contentObjectService;

        public TyposquattingCheckService(ITyposquattingUserService typosquattingUserService, IEntityRepository<PackageRegistration> packageRegistrationRepository, IContentObjectService contentObjectService)
        {
            _userTyposquattingService = typosquattingUserService ?? throw new ArgumentNullException(nameof(typosquattingUserService));
            _packageRegistrationRepository = packageRegistrationRepository ?? throw new ArgumentNullException(nameof(packageRegistrationRepository));
            _contentObjectService = contentObjectService ?? throw new ArgumentNullException(nameof(contentObjectService));

            TyposquattingCheckListLength = _contentObjectService.TyposquattingConfiguration.PackageIdChecklistLength;
        }
              
        public bool IsUploadedPackageIdTyposquatting(string uploadedPackageId, User uploadedPackageOwner, out string typosquattingCheckCollisionIds)
        {
            typosquattingCheckCollisionIds = null;
            if (!_contentObjectService.TyposquattingConfiguration.IsCheckEnabled)
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

            var packagesCheckList = _packageRegistrationRepository.GetAll()
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
                // TODO: handle the package which is owned by an organization. 
                // https://github.com/NuGet/Engineering/issues/1656
                string normalizedPackageId = TyposquattingStringNormalization.NormalizeString(packageId);
                if (TyposquattingDistanceCalculation.IsDistanceLessThanThreshold(uploadedPackageId, normalizedPackageId, threshold))
                {
                    collisionIds.Add(packageId);
                }
            });

            List<string> typosquattingUserDoubleCheckIds = new List<string>();
            foreach (var packageId in collisionIds)
            {
                // TODO: refactor user check services into one query
                // https://github.com/NuGet/Engineering/issues/1684
                if (!_userTyposquattingService.CanUserTyposquat(packageId, uploadedPackageOwner.Username))
                {
                    typosquattingUserDoubleCheckIds.Add(packageId);
                }
            }

            if (typosquattingUserDoubleCheckIds.Any())
            {
                // TODO: save in the log metric for typosquatting collision Ids (typosquattingCheckCollisionIds). 
                // https://github.com/NuGet/Engineering/issues/1537
                typosquattingCheckCollisionIds = string.Join(",", typosquattingUserDoubleCheckIds.ToArray());
                if (_contentObjectService.TyposquattingConfiguration.IsBlockUsersEnabled)
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
        public ThresholdInfo(int lowerBound, int upperBound, int threshold)
        {
            LowerBound = lowerBound;
            UpperBound = upperBound;
            Threshold = threshold;
        }        
    }
}