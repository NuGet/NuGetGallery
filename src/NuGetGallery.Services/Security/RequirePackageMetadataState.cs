// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGetGallery.Security
{
    public class RequirePackageMetadataState
    {
        [JsonProperty("u")]
        public string RequiredCoOwnerUsername { get; set; }

        [JsonProperty("copy")]
        public string[] AllowedCopyrightNotices { get; set; }

        [JsonProperty("authors")]
        public string[] AllowedAuthors { get; set; }

        [JsonProperty("licUrlReq")]
        public bool IsLicenseUrlRequired { get; set; }

        [JsonProperty("projUrlReq")]
        public bool IsProjectUrlRequired { get; set; }

        [JsonProperty("error")]
        public string ErrorMessageFormat { get; set; }
    }
}