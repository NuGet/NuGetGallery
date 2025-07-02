// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services.Models
{
    public class McpServerEntryTemplateResult
    {
        public McpServerEntryResultValidity Validity { get; set; }
        public string Template { get; set; }
    }

    public enum McpServerEntryResultValidity
    {
        Success,
        MissingMetadata,
        MissingNugetRegistry,
        InvalidMetadata,
        Unset,
    }
}
