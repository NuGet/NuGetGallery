// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGetGallery.Services
{
    public class NamespaceReservationEvidenceFacts
    {
        private static readonly DateTime AssessmentTime = new DateTime(2024, 2, 29, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void FactoryRequiresScopedContext()
        {
            Assert.Throws<ArgumentNullException>(() => new NamespaceReservationEvidenceFactory(null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("Contoso.")]
        [InlineData(".Contoso")]
        [InlineData("Contoso..Tools")]
        [InlineData("Contoso.-Tools")]
        [InlineData("Contoso%")]
        [InlineData("Contoso[Tools]")]
        [InlineData("Contosó")]
        [InlineData("Contoso\n")]
        [InlineData("Contoso'; SELECT 1--")]
        public void RejectsInvalidNamespaceBeforeObtainingConnection(string value)
        {
            var connection = new FakeConnection();
            Assert.Throws<ArgumentException>(() => Session(connection, namespaceValue: value));
            Assert.Empty(connection.Commands);
            Assert.Equal(0, connection.OpenCount);
        }

        [Theory]
        [InlineData("Contoso")]
        [InlineData("cOnToSo.Tools")]
        [InlineData("Contoso_Tools")]
        [InlineData("Contoso-Tools")]
        [InlineData("_")]
        [InlineData("1._Tools-2")]
        public void AcceptsSharedValidatorNamespaceSyntax(string value)
        {
            Assert.True(PackageIdValidator.IsValidPackageId(value));
            var connection = new FakeConnection();
            Assert.NotNull(Session(connection, namespaceValue: value));
            Assert.Empty(connection.Commands);
        }

        [Fact]
        public void BoundsAndCopiesTrustedScope()
        {
            var connection = new FakeConnection();
            Assert.Throws<ArgumentOutOfRangeException>(() => Session(connection, submitterKey: 0));
            Assert.Throws<ArgumentNullException>(() => new NamespaceReservationSqlEvidenceSession(
                () => connection, () => null, new SemaphoreSlim(1, 1), 1, null, "Contoso", AssessmentTime));
            Assert.Throws<ArgumentException>(() => Session(connection, owners: new int[0]));
            Assert.Throws<ArgumentException>(() => Session(connection, owners: new[] { 0 }));
            Assert.Throws<ArgumentException>(() => Session(connection, owners: new[] { -1 }));
            Assert.Throws<ArgumentException>(() => Session(connection, owners: new[] { 1, 1 }));
            Assert.Throws<ArgumentException>(() => Session(connection, owners: Enumerable.Range(1, 20).ToArray()));
            Assert.Throws<ArgumentException>(() => Session(connection, namespaceValue: new string('a', 128)));
            Assert.NotNull(Session(connection, namespaceValue: new string('a', 127)));
            Assert.NotNull(Session(connection, owners: Enumerable.Range(1, 19).ToArray()));
            Assert.Empty(connection.Commands);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("person@microsoft.com", true)]
        [InlineData("Person@MICROSOFT.COM", true)]
        [InlineData("person+team@microsoft.com", true)]
        [InlineData("person@sub.microsoft.com", false)]
        [InlineData("person@notmicrosoft.com", false)]
        [InlineData("person@microsoft.com.example.org", false)]
        [InlineData("person@microsoft.com.", false)]
        [InlineData("person@mіcrosoft.com", false)]
        [InlineData("person@microsoft.com\n", false)]
        [InlineData(" person@microsoft.com", false)]
        [InlineData("person@microsoft.com ", false)]
        [InlineData("Name <person@microsoft.com>", false)]
        [InlineData("person@microsoft.com (Name)", false)]
        [InlineData("a@microsoft.com,b@microsoft.com", false)]
        [InlineData("a@microsoft.com;b@microsoft.com", false)]
        [InlineData("microsoft.com", false)]
        public void ParsesOnlyAnExactMicrosoftEmailDomain(string value, bool expected)
        {
            Assert.Equal(expected, NamespaceReservationSqlEvidenceSession.HasExactMicrosoftEmailDomain(value));
        }

        [Theory]
        [InlineData("Contoso", "Contoso")]
        [InlineData("Contoso_Tools", "Contoso~_Tools")]
        [InlineData("a%b[c]~d_", "a~%b~[c]~~d~_")]
        [InlineData("~~%_[", "~~~~~%~_~[")]
        public void EscapesSqlServerLikeMetacharacters(string input, string expected)
        {
            Assert.Equal(expected, NamespaceReservationSqlEvidenceSession.EscapeLike(input));
        }

        [Theory]
        [InlineData("unknown", "{}")]
        [InlineData("GET_REQUEST_ACCOUNT_FACTS", "{}")]
        [InlineData("get_request_account_facts", null)]
        [InlineData("get_request_account_facts", "{'submitterKey': 1}")]
        [InlineData("get_namespace_reservations", "{'namespace': 'Other'}")]
        [InlineData("get_namespace_package_usage", "{'ownerKeys': [1]}")]
        [InlineData("get_namespace_package_usage", "{'sql': 'SELECT 1'}")]
        [InlineData("get_package_details", "{}")]
        [InlineData("get_package_details", "{'packageIds': []}")]
        [InlineData("get_package_details", "{'PackageIds': ['Contoso']}")]
        [InlineData("get_package_details", "{'packageIds': 'Contoso'}")]
        [InlineData("get_package_details", "{'packageIds': null}")]
        [InlineData("get_package_details", "{'packageIds': [1]}")]
        [InlineData("get_package_details", "{'packageIds': [null]}")]
        [InlineData("get_package_details", "{'packageIds': [{}]}")]
        [InlineData("get_package_details", "{'packageIds': ['Contoso'], 'namespace': 'Other'}")]
        [InlineData("get_package_details", "{'packageIds': ['Contoso','contoso']}")]
        [InlineData("get_package_details", "{'packageIds': [' Contoso']}")]
        public async Task RejectsUnknownToolsAndNonExactArgumentsWithoutSql(string tool, string json)
        {
            var connection = new FakeConnection();
            var result = await Session(connection).ExecuteAsync(tool, json == null ? null : JObject.Parse(json), CancellationToken.None);

            AssertFailure(result, "invalid_tool_arguments");
            Assert.Empty(connection.Commands);
            Assert.Equal(0, connection.OpenCount);
        }

        [Fact]
        public async Task BoundsDetailArgumentsBeforeSql()
        {
            var connection = new FakeConnection();
            var session = Session(connection);
            var tooMany = new JObject { ["packageIds"] = new JArray(Enumerable.Range(0, 11).Select(i => "Contoso." + i)) };
            AssertFailure(await session.ExecuteAsync("get_package_details", tooMany, CancellationToken.None), "invalid_tool_arguments");
            AssertFailure(await session.ExecuteAsync("get_package_details", DetailArgs(new string('a', 129)), CancellationToken.None), "invalid_tool_arguments");
            AssertFailure(await session.ExecuteAsync("get_package_details", DetailArgs("Contoso\n"), CancellationToken.None), "invalid_tool_arguments");
            AssertFailure(await session.ExecuteAsync("get_package_details", DetailArgs("Contoso"), CancellationToken.None), "package_id_not_in_session_evidence");
            Assert.Empty(connection.Commands);
        }

        [Fact]
        public async Task CopiesIdsBindsValuesAndFixesUtcCutoffWithoutLeakingPrivateData()
        {
            var connection = new FakeConnection();
            connection.Results.Enqueue(Accounts());
            var owners = new[] { 812345 };
            var session = Session(connection, owners, "Contoso_Tools", 912345);
            owners[0] = 777777;
            var result = await session.ExecuteAsync("get_request_account_facts", new JObject(), CancellationToken.None);

            var command = Assert.Single(connection.Commands);
            Assert.Equal(812345, command.Parameters["@owner0"].Value);
            Assert.Equal(912345, command.Parameters["@submitter"].Value);
            Assert.Equal(DBNull.Value, command.Parameters["@owner1"].Value);
            Assert.Equal("Contoso_Tools", command.Parameters["@namespace"].Value);
            Assert.Equal("Contoso~_Tools%", command.Parameters["@rawPrefix"].Value);
            Assert.Equal("Contoso~_Tools.%", command.Parameters["@dottedPrefix"].Value);
            Assert.Equal(AssessmentTime, command.Parameters["@assessment"].Value);
            Assert.Equal(new DateTime(2023, 2, 28, 12, 0, 0, DateTimeKind.Utc), command.Parameters["@cutoff"].Value);
            Assert.Equal(DbType.Int32, command.Parameters["@owner0"].DbType);
            Assert.Equal(DbType.String, command.Parameters["@namespace"].DbType);
            Assert.Equal(128, command.Parameters["@namespace"].Size);
            Assert.Equal(DbType.DateTime2, command.Parameters["@cutoff"].DbType);
            Assert.DoesNotContain("Contoso_Tools", command.CommandText);
            Assert.DoesNotContain("812345", command.CommandText);
            Assert.True((bool)result["submitter"]["hasConfirmedMicrosoftEmail"]);
            Assert.DoesNotContain("private@microsoft.com", result.ToString());
            Assert.DoesNotContain("812345", result.ToString());
            Assert.DoesNotContain("912345", result.ToString());
            Assert.DoesNotContain("EmailAddress", result.ToString());
            Assert.Equal("2024-02-29T12:00:00.0000000Z", (string)result["assessmentTimeUtc"]);
            Assert.Equal("2023-02-28T12:00:00.0000000Z", (string)result["cutoffUtc"]);
        }

        [Fact]
        public async Task ReportsAccountAuthorityWithoutClaimingOnboarding()
        {
            var connection = new FakeConnection();
            connection.Results.Enqueue(Accounts());
            var result = await Session(connection).ExecuteAsync("get_request_account_facts", new JObject(), CancellationToken.None);

            Assert.True((bool)result["complete"]);
            Assert.True((bool)result["eligible"]);
            Assert.Equal("user", (string)result["submitter"]["accountType"]);
            var owner = Assert.Single((JArray)result["owners"]);
            Assert.Equal("organization", (string)owner["accountType"]);
            Assert.True((bool)owner["adminMembership"]);
            Assert.True((bool)owner["microsoftPolicySubscriptionObserved"]);
            Assert.Equal("unknown", (string)owner["securityOnboardingStatus"]);
            Assert.Contains("security_onboarding_not_established_by_subscription", result["blockers"].Values<string>());
            Assert.Null(result["canAutomaticallyApprove"]);
        }

        [Theory]
        [InlineData("IsDeleted", true)]
        [InlineData("UserStatusKey", 2)]
        [InlineData("UserStatusKey", 1)]
        [InlineData("AdminMembership", false)]
        [InlineData("EmailAddress", "")]
        [InlineData("AccountExists", false)]
        public async Task CompleteAccountReadDoesNotImplyEligibilityForEveryRequestedOwner(string field, object value)
        {
            var connection = new FakeConnection();
            var table = Accounts();
            var anotherOwner = table.NewRow();
            anotherOwner.ItemArray = (object[])table.Rows[1].ItemArray.Clone();
            anotherOwner["Ordinal"] = 2L;
            anotherOwner["Username"] = "SecondOwner";
            anotherOwner[field] = value;
            table.Rows.Add(anotherOwner);
            connection.Results.Enqueue(table);

            var result = await Session(connection, new[] { 2, 3 }).ExecuteAsync("get_request_account_facts", new JObject(), CancellationToken.None);

            Assert.False((bool)result["eligible"]);
            Assert.True((bool)result["complete"]);
            Assert.Equal(2, ((JArray)result["owners"]).Count);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public async Task PersonalOwnerMustBeSubmitter(bool isSubmitter, bool eligible)
        {
            var connection = new FakeConnection();
            var accounts = Accounts();
            accounts.Rows[1]["IsOrganization"] = false;
            accounts.Rows[1]["IsSubmitter"] = isSubmitter;
            accounts.Rows[1]["AdminMembership"] = false;
            connection.Results.Enqueue(accounts);

            var result = await Session(connection).ExecuteAsync("get_request_account_facts", new JObject(), CancellationToken.None);
            Assert.Equal(eligible, (bool)result["eligible"]);
        }

        [Fact]
        public async Task MissingAccountsExposeUnknownStateNotEligible()
        {
            var connection = new FakeConnection();
            var accounts = Accounts();
            accounts.Rows[1]["AccountExists"] = false;
            accounts.Rows[1]["Username"] = DBNull.Value;
            accounts.Rows[1]["EmailAddress"] = DBNull.Value;
            accounts.Rows[1]["IsDeleted"] = DBNull.Value;
            accounts.Rows[1]["UserStatusKey"] = DBNull.Value;
            connection.Results.Enqueue(accounts);

            var result = await Session(connection).ExecuteAsync("get_request_account_facts", new JObject(), CancellationToken.None);

            Assert.True((bool)result["complete"]);
            Assert.False((bool)result["eligible"]);
            Assert.Equal(JTokenType.Null, result["owners"][0]["locked"].Type);
            Assert.Equal(JTokenType.Null, result["owners"][0]["confirmed"].Type);
        }

        [Theory]
        [InlineData(0, 0, true, false)]
        [InlineData(1, 1, true, true)]
        [InlineData(20, 20, true, true)]
        [InlineData(100, 20, false, true)]
        public async Task ReservationAggregatesAreNotSampleCountsAndAnyOverlapNeedsReview(int count, int samples, bool sampleComplete, bool requiresReview)
        {
            var connection = new FakeConnection();
            connection.Results.Enqueue(Reservations(count, samples));
            var result = await Session(connection).ExecuteAsync("get_namespace_reservations", new JObject(), CancellationToken.None);

            Assert.True((bool)result["complete"]);
            Assert.Equal(sampleComplete, (bool)result["sampleComplete"]);
            Assert.True((bool)result["aggregatesComplete"]);
            Assert.Equal(requiresReview, (bool)result["requiresReview"]);
            Assert.Equal(count, (long)result["counts"]["overlapCount"]);
            Assert.Equal(samples, ((JArray)result["samples"]).Count);
            Assert.DoesNotContain("reservation_sample_truncated", result["blockers"].Values<string>());

            Assert.Null(result["canAutomaticallyApprove"]);
            Assert.Null(result["isProactive"]);
        }

        [Fact]
        public async Task PreservesRawParentAndSharedOwnedOverlapEvidence()
        {
            var connection = new FakeConnection();
            var table = Reservations(1, 1);
            table.Rows[0]["namespaceValue"] = "Cont";
            table.Rows[0]["relationship"] = "parent";
            table.Rows[0]["parentCount"] = 1L;
            table.Rows[0]["descendantCount"] = 0L;
            table.Rows[0]["isSharedNamespace"] = true;
            table.Rows[0]["sharedCount"] = 1L;
            table.Rows[0]["requestedOwnerCount"] = 1L;
            connection.Results.Enqueue(table);

            var result = await Session(connection).ExecuteAsync("get_namespace_reservations", new JObject(), CancellationToken.None);

            Assert.True((bool)result["requiresReview"]);
            Assert.Equal("Cont", (string)result["samples"][0]["namespaceValue"]);
            Assert.True((bool)result["samples"][0]["isSharedNamespace"]);
            Assert.Contains("LEFT(@namespace, LEN(rn.Value)) COLLATE Latin1_General_100_CI_AS = rn.Value", connection.Commands[0].CommandText);
            Assert.Contains("rn.IsPrefix = 1", connection.Commands[0].CommandText);
            Assert.Contains("LIKE @rawPrefix ESCAPE '~'", connection.Commands[0].CommandText);
        }

        [Theory]
        [InlineData(0, 0, true)]
        [InlineData(2, 2, true)]
        [InlineData(200, 20, false)]
        public async Task UsageKeepsFullAggregatesAndNeverInfersPublicationQualification(int total, int sampleCount, bool sampleComplete)
        {
            var connection = new FakeConnection();
            var ids = Enumerable.Range(0, sampleCount).Select(i => "Contoso.P" + i).ToArray();
            var table = Usage(total, ids);
            var versions = total == 0 ? 0L : 1250L;
            var unavailableVersions = total == 0 ? 0L : 100L;
            var thirdPartyOnlyPackages = total / 2;
            var coownedPackages = total / 4;
            table.Rows[0]["versionCount"] = versions;
            table.Rows[0]["availableVersionCount"] = total == 0 ? 0L : 1000L;
            table.Rows[0]["deletedVersionCount"] = unavailableVersions;
            table.Rows[0]["validatingVersionCount"] = unavailableVersions;
            table.Rows[0]["failedValidationVersionCount"] = total == 0 ? 0L : 50L;
            table.Rows[0]["thirdPartyOnlyPackageCount"] = (long)thirdPartyOnlyPackages;
            table.Rows[0]["coownedWithThirdPartyPackageCount"] = (long)coownedPackages;
            connection.Results.Enqueue(table);

            var result = await Session(connection).ExecuteAsync("get_namespace_package_usage", new JObject(), CancellationToken.None);

            Assert.True((bool)result["complete"]);
            Assert.True((bool)result["aggregatesComplete"]);
            Assert.Equal(sampleComplete, (bool)result["sampleComplete"]);
            Assert.Equal(sampleCount, ((JArray)result["samples"]).Count);
            Assert.Equal(total, (long)result["counts"]["packageCount"]);
            Assert.Equal(versions, (long)result["counts"]["versionCount"]);
            Assert.Equal(unavailableVersions, (long)result["counts"]["deletedVersionCount"]);
            Assert.Equal(unavailableVersions, (long)result["counts"]["validatingVersionCount"]);
            Assert.Equal(thirdPartyOnlyPackages, (long)result["counts"]["thirdPartyOnlyPackageCount"]);
            Assert.Equal(coownedPackages, (long)result["counts"]["coownedWithThirdPartyPackageCount"]);
            AssertUnknownPublication(result);
            Assert.All(result["samples"].Cast<JObject>(), AssertUnknownPublication);
            Assert.All(result["samples"].Cast<JObject>(), sample => Assert.True((bool)sample["complete"]));
            Assert.Empty((JArray)result["blockers"]);
            Assert.Contains("not_all_policy_checks", (string)result["completenessScope"]);
            if (total == 0)
            {
                Assert.Equal(1L, (long)result["counts"]["requestedOwnerCount"]);
                Assert.All(NamespaceReservationSqlQueries.UsageCounts.Except(NamespaceReservationSqlQueries.DateFields)
                    .Where(field => field != "requestedOwnerCount"),
                    field => Assert.Equal(0L, (long)result["counts"][field]));
            }
        }

        [Fact]
        public async Task DetailsAreBoundToPreviouslyReturnedIdsAndDoNotCrossSessions()
        {
            var connection = new FakeConnection();
            connection.Results.Enqueue(Usage(100, "Contoso", "Contoso.Tools"));
            connection.Results.Enqueue(Details("Contoso.Tools"));
            var session = Session(connection);
            await session.ExecuteAsync("get_namespace_package_usage", new JObject(), CancellationToken.None);

            AssertFailure(await session.ExecuteAsync("get_package_details", DetailArgs("Contoso.NotSampled"), CancellationToken.None), "package_id_not_in_session_evidence");
            AssertFailure(await Session(connection).ExecuteAsync("get_package_details", DetailArgs("Contoso.Tools"), CancellationToken.None), "package_id_not_in_session_evidence");
            AssertFailure(await session.ExecuteAsync("get_package_details", DetailArgs("Other"), CancellationToken.None), "package_id_not_in_session_evidence");
            var result = await session.ExecuteAsync("get_package_details", DetailArgs("contoso.tools"), CancellationToken.None);

            Assert.Equal(2, connection.Commands.Count);
            Assert.Equal(3, connection.Commands[1].CommandTimeout);
            Assert.Equal("contoso.tools", connection.Commands[1].Parameters["@package0"].Value);
            Assert.Equal(DBNull.Value, connection.Commands[1].Parameters["@package1"].Value);
            Assert.True((bool)result["sampleComplete"]);
            Assert.True((bool)result["complete"]);
            Assert.True((bool)result["aggregatesComplete"]);
            Assert.Equal("Contoso.Tools", (string)result["packages"][0]["packageId"]);
            AssertUnknownPublication(result);
            AssertUnknownPublication((JObject)result["packages"][0]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(9)]
        [InlineData(10)]
        public async Task AcceptsTenPreviouslyReturnedIdsAndReportsDisappearedPackages(int returnedCount)
        {
            var connection = new FakeConnection();
            var ids = Enumerable.Range(0, 10).Select(i => "Contoso.P" + i).ToArray();
            connection.Results.Enqueue(Usage(10, ids));
            connection.Results.Enqueue(Details(ids.Take(returnedCount).ToArray()));
            var session = Session(connection);
            await session.ExecuteAsync("get_namespace_package_usage", new JObject(), CancellationToken.None);

            var result = await session.ExecuteAsync("get_package_details", DetailArgs(ids), CancellationToken.None);

            Assert.Equal(returnedCount == 10, (bool)result["complete"]);
            Assert.Equal(returnedCount == 10, (bool)result["sampleComplete"]);
            Assert.Equal(returnedCount == 10, (bool)result["aggregatesComplete"]);
            Assert.Equal(10, (int)result["requestedPackageCount"]);
            Assert.Equal(returnedCount, (int)result["returnedPackageCount"]);
            AssertUnknownPublication(result);
            if (returnedCount < 10)
            {
                Assert.Contains("previously_observed_package_missing", result["blockers"].Values<string>());
            }
            else
            {
                Assert.Empty((JArray)result["blockers"]);
            }
        }

        [Theory]
        [InlineData("Contoso", "Contoso")]
        [InlineData("Contoso", "Other.Tools")]
        public async Task EqualDetailRowCountCannotHideMissingOrUnexpectedIds(string first, string second)
        {
            var connection = new FakeConnection();
            connection.Results.Enqueue(Usage(2, "Contoso", "Contoso.Tools"));
            connection.Results.Enqueue(Details(first, second));
            var session = Session(connection);
            await session.ExecuteAsync("get_namespace_package_usage", new JObject(), CancellationToken.None);

            var result = await session.ExecuteAsync("get_package_details", DetailArgs("Contoso", "Contoso.Tools"), CancellationToken.None);

            AssertFailure(result, "unexpected_package_projection");
            Assert.Null(result["packages"]);
        }

        [Fact]
        public async Task UsagePreservesCompletePerIdThirdPartyAggregatesInsteadOfCountingSamples()
        {
            var connection = new FakeConnection();
            var usage = Usage(100, "Contoso.Sample");
            var expected = new Dictionary<string, long>
            {
                ["requestedOwnerCount"] = 1,
                ["requestedOwnerWithPublishedPackageCount"] = 1,
                ["requestedOwnerPublishedPackageCount"] = 10,
                ["requestedOwnerPackageCount"] = 10,
                ["coownedWithThirdPartyPackageCount"] = 10,
                ["thirdPartyOnlyPackageCount"] = 90,
                ["thirdPartyOnlyListedPackageCount"] = 37,
                ["thirdPartyOnlyUnlistedPackageCount"] = 41,
                ["thirdPartyOnlyUncertainPackageCount"] = 22,
                ["thirdPartyOnlyRecentStoredPublicationAndListedPackageCount"] = 17
            };
            foreach (var pair in expected)
            {
                usage.Rows[0][pair.Key] = pair.Value;
            }

            connection.Results.Enqueue(usage);
            var result = await Session(connection).ExecuteAsync("get_namespace_package_usage", new JObject(), CancellationToken.None);

            Assert.True((bool)result["complete"]);
            Assert.True((bool)result["aggregatesComplete"]);
            Assert.False((bool)result["sampleComplete"]);
            Assert.Single((JArray)result["samples"]);
            foreach (var pair in expected)
            {
                Assert.Equal(pair.Value, (long)result["counts"][pair.Key]);
            }

            Assert.Equal(1L, (long)result["samples"][0]["requestedOwnerCount"]);
            Assert.Equal(2L, (long)result["samples"][0]["ownerCount"]);
            Assert.Contains("per_package_id", (string)result["thirdPartyOnlyCountSemantics"]);
            Assert.Contains("listed_and_uncertain_may_overlap", (string)result["thirdPartyOnlyCountSemantics"]);
            Assert.Contains("these_may_be_different_versions", (string)result["recentStoredPublicationAndListedSemantics"]);
            Assert.Contains("not_verified_publication_activity_or_first_publication", (string)result["recentStoredPublicationAndListedSemantics"]);
            AssertUnknownPublication(result);
            Assert.Empty((JArray)result["blockers"]);
        }

        [Theory]
        [InlineData("get_request_account_facts", "incomplete_account_projection")]
        [InlineData("get_namespace_reservations", "missing_reservation_aggregate")]
        [InlineData("get_namespace_package_usage", "evidence_unavailable")]
        public async Task MissingSqlProjectionIsNotACompleteEmptyResult(string tool, string error)
        {
            var connection = new FakeConnection();
            connection.Results.Enqueue(new DataTable());

            var result = await Session(connection).ExecuteAsync(tool, new JObject(), CancellationToken.None);

            AssertFailure(result, error);
        }

        [Theory]
        [InlineData("get_request_account_facts")]
        [InlineData("get_namespace_reservations")]
        [InlineData("get_namespace_package_usage")]
        public async Task UsesOneFixedSelectWithThreeSecondTimeoutAndNoRetries(string toolName)
        {
            var connection = new FakeConnection { ExecuteFailure = new InvalidOperationException("private SQL details") };
            var result = await Session(connection).ExecuteAsync(toolName, new JObject(), CancellationToken.None);

            AssertFailure(result, "evidence_unavailable");
            var command = Assert.Single(connection.Commands);
            Assert.Equal(3, command.CommandTimeout);
            Assert.Equal(CommandType.Text, command.CommandType);
            Assert.StartsWith("WITH RequestedOwners AS", command.CommandText);
            Assert.DoesNotContain(";", command.CommandText);
            Assert.DoesNotContain("FOR JSON", command.CommandText);
            Assert.DoesNotContain("LastUpdated", command.CommandText);
            Assert.DoesNotContain("NOLOCK", command.CommandText);
            Assert.Equal(1, connection.ExecuteCount);
            Assert.Equal(1, connection.CloseCount);
            Assert.False(connection.WasDisposed);
        }

        [Fact]
        public void FixedPackageSqlCoversDottedScopeAllVersionsAndOwnershipSeparately()
        {
            Assert.Contains("pr.Id COLLATE Latin1_General_100_CI_AS = @namespace OR pr.Id COLLATE Latin1_General_100_CI_AS LIKE @dottedPrefix ESCAPE '~'", NamespaceReservationSqlQueries.Usage);
            Assert.Contains("FROM dbo.Packages p WHERE p.PackageRegistrationKey = pr.[Key]", NamespaceReservationSqlQueries.Usage);
            Assert.DoesNotContain("WHERE p.PackageStatusKey", NamespaceReservationSqlQueries.Usage);
            Assert.DoesNotContain("IsLatest", NamespaceReservationSqlQueries.Usage);
            Assert.Contains("requestedOwnerCount = 0 AND ownerCount > 0", NamespaceReservationSqlQueries.Usage);
            Assert.Contains("requestedOwnerCount > 0 AND ownerCount > requestedOwnerCount", NamespaceReservationSqlQueries.Usage);
            Assert.Contains("AS requestedOwnerWithPublishedPackageCount", NamespaceReservationSqlQueries.Usage);
            Assert.Contains("package.PackageStatusKey = 0", NamespaceReservationSqlQueries.Usage);
            Assert.Contains("COUNT_BIG(*)", NamespaceReservationSqlQueries.Usage);
            Assert.Contains("SELECT TOP (20) * FROM PackagesInScope", NamespaceReservationSqlQueries.Usage);
            Assert.Contains("SELECT TOP (10)", NamespaceReservationSqlQueries.Details);
            Assert.Contains("@package9", NamespaceReservationSqlQueries.Details);
            Assert.DoesNotContain("@package10", NamespaceReservationSqlQueries.Details);
        }

        [Theory]
        [InlineData("thirdPartyOnlyListedPackageCount", "listedAvailableVersionCount > 0")]
        [InlineData("thirdPartyOnlyUnlistedPackageCount", "availableVersionCount > 0 AND listedAvailableVersionCount = 0 AND deletedVersionCount = 0 AND validatingVersionCount = 0 AND failedValidationVersionCount = 0 AND unknownStatusVersionCount = 0")]
        [InlineData("thirdPartyOnlyUncertainPackageCount", "(availableVersionCount = 0 OR deletedVersionCount > 0 OR validatingVersionCount > 0 OR failedValidationVersionCount > 0 OR unknownStatusVersionCount > 0)")]
        [InlineData("thirdPartyOnlyRecentStoredPublicationAndListedPackageCount", "publishedSinceCutoffAvailableVersionCount > 0 AND listedAvailableVersionCount > 0")]
        public void ThirdPartyCountsAggregatePerIdWithExplicitOwnershipAndStatusPredicates(string field, string predicate)
        {
            var sql = string.Join(" ", NamespaceReservationSqlQueries.Usage.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            var aggregate = "COALESCE(SUM(CONVERT(bigint, CASE WHEN requestedOwnerCount = 0 AND ownerCount > 0 AND "
                + predicate + " THEN 1 ELSE 0 END)), 0) AS " + field;

            Assert.Contains(field, NamespaceReservationSqlQueries.UsageCounts);
            Assert.Contains(aggregate, sql);
            Assert.Contains("FROM PackagesInScope ) SELECT", sql);
            Assert.Contains("s." + field, sql);
            Assert.True(sql.IndexOf(aggregate, StringComparison.Ordinal) < sql.IndexOf("SELECT TOP (20)", StringComparison.Ordinal));
            Assert.Contains("p.PackageStatusKey = 0 AND p.Published >= @cutoff AND p.Published <= @assessment", sql);
            Assert.DoesNotContain("LastUpdated", sql);
        }

        [Fact]
        public void SqlNamespaceAndDetailComparisonsIgnoreDatabaseDefaultCaseSensitivity()
        {
            const string scope = "pr.Id COLLATE Latin1_General_100_CI_AS = @namespace OR pr.Id COLLATE Latin1_General_100_CI_AS LIKE @dottedPrefix ESCAPE '~'";
            Assert.Contains(scope, NamespaceReservationSqlQueries.Usage);
            Assert.Contains(scope, NamespaceReservationSqlQueries.Details);
            Assert.Contains("AND pr.Id COLLATE Latin1_General_100_CI_AS IN (@package0,", NamespaceReservationSqlQueries.Details);
            Assert.Contains("CASE WHEN rn.Value COLLATE Latin1_General_100_CI_AS = @namespace", NamespaceReservationSqlQueries.Reservations);
            Assert.Contains("WHERE rn.Value COLLATE Latin1_General_100_CI_AS = @namespace", NamespaceReservationSqlQueries.Reservations);
            Assert.Contains("WHEN rn.IsPrefix = 1 AND LEFT(@namespace, LEN(rn.Value)) COLLATE Latin1_General_100_CI_AS = rn.Value", NamespaceReservationSqlQueries.Reservations);
            Assert.Contains("OR (rn.IsPrefix = 1 AND LEFT(@namespace, LEN(rn.Value)) COLLATE Latin1_General_100_CI_AS = rn.Value)", NamespaceReservationSqlQueries.Reservations);
            Assert.Contains("rn.Value COLLATE Latin1_General_100_CI_AS LIKE @rawPrefix ESCAPE '~'", NamespaceReservationSqlQueries.Reservations);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task TenStatementBudgetIncludesFailedOpenAndExecuteAttempts(bool failOpen)
        {
            var connection = new FakeConnection
            {
                OpenFailure = failOpen ? new InvalidOperationException("Server=secret;Password=secret") : null,
                ExecuteFailure = new InvalidOperationException("private@microsoft.com SQL 812345")
            };
            var session = Session(connection);
            for (var i = 0; i < 10; i++)
            {
                var result = await session.ExecuteAsync("get_namespace_reservations", new JObject(), CancellationToken.None);
                AssertFailure(result, "evidence_unavailable");
                Assert.DoesNotContain("secret", result.ToString());
                Assert.DoesNotContain("private@microsoft.com", result.ToString());
                Assert.DoesNotContain("812345", result.ToString());
            }

            AssertFailure(await session.ExecuteAsync("get_request_account_facts", new JObject(), CancellationToken.None), "statement_budget_exhausted");
            Assert.Equal(10, connection.Commands.Count);
            Assert.Equal(10, connection.OpenCount);
            Assert.Equal(failOpen ? 0 : 10, connection.ExecuteCount);
        }

        [Fact]
        public async Task InvalidArgumentsDoNotSpendSqlBudgetAndSuccessfulCallsDo()
        {
            var connection = new FakeConnection();
            var session = Session(connection);
            for (var i = 0; i < 12; i++)
            {
                AssertFailure(await session.ExecuteAsync("bad", new JObject(), CancellationToken.None), "invalid_tool_arguments");
            }

            for (var i = 0; i < 10; i++)
            {
                connection.Results.Enqueue(Reservations(0, 0));
                var result = await session.ExecuteAsync("get_namespace_reservations", new JObject(), CancellationToken.None);
                Assert.True((bool)result["complete"]);
            }

            AssertFailure(await session.ExecuteAsync("get_namespace_reservations", new JObject(), CancellationToken.None), "statement_budget_exhausted");
            Assert.Equal(10, connection.ExecuteCount);
        }

        [Fact]
        public async Task PreCancelledRequestDoesNotTouchConnection()
        {
            var connection = new FakeConnection();
            var result = await Session(connection).ExecuteAsync("get_namespace_reservations", new JObject(), new CancellationToken(true));

            AssertFailure(result, "cancelled");
            Assert.Empty(connection.Commands);
        }

        [Theory]
        [InlineData("open")]
        [InlineData("execute")]
        [InlineData("read")]
        public async Task PropagatesCancellationThroughEachAsyncSqlStage(string stage)
        {
            using (var source = new CancellationTokenSource())
            {
                var connection = new FakeConnection { CancellationStage = stage };
                connection.Results.Enqueue(Reservations(0, 0));
                var result = await Session(connection).ExecuteAsync("get_namespace_reservations", new JObject(), source.Token);

                AssertFailure(result, "cancelled");
                Assert.Equal(source.Token, connection.OpenToken);
                if (stage != "open")
                {
                    Assert.Equal(source.Token, connection.ExecuteToken);
                }

                if (stage == "read")
                {
                    Assert.Equal(source.Token, connection.ReadToken);
                }

                Assert.Equal(1, connection.CloseCount);
            }
        }

        [Fact]
        public async Task BorrowsAlreadyOpenConnectionWithoutClosingOrDisposingIt()
        {
            var connection = new FakeConnection(initiallyOpen: true);
            connection.Results.Enqueue(Reservations(0, 0));
            var result = await Session(connection).ExecuteAsync("get_namespace_reservations", new JObject(), CancellationToken.None);

            Assert.True((bool)result["complete"]);
            Assert.Equal(0, connection.OpenCount);
            Assert.Equal(0, connection.CloseCount);
            Assert.False(connection.WasDisposed);
            Assert.Equal(ConnectionState.Open, connection.State);
        }

        [Fact]
        public async Task SerializesConnectionAccessAndCopiesArgumentsBeforeAwaiting()
        {
            var connection = new FakeConnection();
            connection.Results.Enqueue(Usage(1, "Contoso"));
            connection.Results.Enqueue(Details("Contoso"));
            var gate = new SemaphoreSlim(1, 1);
            var session = Session(connection, gate: gate);
            await session.ExecuteAsync("get_namespace_package_usage", new JObject(), CancellationToken.None);
            await gate.WaitAsync();
            var arguments = DetailArgs("Contoso");
            var pending = session.ExecuteAsync("get_package_details", arguments, CancellationToken.None);
            arguments["packageIds"][0] = "Other";
            Assert.False(pending.IsCompleted);
            Assert.Single(connection.Commands);
            gate.Release();
            var result = await pending;

            Assert.True((bool)result["sampleComplete"]);
            Assert.Equal("Contoso", connection.Commands[1].Parameters["@package0"].Value);
        }

        [Fact]
        public async Task RejectsUnexpectedExtraRowsWithoutReturningOrAuthorizingPartialSamples()
        {
            var connection = new FakeConnection();
            connection.Results.Enqueue(Usage(21, Enumerable.Range(0, 21).Select(i => "Contoso.P" + i).ToArray()));
            var session = Session(connection);
            var result = await session.ExecuteAsync("get_namespace_package_usage", new JObject(), CancellationToken.None);

            AssertFailure(result, "evidence_unavailable");
            Assert.Null(result["samples"]);
            AssertFailure(await session.ExecuteAsync("get_package_details", DetailArgs("Contoso.P0"), CancellationToken.None), "package_id_not_in_session_evidence");
            Assert.Single(connection.Commands);
        }

        private static NamespaceReservationSqlEvidenceSession Session(
            FakeConnection connection,
            int[] owners = null,
            string namespaceValue = "Contoso",
            int submitterKey = 1,
            SemaphoreSlim gate = null)
        {
            return new NamespaceReservationSqlEvidenceSession(
                () => connection, () => null, gate ?? new SemaphoreSlim(1, 1),
                submitterKey, owners ?? new[] { 2 }, namespaceValue, AssessmentTime);
        }

        private static JObject DetailArgs(params string[] ids)
        {
            return new JObject { ["packageIds"] = new JArray(ids) };
        }

        private static void AssertFailure(JObject result, string code)
        {
            Assert.False((bool)result["complete"]);
            Assert.False((bool)result["eligible"]);
            Assert.True((bool)result["requiresReview"]);
            Assert.Equal(code, (string)result["error"]);
            Assert.Contains(code, result["blockers"].Values<string>());
        }

        private static void AssertUnknownPublication(JObject result)
        {
            Assert.Equal("unknown", (string)result["publicationHistoryStatus"]);
            Assert.Equal(JTokenType.Null, result["firstPublicationUtc"].Type);
            Assert.Equal(JTokenType.Null, result["latestFirstPublicationUtc"].Type);
            Assert.Equal(JTokenType.Null, result["allReleasesOlderThanCutoff"].Type);
            Assert.DoesNotContain("immutable_first_publication_history_not_available", result["blockers"].Values<string>());
            Assert.DoesNotContain("hard_deleted_or_nonavailable_release_history_not_established", result["blockers"].Values<string>());
            Assert.Contains("Optional historical evidence is unknown", (string)result["publicationHistoryExplanation"]);
            Assert.Contains("not immutable first-publication dates", (string)result["publicationHistoryExplanation"]);
            Assert.Contains("does not invalidate current ownership, listing, or zero-current-package facts", (string)result["publicationHistoryExplanation"]);
        }

        private static DataTable Accounts()
        {
            var table = Table("Ordinal", "Username", "EmailAddress", "IsDeleted", "UserStatusKey", "AccountExists",
                "IsOrganization", "AdminMembership", "IsSubmitter", "MicrosoftPolicySubscriptionObserved");
            table.Rows.Add(0L, "Submitter", "private@microsoft.com", false, 0, true, false, false, true, false);
            table.Rows.Add(1L, "Owner", "owner@example.org", false, 0, true, true, true, false, true);
            return table;
        }

        private static DataTable Reservations(long total, int sampleCount)
        {
            var table = Table(NamespaceReservationSqlQueries.ReservationCounts.Concat(NamespaceReservationSqlQueries.ReservationFields).ToArray());
            for (var i = 0; i < Math.Max(1, sampleCount); i++)
            {
                var row = table.NewRow();
                row["overlapCount"] = total;
                row["exactCount"] = 0L;
                row["parentCount"] = 0L;
                row["descendantCount"] = total;
                row["sharedCount"] = 0L;
                if (sampleCount > 0)
                {
                    row["namespaceValue"] = "Contoso.P" + i;
                    row["isPrefix"] = true;
                    row["isSharedNamespace"] = false;
                    row["relationship"] = "descendant";
                    row["ownerCount"] = 1L;
                    row["requestedOwnerCount"] = 0L;
                }

                table.Rows.Add(row);
            }

            return table;
        }

        private static DataTable Usage(long total, params string[] ids)
        {
            var table = Table(NamespaceReservationSqlQueries.UsageCounts
                .Concat(NamespaceReservationSqlQueries.PackageFields.Select(f => "sample_" + f)).ToArray());
            for (var i = 0; i < Math.Max(1, ids.Length); i++)
            {
                var row = table.NewRow();
                foreach (var name in NamespaceReservationSqlQueries.UsageCounts.Except(NamespaceReservationSqlQueries.DateFields))
                {
                    row[name] = 0L;
                }

                row["packageCount"] = total;
                row["requestedOwnerCount"] = 1L;
                if (ids.Length > 0)
                {
                    FillPackage(row, ids[i], "sample_");
                }

                table.Rows.Add(row);
            }

            return table;
        }

        private static DataTable Details(params string[] ids)
        {
            var table = Table(NamespaceReservationSqlQueries.PackageFields);
            foreach (var id in ids)
            {
                var row = table.NewRow();
                FillPackage(row, id, string.Empty);
                table.Rows.Add(row);
            }

            return table;
        }

        private static void FillPackage(DataRow row, string id, string prefix)
        {
            row[prefix + "packageId"] = id;
            row[prefix + "ownerCount"] = 2L;
            row[prefix + "requestedOwnerCount"] = 1L;
            foreach (var field in NamespaceReservationSqlQueries.VersionCounts)
            {
                row[prefix + field] = 0L;
            }

            row[prefix + "versionCount"] = 2L;
            row[prefix + "availableVersionCount"] = 1L;
            row[prefix + "validatingVersionCount"] = 1L;
            foreach (var field in NamespaceReservationSqlQueries.DateFields)
            {
                row[prefix + field] = AssessmentTime.AddYears(-2);
            }
        }

        private static DataTable Table(params string[] fields)
        {
            var table = new DataTable();
            foreach (var field in fields)
            {
                table.Columns.Add(field, typeof(object));
            }

            return table;
        }

        // Entirely in-memory DbConnection/DbCommand seam. The SqlCommand below is only a
        // parameter collection; it is never connected or executed. No SQL server is contacted.
        private sealed class FakeConnection : DbConnection
        {
            private ConnectionState _state;

            public FakeConnection(bool initiallyOpen = false)
            {
                _state = initiallyOpen ? ConnectionState.Open : ConnectionState.Closed;
            }

            public Queue<DataTable> Results { get; } = new Queue<DataTable>();
            public List<FakeCommand> Commands { get; } = new List<FakeCommand>();
            public Exception OpenFailure { get; set; }
            public Exception ExecuteFailure { get; set; }
            public string CancellationStage { get; set; }
            public int OpenCount { get; private set; }
            public int CloseCount { get; private set; }
            public int ExecuteCount { get; private set; }
            public bool WasDisposed { get; private set; }
            public CancellationToken OpenToken { get; private set; }
            public CancellationToken ExecuteToken { get; private set; }
            public CancellationToken ReadToken { get; private set; }
            public override string ConnectionString { get; set; }
            public override string Database => "in-memory";
            public override string DataSource => "in-memory";
            public override string ServerVersion => "not-a-server";
            public override ConnectionState State => _state;

            public override Task OpenAsync(CancellationToken cancellationToken)
            {
                OpenCount++;
                OpenToken = cancellationToken;
                _state = ConnectionState.Open;
                cancellationToken.ThrowIfCancellationRequested();
                if (CancellationStage == "open")
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                if (OpenFailure != null)
                {
                    throw OpenFailure;
                }

                return Task.CompletedTask;
            }

            public override void Close()
            {
                CloseCount++;
                _state = ConnectionState.Closed;
            }

            public override void Open()
            {
                throw new NotSupportedException("Only asynchronous open is expected.");
            }

            public override void ChangeDatabase(string databaseName)
            {
                throw new NotSupportedException();
            }

            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            {
                throw new NotSupportedException();
            }

            protected override DbCommand CreateDbCommand()
            {
                var command = new FakeCommand(this);
                Commands.Add(command);
                return command;
            }

            protected override void Dispose(bool disposing)
            {
                WasDisposed = true;
                base.Dispose(disposing);
            }

            public Task<DbDataReader> ExecuteAsync(CancellationToken cancellationToken)
            {
                ExecuteCount++;
                ExecuteToken = cancellationToken;
                cancellationToken.ThrowIfCancellationRequested();
                if (CancellationStage == "execute")
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                if (ExecuteFailure != null)
                {
                    throw ExecuteFailure;
                }

                var inner = Results.Dequeue().CreateDataReader();
                var reader = new Mock<DbDataReader>();
                reader.Setup(r => r[It.IsAny<string>()]).Returns((string field) => inner[field]);
                reader.Setup(r => r.ReadAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken token) =>
                {
                    ReadToken = token;
                    token.ThrowIfCancellationRequested();
                    if (CancellationStage == "read")
                    {
                        throw new OperationCanceledException(token);
                    }

                    return Task.FromResult(inner.Read());
                });
                return Task.FromResult(reader.Object);
            }
        }

        private sealed class FakeCommand : DbCommand
        {
            private readonly FakeConnection _connection;
            private readonly SqlCommand _parameterContainer = new SqlCommand();

            public FakeCommand(FakeConnection connection)
            {
                _connection = connection;
            }

            public override string CommandText { get; set; }
            public override int CommandTimeout { get; set; }
            public override CommandType CommandType { get; set; }
            public override bool DesignTimeVisible { get; set; }
            public override UpdateRowSource UpdatedRowSource { get; set; }
            protected override DbConnection DbConnection { get; set; }
            protected override DbTransaction DbTransaction { get; set; }
            protected override DbParameterCollection DbParameterCollection => _parameterContainer.Parameters;

            protected override DbParameter CreateDbParameter()
            {
                return new SqlParameter();
            }

            protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
            {
                Assert.Equal(CommandBehavior.SingleResult, behavior);
                return _connection.ExecuteAsync(cancellationToken);
            }

            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            {
                throw new NotSupportedException("Only asynchronous reads are expected.");
            }

            public override int ExecuteNonQuery()
            {
                throw new NotSupportedException("Writes are forbidden.");
            }

            public override object ExecuteScalar()
            {
                throw new NotSupportedException();
            }

            public override void Cancel()
            {
            }

            public override void Prepare()
            {
                throw new NotSupportedException();
            }
        }
    }
}