// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class TyposquattingCheckService : ITyposquattingCheckService
    {
        // TODO: Threshold parameters will be saved in the configuration file.
        // https://github.com/NuGet/Engineering/issues/1645
        private static List<ThresholdInfo> _thresholdsList = new List<ThresholdInfo>
        {
            new ThresholdInfo { LowerBound = 0, UpperBound = 30, Threshold = 0 },
            new ThresholdInfo { LowerBound = 30, UpperBound = 50, Threshold = 1 },
            new ThresholdInfo { LowerBound = 50, UpperBound = 120, Threshold = 2 }
        };
        
        // TODO: popular packages checklist will be implemented
        // https://github.com/NuGet/Engineering/issues/1624
        public static List<PackageInfo> PackagesCheckList { get; set; }

        private readonly ITyposquattingUserService _userTyposquattingService;

        public TyposquattingCheckService(ITyposquattingUserService typosquattingUserService)
        {
            _userTyposquattingService = typosquattingUserService ?? throw new ArgumentNullException(nameof(typosquattingUserService));
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

            var threshold = GetThreshold(uploadedPackageId);
            uploadedPackageId = TyposquattingStringNormalization.NormalizeString(uploadedPackageId);

            var countCollision = 0;
            Parallel.ForEach(PackagesCheckList, (package, loopState) =>
            {
                // TODO: handle the package which is owned by an organization. 
                // https://github.com/NuGet/Engineering/issues/1656
                if (package.Owners.Contains(uploadedPackageOwner.Username))
                {
                    return;
                }

                if (TyposquattingDistanceCalculation.IsDistanceLessThanThreshold(uploadedPackageId, package.Id, threshold))
                {
                    // Double check the owners list in the latest DB. 
                    if (_userTyposquattingService.CanUserTyposquat(package.Id, uploadedPackageOwner.Username))
                    {
                        return;
                    }

                    Interlocked.Increment(ref countCollision);
                    loopState.Stop();
                }
            });

            return countCollision != 0;
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

    public class PackageInfo
    {
        public string Id { get; set; }
        public HashSet<string> Owners { get; set; }
    }

    public class ThresholdInfo
    {
        public int LowerBound { get; set; }
        public int UpperBound { get; set; }
        public int Threshold { get; set; }
    }
}