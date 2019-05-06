// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity.Migrations;

namespace NuGet.Services.Validation
{
    public class ValidationMigrationsConfiguration: DbMigrationsConfiguration<ValidationEntitiesContext>
    {
        public ValidationMigrationsConfiguration()
        {
            AutomaticMigrationsEnabled = false;
        }

        protected override void Seed(ValidationEntitiesContext context)
        {
        }
    }
}
