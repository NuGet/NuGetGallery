// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.ModelConfiguration;
using NuGet.Services.Validation;

namespace NuGet.Services.CatalogValidation.Entities
{
    /// <summary>
    /// DbContext factory for CatalogValidation migrations
    /// </summary>
    public class CatalogValidationDbContextFactory : IDbContextFactory<CatalogValidationEntitiesContext>
    {
        public static Func<CatalogValidationEntitiesContext> CatalogValidationEntitiesContextFactory;
        
        public CatalogValidationEntitiesContext Create()
        {
            var factory = CatalogValidationEntitiesContextFactory;
            return factory == null ? new CatalogValidationEntitiesContext("CatalogValidation.SqlServer") : factory();
        }
    }

	/// <summary>
	/// Entity Framework context for CatalogValidation database
	/// </summary>
	public class CatalogValidationEntitiesContext : ValidationEntitiesContext
	{
		public CatalogValidationEntitiesContext(string nameOrConnectionString) : base(nameOrConnectionString)
		{
		}

		public CatalogValidationEntitiesContext(DbConnection connection) : base(connection)
		{
		}
	}
}
