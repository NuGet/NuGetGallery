// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.AzureCdnLogs.Common.Collect
{
    /// <summary>
    /// The schema of the line in statistic logs. 
    /// Any log needs to have its lines formatted like this in order to be inserted in the stats db. 
    /// </summary>
    public class OutputLogLine
    {
        //timestamp time-taken c-ip filesize s-ip s-port sc-status sc-bytes cs-method cs-uri-stem - rs-duration rs-bytes c-referrer c-user-agent customer-id x-ec_custom-1\n");
        public string TimeStamp { get; }

        public string TimeTaken { get; }

        public string CIp { get; }

        public string FileSize { get; }

        public string SIp { get; }

        public string SPort { get; }

        public string ScStatus { get; }

        public string ScBytes { get; }

        public string CsMethod { get; }

        public string CsUriStem { get; }

        public string RsDuration { get; }

        public string RsBytes { get; }

        public string CReferrer { get; }

        public string CUserAgent { get; }

        public string CustomerId { get; }

        public string XEc_Custom_1 { get; }

        public OutputLogLine(string timestamp,
                             string timetaken,
                             string cip,
                             string filesize,
                             string sip,
                             string sport,
                             string scstatus,
                             string scbytes,
                             string csmethod,
                             string csuristem,
                             string rsduration,
                             string rsbytes,
                             string creferrer,
                             string cuseragent,
                             string customerid,
                             string xeccustom1)
        {
            TimeStamp = timestamp;
            TimeTaken = timetaken;
            CIp = cip;
            FileSize = filesize;
            SIp = sip;
            SPort = sport;
            ScStatus = scstatus;
            ScBytes = scbytes;
            CsMethod = csmethod;
            CsUriStem = csuristem;
            RsDuration = rsduration;
            RsBytes = rsbytes;
            CReferrer = creferrer;
            CUserAgent = cuseragent;
            CustomerId = customerid;
            XEc_Custom_1 = xeccustom1;
        }

        public static string Header
        {
            get { return "#Fields: timestamp time-taken c-ip filesize s-ip s-port sc-status sc-bytes cs-method cs-uri-stem - rs-duration rs-bytes c-referrer c-user-agent customer-id x-ec_custom-1\n"; }
        }

        public override string ToString()
        {
            return $"{TimeStamp} {TimeTaken} {CIp} {FileSize} {SIp} {SPort} {ScStatus} {ScBytes} {CsMethod} {CsUriStem} - {RsDuration} {RsBytes} {CReferrer} {Quote(CUserAgent)} {CustomerId} {Quote(XEc_Custom_1)}";
        }

        private static string Quote(string input)
        {
            if (input.StartsWith("\"") && input.EndsWith("\""))
            {
                // already quoted
                return input;
            }
            if (input.IndexOfAny(new[] { ' ', '\t' }) >= 0)
            {
                return $"\"{input}\"";
            }

            return input;
        }
    }
}
