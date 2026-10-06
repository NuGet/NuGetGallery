// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    /// <summary>
    /// A structurally validated model recommendation, not authorization to reserve a namespace.
    /// Text remains untrusted and must be encoded if displayed. No database state is changed here.
    /// </summary>
    public class NamespaceReservationAssessment
    {
        public string Status { get; }
        public string ReasonCode { get; }
        public string Reason { get; }
        public string Rationale { get; }
        public IReadOnlyList<string> PolicyReferences { get; }
        public IReadOnlyList<string> EvidenceReferences { get; }
        public IReadOnlyList<string> MissingInformation { get; }
        public long? InputTokens { get; }
        public long? OutputTokens { get; }

        internal NamespaceReservationAssessment(
            string status,
            string reasonCode,
            string reason,
            string rationale,
            string[] policyReferences,
            string[] evidenceReferences,
            string[] missingInformation,
            long? inputTokens,
            long? outputTokens)
        {
            Status = status;
            ReasonCode = reasonCode;
            Reason = reason;
            Rationale = rationale;
            PolicyReferences = System.Array.AsReadOnly(policyReferences);
            EvidenceReferences = System.Array.AsReadOnly(evidenceReferences);
            MissingInformation = System.Array.AsReadOnly(missingInformation);
            InputTokens = inputTokens;
            OutputTokens = outputTokens;
        }
    }
}