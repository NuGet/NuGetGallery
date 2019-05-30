// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    /// <summary>
    /// This interface is used to implement a basic caching for typosquatting checklist.
    /// /// </summary>
    public interface ITyposquattingCheckListCacheService
    {
        /// <summary>
        /// The function is used to get the checklist from the cache for typosquatting
        /// </summary>
        /// <param name="checkListLength">The length of the checklist for typosquatting</param>
        /// <param name="checkListExpireTime">The expire time for checklist caching</param>
        /// <param name="packageService">The package service for checklist retrieval from database</param>
        IReadOnlyCollection<string> GetTyposquattingCheckList(int checkListLength, TimeSpan checkListExpireTime, IPackageService packageService);
    }
}
