// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Gallery;
using NuGet.Services.Gallery.Entities;

namespace NuGetGallery
{
    public abstract class AppCommand
    {
        protected AppCommand(IEntitiesContext entities)
        {
            Entities = entities;
        }

        protected IEntitiesContext Entities { get; set; }
    }
}