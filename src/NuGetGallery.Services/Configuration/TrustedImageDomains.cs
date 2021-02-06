// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NuGetGallery.Services
{
    public class TrustedImageDomains : ITrustedImageDomains
    {
        public HashSet<String> TrustedImageDomainList { get; }

        public TrustedImageDomains()
            : this(trustedImageDomainList: Enumerable.Empty<String>())
        {

        }

        [JsonConstructor]
        public TrustedImageDomains(IEnumerable<string> trustedImageDomainList)
        {
            if (trustedImageDomainList == null)
            {
                throw new ArgumentNullException(nameof(trustedImageDomainList));
            }

            TrustedImageDomainList = new HashSet<String>(trustedImageDomainList, StringComparer.OrdinalIgnoreCase);
        }

        public bool IsImageDomainTrusted(String imageDomain)
        {
            if (imageDomain == null)
            {
                return false;
            }

            return TrustedImageDomainList.Contains(imageDomain);
        }
    }
}
