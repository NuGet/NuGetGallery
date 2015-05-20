// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace PublishTestDriverWebSite.Models
{
    public class AgreementModel
    {
        public string Agreement { get; set; }
        public string AgreementVersion { get; set; }
        public string Email { get; set; }
        public bool Accepted { get; set; }
        public DateTime DateAccepted { get; set; }

        public string Message { get; set; }
    }
}