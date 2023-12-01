// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// This class is for passing the package ID with the owner and typo-squatting collision check parameters to ITyposquattingService.  
    /// </summary>
    public class TyposquattingCheckInfo
    {
        public string UploadedPackageId { get; } // The package ID of the uploaded package. We check the package ID with the packages in the gallery for typo-squatting issue
        public User UploadedPackageOwner { get; } //The package owner of the uploaded package.
        public IQueryable<PackageRegistration> AllPackageRegistrations { get; }
        public int CheckListConfiguredLength { get; }
        public TimeSpan CheckListExpireTimeInHours { get; }
        public bool IsTyposquattingEnabledForOwner { get; }
        public TyposquattingCheckInfo(
            string uploadedPackageId, 
            User uploadedPackageOwner, 
            IQueryable<PackageRegistration> allPackageRegistrations, 
            int checkListConfiguredLength, 
            TimeSpan checkListExpireTimeInHours, 
            bool isTyposquattingEnabledForOwner)
        {
            UploadedPackageId = uploadedPackageId;
            UploadedPackageOwner = uploadedPackageOwner;
            AllPackageRegistrations = allPackageRegistrations;
            CheckListConfiguredLength = checkListConfiguredLength;
            CheckListExpireTimeInHours = checkListExpireTimeInHours;
            IsTyposquattingEnabledForOwner = isTyposquattingEnabledForOwner;
        }
    }
}
