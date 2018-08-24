// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// The interface and method are used to check the latest info like owners' list from the DB for typo-squatting. 
    /// </summary>
    public interface ITyposquattingUserService
    {
        /// <summary>
        /// The function is used to check the latest info of owners from the DB to confirm that the uploaded package and the conflict package are not shared by the same user. 
        /// </summary>
        /// <param name="packageId"> The package ID of the potential conflict package in the gallery. 
        ///                          We'd like to double check that the conflict package and uploaded package don't share the same user</param>
        /// <param name="userName"> The package owner of the uploaded package.</param>
        bool CanUserTyposquat(string packageId, string userName);
    }
}
