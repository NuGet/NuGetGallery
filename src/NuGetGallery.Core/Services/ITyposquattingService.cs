// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery
{
    /// <summary>
    /// This interface is used to check typo-squatting of uploaded package ID with the owner.  
    /// </summary>
    public interface ITyposquattingService
    {
        /// <summary>
        /// The function is used to check whether the uploaded package is a typo-squatting package.
        /// </summary>
        /// <param name="typosquattingCheckInfo"> The package ID with the owner and typo-squatting collision check parameters</param>
        Task<TyposquattingCheckResult> IsUploadedPackageIdTyposquattingAsync(TyposquattingCheckInfo typosquattingCheckInfo);
    }
}
