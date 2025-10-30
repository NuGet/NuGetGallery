// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Migrations;
using NuGet.Services.CatalogValidation.Entities;

namespace NuGet.Services.CatalogValidation
{
    public class CatalogValidationMigrationsConfiguration: DbMigrationsConfiguration<CatalogValidationEntitiesContext>
    {
        public CatalogValidationMigrationsConfiguration()
        {
            AutomaticMigrationsEnabled = false;
            CommandTimeout = (int)TimeSpan.FromMinutes(30).TotalSeconds;
        }

        protected override void Seed(CatalogValidationEntitiesContext context)
        {
        }
    }
}
