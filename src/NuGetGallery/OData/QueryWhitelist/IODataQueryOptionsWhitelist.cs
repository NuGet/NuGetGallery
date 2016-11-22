// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.OData.QueryWhitelist
{
    /// <summary>
    /// IODataQueryOptionsWhitelist interface
    /// </summary>
    internal interface IODataQueryOptionsWhitelist
    {
        /// <summary>
        /// Returns true if the queryFormat is whitelisted
        /// </summary>
        /// <param name="queryFormat">The string representing the the query to be validated.</param>
        /// <returns></returns>
        bool IsWhitelisted(string queryFormat);
    }
}
