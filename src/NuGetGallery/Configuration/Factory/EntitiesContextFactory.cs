// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Configuration.Factory
{
    public class EntitiesContextFactory : ConfigObjectFactory<EntitiesContext>
    {
        public EntitiesContextFactory()
            : base(new ConfigObjectDelegate<EntitiesContext>(
                objects => new EntitiesContext((string)objects[0], (bool)objects[1]), new string[] { "SqlConnectionString", "ReadOnlyMode" }))
        {
        }
    }
}