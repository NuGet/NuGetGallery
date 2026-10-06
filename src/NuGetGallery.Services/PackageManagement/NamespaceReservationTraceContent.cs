// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGetGallery
{
    /// <summary>
    /// Explicit telemetry projections, not a serializer for arbitrary model/SQL objects. Free-text
    /// redaction is best effort, not a guarantee. Callers must omit content on any sanitization error.
    /// </summary>
    internal static class NamespaceReservationTraceContent
    {
        internal const int AttributeLimit = 8000;
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);
        private static readonly Regex Url = Pattern(@"\b(?:https?|ftp)://[^\s<>""']+");
        private static readonly Regex Secrets = Pattern(@"\bBearer\s+[^\s,;""']+|\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+|\b(?:sk-|gh[pousr]_|github_pat_)[A-Za-z0-9_-]+|\b[A-Za-z0-9+/=_-]{32,}\b");
        private static readonly Regex Assignments = Pattern(@"\b(?:api[-_ ]?key|token|password|pwd|secret|authorization|credential|connection\s*string|AccountKey|SharedAccessKey|SharedAccessSignature|InstrumentationKey|IngestionEndpoint|Server|Data\s+Source|User\s+Id|uid|Initial\s+Catalog|Database|submitterKey|requestKey|ownerKeys?|UserKey)\b\s*[""']?\s*[:=]\s*(?:""[^""]*""|'[^']*'|[^\s,;]+)");
        private static readonly Regex Email = Pattern(@"[\p{L}\p{N}.!#$%&'*+/=?^_`{|}~-]+@[\p{L}\p{N}.-]+(?:\.[\p{L}\p{N}-]+)+");
        private static readonly Regex Controls = Pattern(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]");
        private static readonly string[] EnvelopeFields =
        {
            "complete", "completenessScope", "assessmentTimeUtc", "cutoffUtc", "observationConsistency",
            "blockers", "error", "aggregatesComplete", "sampleComplete"
        };
        private static readonly string[] AccountFields =
        {
            "role", "accountExists", "username", "accountType", "deleted", "locked", "confirmed",
            "hasConfirmedMicrosoftEmail", "adminMembership", "isSubmitter",
            "microsoftPolicySubscriptionObserved", "securityOnboardingStatus"
        };
        private static readonly string[] UncertaintyFields =
        {
            "firstPublicationUtc", "latestFirstPublicationUtc", "allReleasesOlderThanCutoff",
            "publicationHistoryStatus", "publicationHistoryExplanation", "blockers", "complete"
        };
        // Exact application-owned values from NamespaceReservationSqlEvidenceSession. Do not
        // exempt a whole field or all snake_case strings: unknown values still require redaction.
        private static readonly IReadOnlyDictionary<string, string> KnownEvidenceMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "completenessScope", "sql_fact_read_only_not_all_policy_checks; aggregates_independent_of_sample_truncation_and_optional_publication_history" },
            { "eligibilityScope", "account_state_and_submitter_authority_only" },
            { "matchingSemantics", "raw_prefix_including_non_dotted_prefixes" },
            { "coverage", "exact_base_or_base_followed_by_dot" },
            { "packageCountSemantics", "current_persisted_package_registrations_not_historical_existence" },
            { "thirdPartyOnlyCountSemantics", "per_package_id_with_owners_but_no_requested_owner; coowned_ids_are_separate; "
                + "unlisted_requires_available_versions_and_no_listed_available_versions_or_uncertain_statuses; "
                + "uncertain_means_no_available_versions_or_any_deleted_validating_failed_or_unknown_status; listed_and_uncertain_may_overlap" },
            { "recentStoredPublicationAndListedSemantics", "at_least_one_currently_listed_available_version_and_at_least_one_available_version_with_stored_Published_in_cutoff_to_assessment_window; "
                + "these_may_be_different_versions; not_verified_publication_activity_or_first_publication" }
        };
        private static readonly HashSet<string> KnownEvidenceCodes = new HashSet<string>(StringComparer.Ordinal)
        {
            "security_onboarding_not_established_by_subscription",
            "account_state_or_submitter_authority_not_satisfied",
            "previously_observed_package_missing",
            "package_id_not_in_session_evidence"
        };

        private static Regex Pattern(string value)
        {
            return new Regex(value, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
        }

        internal static string Text(string value)
        {
            if (value == null)
            {
                return null;
            }

            // Do not truncate before scanning: that could leave a partial credential behind.
            if (value.Length > 64000)
            {
                throw new InvalidOperationException();
            }

            value = Url.Replace(value, "[REDACTED_URL]");
            value = Assignments.Replace(value, "[REDACTED]");
            value = Secrets.Replace(value, "[REDACTED]");
            value = Email.Replace(value, "[REDACTED_EMAIL]");
            return Controls.Replace(value, " ");
        }

        internal static string Identifier(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128)
            {
                return "unknown";
            }

            var safe = Text(value);
            return safe == value && Regex.IsMatch(safe, @"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,127}\z", RegexOptions.CultureInvariant, RegexTimeout)
                ? safe : "unknown";
        }

        internal static string ToolName(string value)
        {
            return NamespaceReservationFoundryClient.CoreTools.Contains(value, StringComparer.Ordinal) || value == "get_package_details"
                ? value : "unknown";
        }

        internal static JObject Form(NamespaceReservationAssessmentInput input)
        {
            return new JObject
            {
                ["namespace"] = Text(input.Namespace),
                ["owner"] = Text(input.Owner),
                ["justification"] = Text(input.Justification)
            };
        }

        internal static JObject Decision(NamespaceReservationAssessment result)
        {
            return new JObject
            {
                ["status"] = Text(result.Status), ["reasonCode"] = Text(result.ReasonCode),
                ["reason"] = Text(result.Reason), ["rationale"] = Text(result.Rationale),
                ["policyReferences"] = new JArray(result.PolicyReferences.Select(Text)),
                ["evidenceReferences"] = new JArray(result.EvidenceReferences.Select(Text)),
                ["missingInformation"] = new JArray(result.MissingInformation.Select(Text))
            };
        }

        internal static JObject Arguments(string name, JObject arguments)
        {
            // Repeat validation so this helper never accepts an accidentally raw argument object.
            if (!NamespaceReservationFoundryClient.TryReadToolArguments(name, arguments, out var validated))
            {
                return null;
            }

            return Project(validated, new[] { "packageIds" });
        }

        internal static JObject ToolResult(string name, JObject result)
        {
            var safe = Project(result, EnvelopeFields);
            switch (ToolName(name))
            {
                case "get_request_account_facts":
                    Copy(safe, result, new[] { "eligible", "eligibilityScope" });
                    safe["submitter"] = Project(result["submitter"] as JObject, AccountFields);
                    safe["owners"] = Rows(result["owners"], AccountFields);
                    break;
                case "get_namespace_reservations":
                    Copy(safe, result, new[] { "requiresReview", "matchingSemantics" });
                    safe["counts"] = Project(result["counts"] as JObject, NamespaceReservationSqlQueries.ReservationCounts);
                    safe["samples"] = Rows(result["samples"], NamespaceReservationSqlQueries.ReservationFields);
                    break;
                case "get_namespace_package_usage":
                    Copy(safe, result, new[] { "coverage", "packageCountSemantics", "thirdPartyOnlyCountSemantics", "recentStoredPublicationAndListedSemantics" }.Concat(UncertaintyFields));
                    safe["counts"] = Project(result["counts"] as JObject, NamespaceReservationSqlQueries.UsageCounts);
                    safe["samples"] = Rows(result["samples"], NamespaceReservationSqlQueries.PackageFields.Concat(UncertaintyFields));
                    break;
                case "get_package_details":
                    Copy(safe, result, new[] { "requestedPackageCount", "returnedPackageCount" }.Concat(UncertaintyFields));
                    safe["packages"] = Rows(result["packages"], NamespaceReservationSqlQueries.PackageFields.Concat(UncertaintyFields));
                    break;
            }

            return safe;
        }

        private static JArray Rows(JToken value, IEnumerable<string> fields)
        {
            var rows = new JArray();
            if (value is JArray array)
            {
                if (array.Count > 20)
                {
                    throw new InvalidOperationException();
                }

                foreach (var row in array.OfType<JObject>())
                {
                    rows.Add(Project(row, fields));
                }
            }

            return rows;
        }

        private static JObject Project(JObject source, IEnumerable<string> fields)
        {
            var result = new JObject();
            Copy(result, source, fields);
            return result;
        }

        private static void Copy(JObject target, JObject source, IEnumerable<string> fields)
        {
            if (source == null)
            {
                return;
            }

            foreach (var field in fields)
            {
                var value = source[field];
                // No recursive arbitrary objects, even beneath an allowed property. Numeric values
                // are accepted only for known counts; usernames/package IDs cannot conceal DB keys.
                if (value?.Type == JTokenType.String)
                {
                    target[field] = EvidenceText(field, (string)value);
                }
                else if (value?.Type == JTokenType.Boolean || value?.Type == JTokenType.Null
                    || (value?.Type == JTokenType.Integer && field.EndsWith("Count", StringComparison.Ordinal)))
                {
                    target[field] = value.DeepClone();
                }
                else if (value is JArray array && (field == "blockers" || field == "packageIds"))
                {
                    if (array.Count > 64)
                    {
                        throw new InvalidOperationException();
                    }

                    target[field] = new JArray(array.Where(item => item.Type == JTokenType.String).Values<string>().Select(item => EvidenceText(field, item)));
                }
            }
        }

        private static string EvidenceText(string field, string value)
        {
            if ((KnownEvidenceMetadata.TryGetValue(field, out var known) && string.Equals(value, known, StringComparison.Ordinal))
                || ((field == "blockers" || field == "error") && KnownEvidenceCodes.Contains(value)))
            {
                return value;
            }

            return Text(value);
        }

        internal static JArray Message(string role, JObject part)
        {
            // GenAI message attributes are JSON strings containing role/parts arrays, not Responses
            // request bodies. Only current input/feedback is included, never replay or reasoning.
            return new JArray(new JObject { ["role"] = role, ["parts"] = new JArray(part) });
        }

        internal static JObject TextPart(JObject value)
        {
            return new JObject { ["type"] = "text", ["content"] = value.ToString(Formatting.None) };
        }

        internal static string Bounded(JToken value, out bool truncated)
        {
            var json = value.ToString(Formatting.None);
            // UTF-8 can use three bytes per UTF-16 code unit. This leaves each attribute below
            // 8 KB even for non-ASCII content and keeps the three-attribute budget below 24 KB.
            truncated = json.Length >= AttributeLimit || System.Text.Encoding.UTF8.GetByteCount(json) >= AttributeLimit;
            if (truncated)
            {
                return value is JArray
                    ? "[{\"role\":\"system\",\"parts\":[{\"type\":\"text\",\"content\":\"[content omitted: size limit]\"}],\"truncated\":true}]"
                    : "{\"truncated\":true,\"content\":\"[content omitted: size limit]\"}";
            }

            return json;
        }
    }
}