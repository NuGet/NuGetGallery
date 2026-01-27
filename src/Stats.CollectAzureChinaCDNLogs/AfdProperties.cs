// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.CollectAzureChinaCDNLogs
{
    public class AfdProperties
    {
        public string HttpMethod { get; set; }
        public string HttpVersion { get; set; }
        public string RequestUri { get; set; }
        public string Sni { get; set; }
        public string ResponseBytes { get; set; }
        public string UserAgent { get; set; }
        public string ClientIp { get; set; }
        public string ClientPort { get; set; }
        public string SocketIp { get; set; }
        public string TimeToFirstByte { get; set; }
        public string TimeTaken { get; set; }
        public string RequestProtocol { get; set; }
        public string SecurityProtocol { get; set; }
        public string[] RulesEngineMatchNames { get; set; }
        public string HttpStatusCode { get; set; }
        public string EdgeActionsStatusCode { get; set; }
        public string RoxyConnectStatusCode { get; set; }
        public string HttpStatusDetails { get; set; }
        public string Pop { get; set; }
        public string CacheStatus { get; set; }
        public string ErrorInfo { get; set; }
        public string Result { get; set; }
        public string Endpoint { get; set; }
        public string RoutingRuleName { get; set; }
        public string ClientJA4FingerPrint { get; set; }
        public string HostName { get; set; }
        public string OriginUrl { get; set; }
        public string OriginIp { get; set; }
        public string OriginName { get; set; }
        public string OriginCryptProtocol { get; set; }
        public string OriginCryptCipher { get; set; }
        public string Referer { get; set; }
        public string ClientCountry { get; set; }
        public string Domain { get; set; }
        public string SecurityCipher { get; set; }
        public string SecurityCurves { get; set; }
    }
}
