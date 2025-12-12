// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Infrastructure.Annotations;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Entities;

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
		private const string ValidatorStatusesBatchIdIndex = "IX_ValidatorStatuses_BatchId";

		public CatalogValidationEntitiesContext(string nameOrConnectionString) : base(nameOrConnectionString)
		{
		}

		public CatalogValidationEntitiesContext(DbConnection connection) : base(connection)
		{
		}

		/// <summary>
		/// Configures the BatchId property for CatalogValidationDb.
		/// Overrides base implementation to configure the property instead of ignoring it.
		/// </summary>
		/// <param name="modelBuilder">The model builder.</param>
		protected override void ConfigureBatchIdProperty(DbModelBuilder modelBuilder)
		{
			// DO NOT call base - we're overriding the Ignore() behavior
			
			// CatalogValidationDb HAS the BatchId column, so configure it
			modelBuilder.Entity<ValidatorStatus>()
				.Property(s => s.BatchId)
				.HasColumnName("BatchId")
				.HasMaxLength(20)
				.IsOptional()
				.HasColumnAnnotation(
					IndexAnnotation.AnnotationName,
					new IndexAnnotation(new IndexAttribute(ValidatorStatusesBatchIdIndex)));
		}
	}
}
