// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.AzureCdnLogs.Common
{
    public class CdnLogEntry
    {
        /// <summary>
        /// The date and time (GMT) at which an edge server delivered the requested content to the client.
        /// </summary>
        public DateTime EdgeServerTimeDelivered { get; set; }

        /// <summary>
        /// The length of time, in milliseconds, that it took an edge server to process the requested action.
        /// This field does not take into account network time.
        /// </summary>
        public int? EdgeServerTimeTaken { get; set; }

        /// <summary>
        /// The IP address of the client that made the request to the server.
        /// </summary>
        public string ClientIpAddress { get; set; }

        /// <summary>
        /// The size of the requested asset in bytes.
        /// </summary>
        public long? FileSize { get; set; }

        /// <summary>
        /// The IP address associated with the edge server that processed the request.
        /// </summary>
        public string EdgeServerIpAddress { get; set; }

        /// <summary>
        /// The port number associated with the edge server that processed the request.
        /// </summary>
        public int? EdgeServerPort { get; set; }

        /// <summary>
        /// The cache status code returned by an edge server followed by the HTTP status code that originated from one of the following:
        /// <list type="bullet">
        /// <item><description>Origin server</description></item>
        /// <item><description>Origin Shield Only (Origin shield server)</description></item>
        /// <item><description>ADN Only (ADN gateway server)</description></item>
        /// <item><description>Edge server</description></item>
        /// </list>
        /// </summary>
        public string CacheStatusCode { get; set; }

        /// <summary>
        /// The number of bytes that the edge server sent to the client.
        /// </summary>
        public long? EdgeServerBytesSent { get; set; }

        /// <summary>
        /// The type of action that was requested.
        /// This field is reported according to the HTTP method (i.e., GET, HEAD, POST, PUT, and DELETE) that was used to make the request.
        /// </summary>
        public string HttpMethod { get; set; }

        /// <summary>
        /// The URL for the CDN content that was requested, posted, or deleted.
        /// </summary>
        public string RequestUrl { get; set; }

        /// <summary>
        /// The length of time, in milliseconds, that it took the origin server to process the requested action.
        /// This field does not take into account network time.
        /// </summary>
        public int? RemoteServerTimeTaken { get; set; }

        /// <summary>
        /// The total number of bytes read from a client and an origin server.
        /// </summary>
        public long? RemoteServerBytesSent { get; set; }

        /// <summary>
        /// The URL of the site from which the request originated.
        /// This field will typically be set to "-" for the HTTP Small and the ADN platforms.
        /// </summary>
        public string Referrer { get; set; }

        /// <summary>
        /// The user agent that the client used to perform CDN activity.
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// The customer account number through which the request was processed.
        /// </summary>
        public string CustomerId { get; set; }

        /// <summary>
        /// This is a custom field that can report request and response headers.
        /// This field will only appear in a raw log file when the Add this custom field to the log file option is marked.
        /// The type of headers that will be logged is determined by the rules that have been associated with each platform.
        /// </summary>
        public string CustomField { get; set; }
    }
}