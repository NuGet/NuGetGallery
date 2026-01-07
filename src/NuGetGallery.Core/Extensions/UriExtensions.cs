// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Extensions
{
    public static class UriExtensions
    {
        /// <summary>
        /// Appends the given SAS token to the Uri, ensuring the query string is correctly formatted.
        /// </summary>
        /// <param name="uri">The base Uri to which the query string will be appended.</param>
        /// <param name="sas">The SAS string to append, which may or may not start with a '?' character.</param>
        /// <returns>A new Uri with the SAS string appended.</returns>
        public static Uri BlobStorageAppendSas(this Uri uri, string sas)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (string.IsNullOrEmpty(sas))
            {
                throw new ArgumentNullException(nameof(sas));
            }

            // Trim any leading '?' from the query string to avoid double '?'
            string trimmedQueryString = sas.TrimStart('?');

            var uriBuilder = new UriBuilder(uri)
            {
                Query = trimmedQueryString
            };

            return uriBuilder.Uri;
        }
    }
}
