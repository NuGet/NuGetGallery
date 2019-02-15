// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    // source: https://nvd.nist.gov/vuln
    public enum CveStatus
    {
        /// <summary>
        /// CVE has been recently published to the CVE dictionary and has been received by the NVD.
        /// </summary>
        Received = 0,

        /// <summary>
        /// CVE has been marked for Analysis. Normally once in this state the CVE will be analyzed by NVD staff within 24 hours.
        /// </summary>
        AwaitingAnalysis = 1,

        /// <summary>
        /// CVE is currently being analyzed by NVD staff. 
        /// This process results in association of reference link tags, CVSS scores, CWE association, and CPE applicability statements.
        /// </summary>
        UndergoingAnalysis = 2,

        /// <summary>
        /// CVE has had analysis completed and all data associations made.
        /// </summary>
        Analyzed = 3,

        /// <summary>
        /// CVE has been amended by a source (CVE Primary CNA or another CNA). 
        /// Analysis data supplied by the NVD may be no longer be accurate due to these changes.
        /// </summary>
        Modified = 4,

        /// <summary>
        /// When a CVE is given this status the NVD does not plan analyze or re-analyze this CVE due to resource or other concerns.
        /// </summary>
        Deferred = 5,

        /// <summary>
        /// CVE has been marked as "**REJECT**" in the CVE Dictionary. 
        /// These CVEs are in the NVD, but do not show up in search results.
        /// </summary>
        // source: https://cve.mitre.org/about/faqs.html#reject_signify_in_cve_entry
        Rejected = 6,

        /// <summary>
        /// A CVE Entry is marked as "RESERVED" when it has been reserved for use by a CVE Numbering Authority (CNA) or security researcher,
        /// but the details of it are not yet populated.
        /// A CVE Entry can change from the RESERVED state to being populated at any time based on a number of factors both internal and external to the CVE List.
        /// Once the CVE Entry is populated with details on the CVE List, it will become available in the U.S. National Vulnerability Database (NVD).
        /// </summary>
        // source: https://cve.mitre.org/about/faqs.html#reserved_signify_in_cve_entry
        Reserved = 7,

        /// <summary>
        /// When one party disagrees with another party's assertion that a particular issue in software is a vulnerability,
        /// a CVE Entry assigned to that issue may be designated as being "DISPUTED".
        /// In these cases, CVE is making no determination as to which party is correct.
        /// Instead, CVE makes note of this dispute and try to offer any public references that will better inform those trying to understand the facts of the issue.
        /// </summary>
        // source: https://cve.mitre.org/about/faqs.html#disputed_signify_in_cve_entry
        Disputed = 8,

        /// <summary>
        /// 
        /// </summary>
        Unverifiable = 9,

        /// <summary>
        /// Due to a CNA error, the CVE candidate was also originally assigned to another issue.
        /// The CVE description will provide details about which other CVEs to refer too.
        /// </summary>
        Split = 10
    }
}