// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface INamespaceReservationFoundryClient
    {
        bool IsEnabled { get; }

        /// <summary>
        /// Makes one bounded call with a synthetic prompt. Does not read or update namespace requests.
        /// Only call explicitly: this incurs model usage charges.
        /// </summary>
        Task<NamespaceReservationFoundryResponse> TestConnectionAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Sends the embedded skill and only the three submitted form fields in one bounded, paid call.
        /// Returns a validated recommendation only. Has no database access or evidence-collection tools.
        /// Technical failures throw; callers must leave the saved request Pending on failure.
        /// </summary>
        Task<NamespaceReservationAssessment> AssessAsync(NamespaceReservationAssessmentInput input, CancellationToken cancellationToken);

        /// <summary>
        /// Assesses with request-scoped, read-only evidence. The host enforces tool and time budgets;
        /// no database connection or credentials are supplied to the model.
        /// </summary>
        Task<NamespaceReservationAssessment> AssessAsync(NamespaceReservationAssessmentInput input, INamespaceReservationEvidenceSession evidence, CancellationToken cancellationToken);
    }
}