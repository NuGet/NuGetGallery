// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace PublishTestDriverWebSite.Models
{
    public class HomeModel
    {
        public string ClientId { get; set; }
        public string AADInstance { get; set; }
        public string Tenant { get; set; }
        public string CertificateThumbprint { get; set; }
        public string CertificateSubject { get; set; }
    }
}