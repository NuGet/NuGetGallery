// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    // source: https://cwe.mitre.org/documents/schema/#StatusEnumeration
    // Note that the quality requirements for Draft and Usable status are very resource-intensive to accomplish, 
    // while some Incomplete and Draft entries are actively used by the general public; 
    // so, this status enumeration might change in the future.
    public enum CweStatus
    {
        /// <summary>
        /// A value of Stable indicates that all important elements have been verified, 
        /// and the entry is unlikely to change significantly in the future.
        /// </summary>
        Stable = 0,

        /// <summary>
        /// A value of Usable refers to an entity that has received close, extensive review, 
        /// with critical elements verified. 
        /// </summary>
        Usable = 1,

        /// <summary>
        /// A value of Draft refers to an entity that has all important elements filled, 
        /// and critical elements such as Name and Description are reasonably well-written; 
        /// the entity may still have important problems or gaps.
        /// </summary>
        Draft = 2,

        /// <summary>
        /// A value of Incomplete means that the entity does not have all important elements filled, 
        /// and there is no guarantee of quality.
        /// </summary>
        Incomplete = 3,

        /// <summary>
        /// A value of Obsolete is used when an entity is still valid but no longer is relevant, 
        /// likely because it has been superceded by a more recent entity.
        /// </summary>
        Obsolete = 4,

        /// <summary>
        /// A value of Deprecated refers to an entity that has been removed from CWE, 
        /// likely because it was a duplicate or was created in error.
        /// </summary>
        /// <remarks>
        /// CWEs with this status must be filtered out during initial import.
        /// </remarks>
        Deprecated = 5,

        /// <summary>
        /// A value of Unknown refers to an entity that was entered by a user of NuGet.org.
        /// If the CWE exists, it will be updated with the full data when it is imported.
        /// </summary>
        Unknown = 6
    }
}