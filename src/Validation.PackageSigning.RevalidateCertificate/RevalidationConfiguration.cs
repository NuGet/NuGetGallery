// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Validation.PackageSigning.RevalidateCertificate
{
    public class RevalidationConfiguration
    {
        /// <summary>
        /// The maximum number of package signatures that can be scanned for promotion.
        /// Each iteration of the job will scan signatures until it finds <see cref="SignaturePromotionBatchSize"/>
        /// signatures to promote.
        /// </summary>
        public int SignaturePromotionScanSize { get; set; }

        /// <summary>
        /// The maximum number of package signatures that can be promoted per iteration of the job.
        /// </summary>
        public int SignaturePromotionBatchSize { get; set; }

        /// <summary>
        /// The maximum number of certificates that can be revalidated per iteration of the job.
        /// </summary>
        public int CertificateRevalidationBatchSize { get; set; }

        /// <summary>
        /// How frequently a certificate may be revalidated.
        /// </summary>
        public TimeSpan RevalidationPeriodForCertificates { get; set; }

        /// <summary>
        /// How frequently the revalidate certificate job should check the status of in-flight certificate validations.
        /// </summary>
        public TimeSpan CertificateRevalidationPollTime { get; set; }

        /// <summary>
        /// How long the revalidation certificate job should wait for in-flight certificate validations before emitting
        /// a metric that the validations are taking too long.
        /// </summary>
        public TimeSpan CertificateRevalidationTrackAfter { get; set; }

        /// <summary>
        /// How long the revalidation certificate job should wait for in-flight certificate validations before timing out.
        /// </summary>
        public TimeSpan CertificateRevalidationTimeout { get; set; }
    }
}
