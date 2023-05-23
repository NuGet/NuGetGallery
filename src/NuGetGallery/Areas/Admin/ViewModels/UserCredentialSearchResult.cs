// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class UserCredentialSearchResult
    {
        public List<string> emailList {  get; set; }

        public string Username { get; set; }

        public string EmailAddress { get; set; }

        public UserCredential Credential { get; set; }

        public bool IsAADorMACredential { get; set; }
    }
}