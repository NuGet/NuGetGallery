// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class ManagePackagesSerializableSignerViewModel
    {
        public ManagePackagesSerializableSignerViewModel(SignerViewModel signer)
        {
            Username = signer.Username;
            OptionText = signer.DisplayText;
            HasCertificate = signer.HasCertificate;
        }

        public string Username { get; }
        public string OptionText { get; }
        public bool? HasCertificate { get; }
    }
}