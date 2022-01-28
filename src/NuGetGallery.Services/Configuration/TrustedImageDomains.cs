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
        public HashSet<string> TrustedImageDomainList { get; }
        public HashSet<string> ExpandedTrustedImageDomainList { get; }

        public TrustedImageDomains()
            : this(trustedImageDomainList: Enumerable.Empty<string>())
        {

        }

        [JsonConstructor]
        public TrustedImageDomains(IEnumerable<string> trustedImageDomainList)
        {
            if (trustedImageDomainList == null)
            {
                throw new ArgumentNullException(nameof(trustedImageDomainList));
            }

            TrustedImageDomainList = new HashSet<string>(trustedImageDomainList, StringComparer.OrdinalIgnoreCase);
            ExpandedTrustedImageDomainList = expandDomainList();
        }

        public bool IsImageDomainTrusted(string imageDomain)
        {
            if (imageDomain == null)
            {
                return false;
            }

            return ExpandedTrustedImageDomainList.Contains(imageDomain);
        }

        public HashSet<string> expandDomainList()
        {
            var expandedImageDomainList = new HashSet<string>();

            foreach (var imageDomain in TrustedImageDomainList)
            {
                expandedImageDomainList.Add(imageDomain);

                var subdomain = ParseSubDomain(imageDomain);

                if (string.IsNullOrEmpty(subdomain))
                {
                    expandedImageDomainList.Add("www." + imageDomain);
                } 
                else if (subdomain == "www")
                {
                    expandedImageDomainList.Add(imageDomain.Substring(subdomain.Length));
                }
            }
            return expandedImageDomainList;
        }

        private string ParseSubDomain(string domain)
        {
            if (domain.Split('.').Length > 2)
            {
                var lastIndex = domain.LastIndexOf(".");
                var index = domain.LastIndexOf('.', lastIndex - 1);

                return domain.Substring(0, index);
            }

            return null; 
        }
    }
}
