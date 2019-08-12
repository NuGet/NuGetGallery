// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Produces embedded icon storage URLs from the <see cref="Package"/> objects.
    /// </summary>
    public interface IIconUrlTemplateProcessor : IStringTemplateProcessor<Package>
    {
    }
}