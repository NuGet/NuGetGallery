// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.AzureSearch
{
    public enum HijackIndexChangeType
    {
        /// <summary>
        /// Update the metadata of the document. This is not a superset of <see cref="SetLatestToFalse"/>
        /// or <see cref="SetLatestToTrue"/>. Suppose versions 1.0.0 and 2.0.0 are both listed. Then a catalog leaf
        /// comes in unlisting version 2.0.0 (making it no longer the latest). In this case version 1.0.0 will be
        /// changed using <see cref="SetLatestToTrue"/> but will not have a <see cref="UpdateMetadata"/> change.
        /// 2.0.0 will have both <see cref="SetLatestToFalse"/> and <see cref="UpdateMetadata"/> changes.
        /// </summary>
        UpdateMetadata,

        /// <summary>
        /// Delete the document. This change type is mutually exclusive with all other change types.
        /// </summary>
        Delete,

        /// <summary>
        /// Set the latest property to true. This change type is mutually exclusive with
        /// <see cref="SetLatestToFalse"/>.
        /// </summary>
        SetLatestToTrue,

        /// <summary>
        /// Set the latest property to false. this change type is mutually exclusive with
        /// <see cref="SetLatestToTrue"/>.
        /// </summary>
        SetLatestToFalse,
    }
}
