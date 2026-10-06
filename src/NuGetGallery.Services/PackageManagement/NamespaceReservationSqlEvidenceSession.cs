// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;
using NuGetGallery.Security;

namespace NuGetGallery
{
    internal sealed class NamespaceReservationSqlEvidenceSession : INamespaceReservationEvidenceSession
    {
        // Nineteen owners plus the submitter keeps even the account projection at twenty rows.
        internal const int MaxOwnerCount = 19;
        internal const int MaxStatements = 10;
        internal const int CommandTimeoutSeconds = 3;
        private const int SampleLimit = 20;
        private readonly Func<DbConnection> _getConnection;
        private readonly Func<DbTransaction> _getTransaction;
        private readonly SemaphoreSlim _connectionGate;
        private readonly int _submitterKey;
        private readonly int[] _ownerKeys;
        private readonly string _namespaceValue;
        private readonly DateTime _assessmentTimeUtc;
        private readonly HashSet<string> _returnedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int _statementAttempts;

        internal NamespaceReservationSqlEvidenceSession(
            Func<DbConnection> getConnection,
            Func<DbTransaction> getTransaction,
            SemaphoreSlim connectionGate,
            int submitterKey,
            int[] ownerKeys,
            string namespaceValue,
            DateTime assessmentTimeUtc)
        {
            _getConnection = getConnection ?? throw new ArgumentNullException(nameof(getConnection));
            _getTransaction = getTransaction ?? throw new ArgumentNullException(nameof(getTransaction));
            _connectionGate = connectionGate ?? throw new ArgumentNullException(nameof(connectionGate));
            if (submitterKey <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(submitterKey));
            }

            if (ownerKeys == null)
            {
                throw new ArgumentNullException(nameof(ownerKeys));
            }

            if (ownerKeys.Length == 0 || ownerKeys.Length > MaxOwnerCount)
            {
                throw new ArgumentException("Invalid owner scope.", nameof(ownerKeys));
            }

            // Clone before inspecting or retaining IDs supplied by the caller.
            _ownerKeys = (int[])ownerKeys.Clone();
            if (_ownerKeys.Any(k => k <= 0) || _ownerKeys.Distinct().Count() != _ownerKeys.Length)
            {
                throw new ArgumentException("Invalid owner scope.", nameof(ownerKeys));
            }

            if (!IsNamespaceBase(namespaceValue))
            {
                throw new ArgumentException("Invalid namespace scope.", nameof(namespaceValue));
            }

            if (assessmentTimeUtc.Kind != DateTimeKind.Utc || assessmentTimeUtc.Year <= 1)
            {
                throw new ArgumentException("A UTC assessment time is required.", nameof(assessmentTimeUtc));
            }

            _submitterKey = submitterKey;
            _namespaceValue = namespaceValue;
            _assessmentTimeUtc = assessmentTimeUtc;
        }

        public async Task<JObject> ExecuteAsync(string toolName, JObject arguments, CancellationToken cancellationToken)
        {
            // Copy the small, exact argument contract before the first await. Never accept SQL,
            // scope overrides, pagination, time cutoffs, or arbitrary projections from the caller.
            if (!TryReadArguments(toolName, arguments, out var packageIds))
            {
                return Failure("invalid_tool_arguments");
            }

            var entered = false;
            try
            {
                await _connectionGate.WaitAsync(cancellationToken);
                entered = true;
                cancellationToken.ThrowIfCancellationRequested();
                if (packageIds != null && packageIds.Any(id => !_returnedPackageIds.Contains(id)))
                {
                    return Failure("package_id_not_in_session_evidence");
                }

                if (_statementAttempts >= MaxStatements)
                {
                    return Failure("statement_budget_exhausted");
                }

                // Every tool uses exactly one SELECT. Count attempts before even obtaining/opening
                // the connection so failures cannot circumvent the budget. There is no retry.
                _statementAttempts++;
                var rows = await ReadAsync(toolName, packageIds, cancellationToken);
                switch (toolName)
                {
                    case "get_request_account_facts":
                        return AccountResult(rows);
                    case "get_namespace_reservations":
                        return ReservationResult(rows);
                    case "get_namespace_package_usage":
                        var usage = UsageResult(rows);
                        foreach (var sample in (JArray)usage["samples"])
                        {
                            _returnedPackageIds.Add((string)sample["packageId"]);
                        }

                        return usage;
                    default:
                        return DetailsResult(rows, packageIds);
                }
            }
            catch (OperationCanceledException)
            {
                return Failure("cancelled");
            }
            catch (Exception)
            {
                // Tool boundary: no provider messages, SQL text, parameters, connection strings,
                // entities, or exception types are sent to the assessment client.
                return Failure("evidence_unavailable");
            }
            finally
            {
                if (entered)
                {
                    _connectionGate.Release();
                }
            }
        }

        private async Task<List<JObject>> ReadAsync(string toolName, string[] packageIds, CancellationToken cancellationToken)
        {
            var connection = _getConnection();
            var openedHere = false;
            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = CommandTimeoutSeconds;
                    command.Transaction = _getTransaction();
                    AddParameters(command, packageIds);
                    switch (toolName)
                    {
                        case "get_request_account_facts":
                            command.CommandText = NamespaceReservationSqlQueries.Accounts;
                            break;
                        case "get_namespace_reservations":
                            command.CommandText = NamespaceReservationSqlQueries.Reservations;
                            break;
                        case "get_namespace_package_usage":
                            command.CommandText = NamespaceReservationSqlQueries.Usage;
                            break;
                        default:
                            command.CommandText = NamespaceReservationSqlQueries.Details;
                            break;
                    }

                    if (connection.State == ConnectionState.Closed)
                    {
                        // Also restore a connection whose asynchronous open failed part way through.
                        openedHere = true;
                        await connection.OpenAsync(cancellationToken);
                    }

                    var rows = new List<JObject>();
                    var rowLimit = packageIds == null ? SampleLimit : packageIds.Length;
                    using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            if (rows.Count >= rowLimit)
                            {
                                throw new InvalidOperationException("Unexpected evidence row count.");
                            }

                            switch (toolName)
                            {
                                case "get_request_account_facts":
                                    rows.Add(ReadAccount(reader));
                                    break;
                                case "get_namespace_reservations":
                                    rows.Add(ReadFields(reader, NamespaceReservationSqlQueries.ReservationCounts
                                        .Concat(NamespaceReservationSqlQueries.ReservationFields)));
                                    break;
                                case "get_namespace_package_usage":
                                    rows.Add(ReadFields(reader, NamespaceReservationSqlQueries.UsageCounts
                                        .Concat(NamespaceReservationSqlQueries.PackageFields.Select(f => "sample_" + f))));
                                    break;
                                default:
                                    rows.Add(ReadFields(reader, NamespaceReservationSqlQueries.PackageFields));
                                    break;
                            }
                        }
                    }

                    return rows;
                }
            }
            finally
            {
                if (openedHere)
                {
                    connection.Close();
                }
            }
        }

        private void AddParameters(DbCommand command, string[] packageIds)
        {
            AddParameter(command, "@submitter", DbType.Int32, _submitterKey);
            for (var i = 0; i < MaxOwnerCount; i++)
            {
                AddParameter(command, "@owner" + i, DbType.Int32, i < _ownerKeys.Length ? (object)_ownerKeys[i] : DBNull.Value);
            }

            AddParameter(command, "@namespace", DbType.String, _namespaceValue, 128);
            AddParameter(command, "@rawPrefix", DbType.String, EscapeLike(_namespaceValue) + "%", 260);
            AddParameter(command, "@dottedPrefix", DbType.String, EscapeLike(_namespaceValue) + ".%", 260);
            AddParameter(command, "@assessment", DbType.DateTime2, _assessmentTimeUtc);
            AddParameter(command, "@cutoff", DbType.DateTime2, _assessmentTimeUtc.AddYears(-1));
            AddParameter(command, "@subscription", DbType.String, MicrosoftTeamSubscription.Name, 256);
            if (packageIds != null)
            {
                for (var i = 0; i < 10; i++)
                {
                    AddParameter(command, "@package" + i, DbType.String, i < packageIds.Length ? (object)packageIds[i] : DBNull.Value, 128);
                }
            }
        }

        private static void AddParameter(DbCommand command, string name, DbType type, object value, int size = 0)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = type;
            parameter.Value = value;
            if (size > 0)
            {
                parameter.Size = size;
            }

            command.Parameters.Add(parameter);
        }

        internal static bool HasExactMicrosoftEmailDomain(string confirmedEmail)
        {
            if (string.IsNullOrEmpty(confirmedEmail) || confirmedEmail.Length > 256
                || confirmedEmail.Any(char.IsControl) || confirmedEmail != confirmedEmail.Trim())
            {
                return false;
            }

            try
            {
                var parsed = new MailAddress(confirmedEmail);
                // Reject display-name wrappers, comments, and parser normalization as well as
                // suffixes, subdomains, Unicode lookalikes, multiple addresses and trailing dots.
                return string.Equals(parsed.Address, confirmedEmail, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(parsed.Host, "microsoft.com", StringComparison.OrdinalIgnoreCase);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        internal static string EscapeLike(string value)
        {
            return value.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[");
        }

        private static bool IsNamespaceBase(string value)
        {
            // Reserve room for the namespace's trailing dot. The shared regex uses '$',
            // so reject control characters explicitly (including a final newline).
            return !string.IsNullOrEmpty(value) && value.Length <= 127
                && !value.Any(char.IsControl) && PackageIdValidator.IsValidPackageId(value);
        }

        private static bool TryReadArguments(string toolName, JObject arguments, out string[] packageIds)
        {
            packageIds = null;
            if (arguments == null)
            {
                return false;
            }

            switch (toolName)
            {
                case "get_request_account_facts":
                case "get_namespace_reservations":
                case "get_namespace_package_usage":
                    return arguments.Count == 0;
                case "get_package_details":
                    if (arguments.Count != 1 || !(arguments["packageIds"] is JArray ids) || ids.Count == 0 || ids.Count > 10)
                    {
                        return false;
                    }

                    var values = new List<string>();
                    foreach (var token in ids)
                    {
                        if (token.Type != JTokenType.String)
                        {
                            return false;
                        }

                        var id = (string)token;
                        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id.Any(char.IsControl) || id != id.Trim())
                        {
                            return false;
                        }

                        values.Add(id);
                    }

                    if (values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count)
                    {
                        return false;
                    }

                    packageIds = values.ToArray();
                    return true;
                default:
                    return false;
            }
        }

        private static JObject ReadAccount(DbDataReader reader)
        {
            var exists = (bool)reader["AccountExists"];
            var email = reader["EmailAddress"] as string;
            var status = reader["UserStatusKey"] is int value ? (int?)value : null;
            var organization = exists && (bool)reader["IsOrganization"];
            var result = new JObject
            {
                ["role"] = Convert.ToInt64(reader["Ordinal"], CultureInfo.InvariantCulture) == 0 ? "submitter" : "owner",
                ["accountExists"] = exists,
                ["username"] = PublicString(reader["Username"], 64),
                ["accountType"] = exists ? (organization ? "organization" : "user") : "unknown",
                ["deleted"] = exists ? new JValue((bool)reader["IsDeleted"]) : JValue.CreateNull(),
                ["locked"] = status == (int)UserStatus.Locked ? new JValue(true)
                    : status == (int)UserStatus.Unlocked ? new JValue(false) : JValue.CreateNull(),
                ["confirmed"] = exists ? new JValue(!string.IsNullOrEmpty(email)) : JValue.CreateNull(),
                ["hasConfirmedMicrosoftEmail"] = HasExactMicrosoftEmailDomain(email),
                ["adminMembership"] = exists ? new JValue((bool)reader["AdminMembership"]) : JValue.CreateNull(),
                ["isSubmitter"] = (bool)reader["IsSubmitter"],
                ["microsoftPolicySubscriptionObserved"] = exists
                    ? new JValue((bool)reader["MicrosoftPolicySubscriptionObserved"]) : JValue.CreateNull(),
                ["securityOnboardingStatus"] = "unknown"
            };
            return result;
        }

        private static JObject ReadFields(DbDataReader reader, IEnumerable<string> fields)
        {
            var result = new JObject();
            foreach (var field in fields)
            {
                var value = reader[field];
                if (value is string)
                {
                    result[field] = PublicString(value, 128);
                }
                else if (value is DateTime date)
                {
                    result[field] = UtcString(DateTime.SpecifyKind(date, DateTimeKind.Utc));
                }
                else
                {
                    result[field] = value == DBNull.Value ? JValue.CreateNull() : new JValue(value);
                }
            }

            return result;
        }

        private static JToken PublicString(object value, int maximumLength)
        {
            if (value == DBNull.Value)
            {
                return JValue.CreateNull();
            }

            var text = (string)value;
            if (text == null || text.Length > maximumLength || text.Any(char.IsControl))
            {
                throw new InvalidOperationException("Invalid public evidence value.");
            }

            return new JValue(text);
        }

        private JObject AccountResult(List<JObject> rows)
        {
            if (rows.Count != _ownerKeys.Length + 1 || (string)rows[0]["role"] != "submitter")
            {
                return Failure("incomplete_account_projection");
            }

            var accountStateKnown = rows.All(a => (bool)a["accountExists"] && a["locked"].Type == JTokenType.Boolean);
            var submitter = rows[0];
            var eligible = accountStateKnown && rows.All(a => !(bool)a["deleted"] && !(bool)a["locked"] && (bool)a["confirmed"])
                && (string)submitter["accountType"] == "user"
                && rows.Skip(1).All(a => (string)a["accountType"] == "organization"
                    ? (bool)a["adminMembership"] : (bool)a["isSubmitter"]);
            var result = Envelope(true);
            result["eligible"] = eligible;
            result["eligibilityScope"] = "account_state_and_submitter_authority_only";
            result["submitter"] = submitter;
            result["owners"] = new JArray(rows.Skip(1));
            AddBlocker(result, "security_onboarding_not_established_by_subscription");
            if (!eligible)
            {
                AddBlocker(result, "account_state_or_submitter_authority_not_satisfied");
            }

            return result;
        }

        private JObject ReservationResult(List<JObject> rows)
        {
            if (rows.Count == 0)
            {
                return Failure("missing_reservation_aggregate");
            }

            var samples = new JArray(rows.Where(r => r["namespaceValue"].Type != JTokenType.Null)
                .Select(r => CopyFields(r, NamespaceReservationSqlQueries.ReservationFields)));
            var count = (long)rows[0]["overlapCount"];
            var result = Envelope(true);
            result["aggregatesComplete"] = true;
            result["counts"] = CopyFields(rows[0], NamespaceReservationSqlQueries.ReservationCounts);
            result["samples"] = samples;
            result["sampleComplete"] = count == samples.Count;
            result["requiresReview"] = count != 0;
            result["matchingSemantics"] = "raw_prefix_including_non_dotted_prefixes";
            if (count != 0)
            {
                AddBlocker(result, "existing_namespace_overlap");
            }

            return result;
        }

        private JObject UsageResult(List<JObject> rows)
        {
            if (rows.Count == 0)
            {
                throw new InvalidOperationException("Missing usage aggregate.");
            }

            var samples = new JArray(rows.Where(r => r["sample_packageId"].Type != JTokenType.Null)
                .Select(r => CopyFields(r, NamespaceReservationSqlQueries.PackageFields, "sample_")));
            foreach (JObject sample in samples)
            {
                sample["complete"] = true;
                AddPackageUncertainty(sample);
            }

            var result = Envelope(true);
            result["aggregatesComplete"] = true;
            result["counts"] = CopyFields(rows[0], NamespaceReservationSqlQueries.UsageCounts);
            result["samples"] = samples;
            result["sampleComplete"] = (long)rows[0]["packageCount"] == samples.Count;
            result["coverage"] = "exact_base_or_base_followed_by_dot";
            result["packageCountSemantics"] = "current_persisted_package_registrations_not_historical_existence";
            result["thirdPartyOnlyCountSemantics"] = "per_package_id_with_owners_but_no_requested_owner; coowned_ids_are_separate; "
                + "unlisted_requires_available_versions_and_no_listed_available_versions_or_uncertain_statuses; "
                + "uncertain_means_no_available_versions_or_any_deleted_validating_failed_or_unknown_status; listed_and_uncertain_may_overlap";
            result["recentStoredPublicationAndListedSemantics"] = "at_least_one_currently_listed_available_version_and_at_least_one_available_version_with_stored_Published_in_cutoff_to_assessment_window; "
                + "these_may_be_different_versions; not_verified_publication_activity_or_first_publication";
            AddPackageUncertainty(result);
            return result;
        }

        private JObject DetailsResult(List<JObject> rows, string[] packageIds)
        {
            var returnedIds = new HashSet<string>(rows.Select(r => (string)r["packageId"]), StringComparer.OrdinalIgnoreCase);
            if (returnedIds.Count != rows.Count || returnedIds.Except(packageIds, StringComparer.OrdinalIgnoreCase).Any())
            {
                return Failure("unexpected_package_projection");
            }

            var complete = returnedIds.SetEquals(packageIds);
            var result = Envelope(complete);
            result["requestedPackageCount"] = packageIds.Length;
            result["returnedPackageCount"] = rows.Count;
            result["sampleComplete"] = complete;
            result["aggregatesComplete"] = complete;
            foreach (var row in rows)
            {
                row["complete"] = true;
                AddPackageUncertainty(row);
            }

            result["packages"] = new JArray(rows);
            AddPackageUncertainty(result);
            if (!complete)
            {
                AddBlocker(result, "previously_observed_package_missing");
            }

            return result;
        }

        private static void AddPackageUncertainty(JObject value)
        {
            value["firstPublicationUtc"] = JValue.CreateNull();
            value["latestFirstPublicationUtc"] = JValue.CreateNull();
            value["allReleasesOlderThanCutoff"] = JValue.CreateNull();
            value["publicationHistoryStatus"] = "unknown";
            value["publicationHistoryExplanation"] = "Optional historical evidence is unknown: stored Created/Published timestamps are not immutable first-publication dates, "
                + "and hard-deleted release history is unavailable. This does not invalidate current ownership, listing, or zero-current-package facts; "
                + "it cannot establish historical inactivity or that packages never existed.";
            if (value["blockers"] == null)
            {
                value["blockers"] = new JArray();
            }
        }

        private static JObject CopyFields(JObject source, IEnumerable<string> fields, string prefix = "")
        {
            var result = new JObject();
            foreach (var field in fields)
            {
                result[field] = source[prefix + field].DeepClone();
            }

            return result;
        }

        private JObject Envelope(bool complete)
        {
            return new JObject
            {
                ["complete"] = complete,
                ["completenessScope"] = "sql_fact_read_only_not_all_policy_checks; aggregates_independent_of_sample_truncation_and_optional_publication_history",
                ["assessmentTimeUtc"] = UtcString(_assessmentTimeUtc),
                ["cutoffUtc"] = UtcString(_assessmentTimeUtc.AddYears(-1)),
                ["observationConsistency"] = "no_cross_tool_snapshot",
                ["blockers"] = new JArray()
            };
        }

        private JObject Failure(string code)
        {
            var result = Envelope(false);
            result["error"] = code;
            result["eligible"] = false;
            result["requiresReview"] = true;
            AddBlocker(result, code);
            return result;
        }

        private static void AddBlocker(JObject result, string code)
        {
            if (!(result["blockers"] is JArray blockers))
            {
                blockers = new JArray();
                result["blockers"] = blockers;
            }

            blockers.Add(code);
        }

        private static string UtcString(DateTime value)
        {
            return value.ToString("O", CultureInfo.InvariantCulture);
        }
    }
}