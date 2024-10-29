// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public static class EntitiesContextExtensions
    {
        public static async Task<bool> TransformUserToOrganization(this IEntitiesContext context, User accountToTransform, User adminUser, string token)
        {
            accountToTransform = accountToTransform ?? throw new ArgumentNullException(nameof(accountToTransform));
            adminUser = adminUser ?? throw new ArgumentNullException(nameof(adminUser));

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException(nameof(token));
            }

            var database = context.GetDatabase();
            var recordCount = await database.ExecuteSqlResourceAsync(
                MigrateUserToOrganization.ResourceName,
                new SqlParameter(MigrateUserToOrganization.OrganizationKey, accountToTransform.Key),
                new SqlParameter(MigrateUserToOrganization.AdminKey, adminUser.Key),
                new SqlParameter(MigrateUserToOrganization.ConfirmationToken, token));

            return recordCount > 0;
        }

        private static class MigrateUserToOrganization
        {
            public const string ResourceName = "NuGetGallery.Infrastructure.MigrateUserToOrganization.sql";
            public const string OrganizationKey = "organizationKey";
            public const string AdminKey = "adminKey";
            public const string ConfirmationToken = "token";
        }
    }
}