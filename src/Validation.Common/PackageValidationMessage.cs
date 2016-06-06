// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation.Common
{
    public class PackageValidationMessage
    {
        public Guid ValidationId { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public NuGetPackage Package { get; set; }
        
        public string MessageId { get; set; }
        public DateTimeOffset? InsertionTime { get; set; }
        public string PopReceipt { get; set; }
        public int DequeueCount { get; set; }
    }
}