// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class ImageDomainValidator: IImageDomainValidator
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMinutes(1);
        private static readonly Regex GithubBadgeUrlRegEx = new Regex("^(https|http):\\/\\/github\\.com\\/[^/]+\\/[^/]+(\\/actions)?\\/workflows\\/.*badge\\.svg", RegexOptions.IgnoreCase, RegexTimeout);

        private readonly IContentObjectService _contentObjectService;
        public ImageDomainValidator (IContentObjectService contentObjectService)
        {
            _contentObjectService = contentObjectService ?? throw new ArgumentNullException(nameof(contentObjectService));
        }

        public bool TryPrepareImageUrlForRendering(string uriString, out string readyUriString)
        {
            if (uriString == null)
            {
                throw new ArgumentNullException(nameof(uriString));
            }

            Uri returnUri = null;
            readyUriString = null;

            if (Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
            {
                if (uri.IsHttpProtocol())
                {
                    if (IsTrustedImageDomain(uri))
                    {
                        returnUri = uri.ToHttps();
                    }
                }
                else if (uri.IsHttpsProtocol() && IsTrustedImageDomain(uri))
                {
                    returnUri = uri;
                }
            }

            if (returnUri != null)
            {
                readyUriString = returnUri.AbsoluteUri;
                return true;
            }

            return false;
        }

        private bool IsTrustedImageDomain(Uri uri)
        {
            return _contentObjectService.TrustedImageDomains.IsImageDomainTrusted(uri.Host) ||
                IsGitHubBadge(uri);
        }

        private bool IsGitHubBadge(Uri uri)
        {
            try
            {
                 return GithubBadgeUrlRegEx.IsMatch(uri.OriginalString);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
}