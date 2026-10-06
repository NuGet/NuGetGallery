// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    internal static class NamespaceReservationAssessmentContract
    {
        internal const string SkillResourceName = "NuGetGallery.NamespaceReservation.Skill.md";
        internal const int MaxInputCharacters = 64000;
        private const int MaxItems = 64;
        internal static readonly string[] SubmissionReferences = { "namespace", "owner", "justification" };
        private static readonly Lazy<string> _skill = new Lazy<string>(LoadSkill);
        private static readonly string[] _fields =
        {
            "status", "reasonCode", "reason", "rationale",
            "policyReferences", "evidenceReferences", "missingInformation"
        };
        private static readonly string[] _statuses = { "Accepted", "Rejected" };
        private static readonly string[] _reasonCodes =
        {
            "criteria_met", "criteria_not_met", "customer_information_required",
            "evidence_unavailable", "policy_ambiguity", "review_required"
        };

        internal static string Skill => _skill.Value;

        internal static string[] PolicyReferences => Regex.Matches(Skill, @"^\*\*([A-Z][A-Z-]+):\*\*", RegexOptions.Multiline)
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        private static string LoadSkill()
        {
            using (var stream = typeof(NamespaceReservationAssessmentContract).Assembly.GetManifestResourceStream(SkillResourceName))
            {
                return ReadSkill(stream);
            }
        }

        internal static string ReadSkill(Stream stream)
        {
            if (stream == null)
            {
                throw new InvalidOperationException("The embedded namespace reservation skill is missing.");
            }

            using (var reader = new StreamReader(stream))
            {
                var text = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(text) || text.Length > MaxInputCharacters
                    || !text.StartsWith("---", StringComparison.Ordinal)
                    || !text.Contains("name: namespace-reservation")
                    || !text.Contains("# Namespace reservation assessment")
                    || !text.Contains("## Response contract")
                    || !text.Contains("**IDENTITY:**"))
                {
                    throw new InvalidOperationException("The embedded namespace reservation skill is invalid.");
                }

                return text;
            }
        }

        internal static string SerializeInput(NamespaceReservationAssessmentInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            // Explicit allowlist: never serialize caller types, EF entities, or database-derived facts.
            if (!IsText(input.Namespace, NamespaceReservationRequestInput.MaxNamespaceLength)
                || !PackageIdValidator.IsValidPackageId(input.Namespace)
                || !IsText(input.Owner, NamespaceReservationRequestInput.MaxOwnerLength)
                || !IsText(input.Justification, NamespaceReservationRequestInput.MaxJustificationLength))
            {
                throw new ArgumentException("The assessment requires a namespace, owner, and justification within the form limits.", nameof(input));
            }

            var json = new JObject
            {
                ["namespace"] = input.Namespace,
                ["owner"] = input.Owner,
                ["justification"] = input.Justification
            }.ToString(Formatting.None);

            if (json.Length > MaxInputCharacters)
            {
                throw new ArgumentException("The host assessment input exceeds the POC transport limit.", nameof(input));
            }

            return json;
        }

        internal static JObject CreateResponseFormat(IEnumerable<string> deliveredEvidence = null)
        {
            // Use only the supported Azure JSON Schema subset; enforce lengths and cross-field rules below.
            return new JObject
            {
                ["type"] = "json_schema",
                ["name"] = "namespace_reservation_assessment",
                ["strict"] = true,
                ["schema"] = new JObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JArray(_fields),
                    ["properties"] = new JObject
                    {
                        ["status"] = new JObject { ["type"] = "string", ["enum"] = new JArray(_statuses) },
                        ["reasonCode"] = new JObject { ["type"] = "string", ["enum"] = new JArray(_reasonCodes) },
                        ["reason"] = new JObject { ["type"] = "string" },
                        ["rationale"] = new JObject { ["type"] = "string" },
                        ["policyReferences"] = new JObject
                        {
                            ["type"] = "array",
                            ["items"] = new JObject { ["type"] = "string", ["enum"] = new JArray(PolicyReferences) }
                        },
                        ["evidenceReferences"] = new JObject
                        {
                            ["type"] = "array",
                            ["items"] = new JObject { ["type"] = "string", ["enum"] = new JArray(SubmissionReferences.Concat(deliveredEvidence ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal)) }
                        },
                        ["missingInformation"] = StringArraySchema()
                    }
                }
            };
        }

        private static JObject StringArraySchema()
        {
            return new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "string" } };
        }

        internal static NamespaceReservationAssessment Parse(NamespaceReservationFoundryResponse response, IEnumerable<string> deliveredEvidence = null)
        {
            var json = ParseObject(response.Text);
            if (!_fields.OrderBy(x => x).SequenceEqual(json.Properties().Select(p => p.Name).OrderBy(x => x)))
            {
                throw InvalidResponse();
            }

            var modelStatus = ReadText(json["status"], 16);
            var reasonCode = ReadText(json["reasonCode"], 64);
            var reason = ReadText(json["reason"], 4000);
            var rationale = ReadText(json["rationale"], 4000);
            var policies = ReadArray(json["policyReferences"], 128);
            var evidence = ReadArray(json["evidenceReferences"], 128);
            var missing = ReadArray(json["missingInformation"], 4000);

            if (!_statuses.Contains(modelStatus) || !_reasonCodes.Contains(reasonCode)
                || (modelStatus == "Accepted") != (reasonCode == "criteria_met")
                || policies.Length == 0 || policies.Any(id => !PolicyReferences.Contains(id, StringComparer.Ordinal))
                || evidence.Any(id => !SubmissionReferences.Concat(deliveredEvidence ?? Array.Empty<string>()).Contains(id, StringComparer.Ordinal))
                || evidence.Length == 0
                || (modelStatus == "Accepted" && missing.Length != 0)
                || (reasonCode == "customer_information_required" && missing.Length == 0))
            {
                throw InvalidResponse();
            }

            // Keep the established persistence/allocation vocabulary internal. Foundry emits
            // only Accepted or Rejected; an accepted recommendation becomes Approved here.
            var status = modelStatus == "Accepted" ? "Approved" : "Rejected";
            return new NamespaceReservationAssessment(status, reasonCode, reason, rationale,
                policies, evidence, missing, response.InputTokens, response.OutputTokens);
        }

        internal static JObject ParseObject(string text)
        {
            try
            {
                // Json.NET accepts comments and trailing commas; reject those with the strict parser first.
                using (var document = System.Text.Json.JsonDocument.Parse(text, new System.Text.Json.JsonDocumentOptions { MaxDepth = 32 }))
                using (var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None, MaxDepth = 32 })
                {
                    if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                    {
                        throw InvalidResponse();
                    }

                    return JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Do not retain parser exceptions: paths/messages can contain model or customer content.
                throw InvalidResponse();
            }
            catch (JsonException)
            {
                throw InvalidResponse();
            }
        }

        internal static InvalidOperationException InvalidResponse()
        {
            return new InvalidOperationException("Foundry returned an invalid or incomplete namespace reservation response. No decision was accepted.");
        }

        private static string ReadText(JToken token, int maxLength)
        {
            if (token?.Type != JTokenType.String || !IsText((string)token, maxLength))
            {
                throw InvalidResponse();
            }

            return (string)token;
        }

        private static string[] ReadArray(JToken token, int maxLength)
        {
            if (!(token is JArray array) || array.Count > MaxItems)
            {
                throw InvalidResponse();
            }

            var items = array.Select(item => ReadText(item, maxLength)).ToArray();
            if (items.Distinct(StringComparer.Ordinal).Count() != items.Length)
            {
                throw InvalidResponse();
            }

            return items;
        }

        private static bool IsText(string value, int maxLength)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength;
        }
    }
}