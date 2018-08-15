// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    /// <summary>
    /// The interface and method are used to retrieve the checklist from the database for typo-squatting.
    /// </summary>
    public interface ITyposquattingPackagesCheckListService
    {
        /// <summary>
        /// This method is used to get the checklist from the database for typo-squatting.
        /// </summary>
        /// <param name="typosquattingCheckListLength">checklist length for typo-squatting</param>
        List<string> GetTyposquattingChecklist(int typosquattingCheckListLength);
    }
}
