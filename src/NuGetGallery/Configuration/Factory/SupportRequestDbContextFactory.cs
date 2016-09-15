// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery.Configuration.Factory
{
    public class SupportRequestDbContextFactory : ConfigObjectFactory<SupportRequestDbContext>
    {
        public SupportRequestDbContextFactory()
            : base(new ConfigObjectDelegate<SupportRequestDbContext>(
                objects => new SupportRequestDbContext((string)objects[0]), "SqlConnectionStringSupportRequest"))
        {
        }
    }
}