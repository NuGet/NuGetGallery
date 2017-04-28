// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation.Common
{
    public enum ValidationEvent
    {
        /// <summary>
        /// Virus scan request is about to be sent
        /// </summary>
        BeforeVirusScanRequest,

        /// <summary>
        /// The validation queue item was deadlettered after several attempts of processing
        /// </summary>
        Deadlettered,

        /// <summary>
        /// The detail item passed with a <see cref="PackageNotClean"/> result
        /// </summary>
        NotCleanReason,

        /// <summary>
        /// Virus scan service reported package as clean
        /// </summary>
        PackageClean,

        /// <summary>
        /// Packages download was successful
        /// </summary>
        PackageDownloaded,

        /// <summary>
        /// Virus scan service reported package as not clean
        /// </summary>
        PackageNotClean,

        /// <summary>
        /// Virus scan service reported its failure to scan package (it does *not* mean package is not clean)
        /// </summary>
        ScanFailed,

        /// <summary>
        /// The detail item passed with <see cref="ScanFailed"/> result
        /// </summary>
        ScanFailureReason,

        /// <summary>
        /// An exception was thrown during validator execution
        /// </summary>
        ValidatorException,

        /// <summary>
        /// The virus scan request was submitted
        /// </summary>
        VirusScanRequestSent,

        /// <summary>
        /// Sending the virus scanning request had failed
        /// </summary>
        VirusScanRequestFailed,

        /// <summary>
        /// Package was successfully unzipped
        /// </summary>
        UnzipSucceeeded,
    }
}
