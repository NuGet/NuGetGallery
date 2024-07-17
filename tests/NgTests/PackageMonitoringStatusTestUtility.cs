// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Versioning;
using NuGetGallery;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NgTests
{
    public static class PackageMonitoringStatusTestUtility
    {
        public static ValidationResult CreateValidationResult(TestResult result, Exception e)
        {
            return new DummyValidator(result, e).Validate();
        }

        public static CatalogIndexEntry CreateCatalogIndexEntry(string id, string version, DateTime commitTimestamp)
        {
            return new CatalogIndexEntry(
                new UriBuilder() { Path = $"{id.ToLowerInvariant()}/{id.ToLowerInvariant()}.{version.ToLowerInvariant()}" }.Uri,
                CatalogConstants.NuGetPackageDetails,
                Guid.NewGuid().ToString(),
                commitTimestamp,
                new PackageIdentity(id, new NuGetVersion(version)));
        }

        public static DeletionAuditEntry CreateDeletionAuditEntry(string id, string version, DateTime commitTimestamp)
        {
            return new DeletionAuditEntry(
                new UriBuilder() { Path = $"auditing/{id}/{version}/{Guid.NewGuid().ToString()}{DeletionAuditEntry.FileNameSuffixes[0]}" }.Uri,
                JObject.Parse("{\"help\":\"i'm trapped in a json factory!\"}"),
                id,
                version,
                commitTimestamp);
        }

        public static PackageMonitoringStatus CreateStatusWithPackageValidationResult(
            string packageId,
            string packageVersion,
            TestResult result,
            DateTime? commitTimestamp = null)
        {
            return CreateStatusWithPackageValidationResult(
                packageId,
                packageVersion,
                new[] { CreateValidationResult(result, null) },
                commitTimestamp);
        }

        public static PackageMonitoringStatus CreateStatusWithPackageValidationResult(
            string packageId,
            string packageVersion,
            IEnumerable<ValidationResult> results,
            DateTime? commitTimestamp = null)
        {
            commitTimestamp = commitTimestamp ?? new DateTime(2019, 6, 10);
            var version = new NuGetVersion(packageVersion);

            var aggregateValidationResult = new DummyAggregateValidator(results).Validate();

            var packageValidationResult = new PackageValidationResult(
                new PackageIdentity(packageId, version),
                new CatalogIndexEntry[] {
                        CreateCatalogIndexEntry(packageId, packageVersion, commitTimestamp.Value),
                        CreateCatalogIndexEntry(packageId, packageVersion, commitTimestamp.Value),
                        CreateCatalogIndexEntry(packageId, packageVersion, commitTimestamp.Value)
                    },
                new DeletionAuditEntry[] {
                        CreateDeletionAuditEntry(packageId, packageVersion, commitTimestamp.Value),
                        CreateDeletionAuditEntry(packageId, packageVersion, commitTimestamp.Value),
                        CreateDeletionAuditEntry(packageId, packageVersion, commitTimestamp.Value)
                    },
                new AggregateValidationResult[] { aggregateValidationResult });

            return new PackageMonitoringStatus(packageValidationResult);
        }

        public static PackageMonitoringStatus CreateStatusWithException(string packageId, string packageVersion)
        {
            return new PackageMonitoringStatus(new FeedPackageIdentity(packageId, packageVersion), new Exception());
        }

        public static TestResult GetTestResultFromPackageState(PackageState state)
        {
            switch (state)
            {
                case PackageState.Invalid:
                    return TestResult.Fail;
                case PackageState.Valid:
                    return TestResult.Pass;
                case PackageState.Unknown:
                    return TestResult.Pending;
                default:
                    throw new ArgumentException(nameof(state));
            }
        }

        public static void AssertFieldEqual<TParent, TField>(
            TParent expected,
            TParent actual,
            Func<TParent, TField> accessor)
        {
            Assert.Equal(accessor(expected), accessor(actual));
        }

        public static void AssertFieldEqual<TParent, TField>(
            TParent expected,
            TParent actual,
            Func<TParent, TField> accessor,
            Action<TField, TField> assert)
        {
            assert(accessor(expected), accessor(actual));
        }

        public static void AssertFieldEqual<TParent, TField>(
            TParent expected,
            TParent actual,
            Func<TParent, IEnumerable<TField>> accessor,
            Action<TField, TField> assert)
        {
            AssertAll(accessor(expected), accessor(actual), assert);
        }

        public static void AssertStatus(PackageMonitoringStatus expected, PackageMonitoringStatus actual)
        {
            AssertFieldEqual(expected, actual, i => i.Package.Id);
            AssertFieldEqual(expected, actual, i => i.Package.Version);
            AssertFieldEqual(expected, actual, i => i.State);

            AssertFieldEqual(expected, actual, i => i.ValidationResult, AssertPackageValidationResult);
            AssertFieldEqual(expected, actual, i => i.ValidationException, AssertException);
        }

        public static void AssertException(Exception expected, Exception actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);

                return;
            }

            AssertFieldEqual(expected, actual, i => i.Message);
            AssertFieldEqual(expected, actual, i => i.StackTrace);
            AssertFieldEqual(expected, actual, i => i.Data, AssertDictionary);
            AssertFieldEqual(expected, actual, i => i.InnerException, AssertException);
        }

        public static void AssertDictionary(IDictionary expected, IDictionary actual)
        {
            foreach (var expectedKey in expected.Keys)
            {
                Assert.True(actual.Contains(expectedKey));
                Assert.Equal(expected[expectedKey], actual[expectedKey]);
            }
        }

        public static void AssertPackageValidationResult(PackageValidationResult expected, PackageValidationResult actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);

                return;
            }

            AssertFieldEqual(expected, actual, i => i.Package.Id);
            AssertFieldEqual(expected, actual, i => i.Package.Version);

            AssertFieldEqual(expected, actual, i => i.CatalogEntries, AssertCatalogIndexEntry);
            AssertFieldEqual(expected, actual, i => i.DeletionAuditEntries, AssertDeletionAuditEntry);

            AssertFieldEqual(expected, actual, i => i.AggregateValidationResults, AssertAggregateValidationResult);
        }

        public static void AssertCatalogIndexEntry(CatalogIndexEntry expected, CatalogIndexEntry actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);

                return;
            }

            AssertFieldEqual(expected, actual, i => i.Uri);
            AssertFieldEqual(expected, actual, i => i.Types);
            AssertFieldEqual(expected, actual, i => i.Id);
            AssertFieldEqual(expected, actual, i => i.Version);
            AssertFieldEqual(expected, actual, i => i.CommitId);
            AssertFieldEqual(expected, actual, i => i.CommitTimeStamp);
        }

        public static void AssertDeletionAuditEntry(DeletionAuditEntry expected, DeletionAuditEntry actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);

                return;
            }

            AssertFieldEqual(expected, actual, i => i.PackageId);
            AssertFieldEqual(expected, actual, i => i.PackageVersion);
            AssertFieldEqual(expected, actual, i => i.Record);
            AssertFieldEqual(expected, actual, i => i.TimestampUtc);
            AssertFieldEqual(expected, actual, i => i.Uri);
        }

        public static void AssertAggregateValidationResult(AggregateValidationResult expected, AggregateValidationResult actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);

                return;
            }

            AssertFieldEqual(expected, actual, i => i.AggregateValidator.Name);
            AssertFieldEqual(expected, actual, i => i.ValidationResults, AssertValidationResult);
        }

        public static void AssertValidationResult(ValidationResult expected, ValidationResult actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);

                return;
            }

            AssertFieldEqual(expected, actual, i => i.Validator.Name);
            AssertFieldEqual(expected, actual, i => i.Result);

            AssertFieldEqual(expected, actual, i => i.Exception, AssertException);
        }

        public static void AssertAccessCondition(IAccessCondition expected, IAccessCondition actual)
        {
            AssertFieldEqual(expected, actual, i => i.IfNoneMatchETag);
            AssertFieldEqual(expected, actual, i => i.IfMatchETag);
        }

        public static void AssertAll<T>(IEnumerable<T> expecteds, IEnumerable<T> actuals, Action<T, T> assert)
        {
            if (expecteds == null)
            {
                Assert.Null(actuals);

                return;
            }

            Assert.Equal(expecteds.Count(), actuals.Count());
            var expectedsArray = expecteds.ToArray();
            var actualsArray = actuals.ToArray();
            for (int i = 0; i < expecteds.Count(); i++)
            {
                assert(expectedsArray[i], actualsArray[i]);
            }
        }
    }
}
