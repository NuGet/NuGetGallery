// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// A wrapper type around <see cref="Microsoft.WindowsAzure.Storage.AccessCondition"/>.
    /// </summary>
    public interface IAccessCondition
    {
        string IfNoneMatchETag { get; }
        string IfMatchETag { get; }
    }
}
