// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ListCertificatePatternItemViewModel
    {
        public ListCertificatePatternItemViewModel(UserCertificatePattern pattern, string deleteUrl)
        {
            PatternKey = pattern.Key;
            PatternType = pattern.PatternType;
            Identifier = pattern.Identifier;
            DeleteUrl = deleteUrl;
        }

        public int PatternKey { get; }
        public CertificatePatternType PatternType { get; }
        public string Identifier { get; }
        public string DeleteUrl { get; }
    }
}