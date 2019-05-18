// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGetGallery.DatabaseMigrationTools;
using Xunit;

namespace NuGet.Services.DatabaseMigration.Facts
{
    public class ValidateMigrationsFacts
    {
        private Job _migrationJob;

        public ValidateMigrationsFacts()
        {
            var migrationContextFactory = new MigrationContextFactory();
            _migrationJob = new Job(migrationContextFactory);
        }

        [Theory]
        [MemberData(nameof(NullMigrations))]
        public void ValidateMigrationsThrowNullExceptions(List<string> databaseMigrations, List<string> localMigrations)
        {
            var exception = Assert.Throws<ArgumentNullException>(() => _migrationJob.CheckIsValidMigration(databaseMigrations, localMigrations));
        }

        public static IEnumerable<object[]> NullMigrations
        {
            get
            {
                yield return new object[] { null, null };
                yield return new object[] { null, new List<string>() { "2011_Migration_1" } };
                yield return new object[] { new List<string>() { "2011_Migration_1" }, null };
            }
        }

        [Theory]
        [MemberData(nameof(InvalidMigrations))]
        public void ValidateMigrationsThrowInvalidOperationExceptions(List<string> databaseMigrations,List<string> localMigrations, string expectedExceptionMessage)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => _migrationJob.CheckIsValidMigration(databaseMigrations, localMigrations));
            Assert.Equal(expectedExceptionMessage, exception.Message);
        }

        public static IEnumerable<object[]> InvalidMigrations
        {
            get
            {
                yield return new object[] { new List<string>(),
                    new List<string> { "2011_Migration_1", "2012_Migration_2" },
                    "Migration validation failed: Unexpected empty history of database migrations."};
                yield return new object[] { new List<string>() { "2011_Migration_1", "2012_Migration_2"},
                    new List<string>(),
                    "Migration validation failed: Unexpected empty history of local migrations."};
                yield return new object[] { new List<string>() { "2011_Migration_1", "2012_Migration_3" },
                    new List<string>() { "2011_Migration_2", "2012_Migration_3"},
                    "Migration validation failed: Mismatch local migration file: 2011_Migration_2." };
                yield return new object[] { new List<string>() { "2011_Migration_1", "2012_Migration_2" },
                    new List<string>() { "2011_Migration_1", "2012_Migration_3"},
                    "Migration validation failed: Mismatch local migration file: 2012_Migration_3." };
                yield return new object[] { new List<string>() { "2011_Migration_1", "2012_Migration_2", "2012_Migration_4" },
                    new List<string>() { "2011_Migration_1", "2012_Migration_3", "2012_Migration_4" },
                    "Migration validation failed: Mismatch local migration file: 2012_Migration_3." };
            }
        }

        [Theory]
        [MemberData(nameof(ValidMigrations))]
        public void ValidateMigrationsDoesNotThrowExceptions(List<string> databaseMigrations, List<string> localMigrations)
        {
            try
            {
                _migrationJob.CheckIsValidMigration(databaseMigrations, localMigrations);
            }
            catch (Exception ex)
            {
                Assert.True(false);
            }

            Assert.True(true);
        }

        public static IEnumerable<object[]> ValidMigrations
        {
            get
            {
                yield return new object[] { new List<string>() { "2011_Migration_1", "2012_Migration_2" },
                    new List<string> { "2011_Migration_1", "2012_Migration_2" } };
                yield return new object[] { new List<string>() { "2011_Migration_1", "2012_Migration_2" },
                    new List<string> { "2011_Migration_1", "2012_Migration_2", "2012_Migration_3" } };
            }
        }
    }
}
