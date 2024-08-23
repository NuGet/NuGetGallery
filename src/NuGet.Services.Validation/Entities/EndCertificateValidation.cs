// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The validation performed for a given certificate.
    /// </summary>
    public class EndCertificateValidation
    {
        /// <summary>
        /// The database-mastered identifier for this certificate.
        /// </summary>
        public long Key { get; set; }

        /// <summary>
        /// The key to the <see cref="EndCertificate"/> this validation is for.
        /// </summary>
        public long EndCertificateKey { get; set; }

        /// <summary>
        /// The unique identifier that represents the round of validation that kicked off
        /// this certificate's validation. Note that this ID may not necessarily be generated
        /// by the Validation Orchestrator!
        /// </summary>
        public Guid ValidationId { get; set; }

        /// <summary>
        /// The determined <see cref="EndCertificate"/> Status after the validation finished. NULL
        /// if the validation has not completed yet.
        /// </summary>
        public EndCertificateStatus? Status { get; set; }

        /// <summary>
        /// The <see cref="EndCertificate"/> this validation is for.
        /// </summary>
        public EndCertificate EndCertificate { get; set; }

        /// <summary>
        /// Check whether the validation is complete or not.
        /// </summary>
        /// <returns>True if the validation for the certificate has finished.</returns>
        public bool IsFinished() => Status.HasValue;
    }
}
