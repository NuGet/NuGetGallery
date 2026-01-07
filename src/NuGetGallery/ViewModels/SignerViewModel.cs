// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public sealed class SignerViewModel
    {
        public string Username { get; }
        public string DisplayText { get; }
        public bool? HasCertificate { get; }

        public SignerViewModel(string username, string displayText, bool? hasCertificate = null)
        {
            Username = username;
            DisplayText = displayText;
            HasCertificate = hasCertificate;
        }
    }
}