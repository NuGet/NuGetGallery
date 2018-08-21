// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// This interface and method are used to check the info like owners' list in the latest DB for typo-squatting. 
    /// </summary>
    public interface ITyposquattingUserService
    {
        bool CanUserTyposquat(string packageId, string userName);
    }
}
