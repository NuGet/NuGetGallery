// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using CommonMark;

namespace NuGetGallery.Helpers
{
    internal static class ReadMeHelper
    {
        internal const string TypeUrl = "Url";
        internal const string TypeFile = "File";
        internal const string TypeWritten = "Written";
        internal const int MaxMdLengthBytes = 8000;
        private const string UrlHostRequirement = "raw.githubusercontent.com";
        private static readonly TimeSpan UrlTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Determine if a readMeRequest is populated with source input.
        /// </summary>
        /// <param name="readMeRequest">ReadMeRequest object.</param>
        /// <returns>True if readMe input source is provided, false otherwise.</returns>
        public static bool HasReadMeSource(ReadMeRequest readMeRequest)
        {
            var readMeType = readMeRequest?.SourceType;

            if (TypeWritten.Equals(readMeType, StringComparison.InvariantCultureIgnoreCase))
            {
                // Text (markdown) provided.
                return !string.IsNullOrWhiteSpace(readMeRequest.SourceText);
            }
            else if (TypeUrl.Equals(readMeType, StringComparison.InvariantCultureIgnoreCase))
            {
                // URL provided.
                return !string.IsNullOrWhiteSpace(readMeRequest.SourceUrl);
            }
            else if (TypeFile.Equals(readMeType, StringComparison.InvariantCultureIgnoreCase))
            {
                // File upload provided.
                var readMeFile = readMeRequest.SourceFile;
                return readMeFile != null && readMeFile.ContentLength > 0;
            }

            return false;
        }

        /// <summary>
        /// Get converted HTML for readme.md string content.
        /// </summary>
        /// <param name="readMeMd">ReadMe.md content.</param>
        /// <returns>HTML content.</returns>
        public static string GetReadMeHtml(string readMeMd)
        {
            var encodedMarkdown = HttpUtility.HtmlEncode(readMeMd);
            return CommonMarkConverter.Convert(encodedMarkdown);
        }
        
        /// <summary>
        /// Get converted HTML for readme.md content from a ReadMeRequest object.
        /// </summary>
        /// <param name="readMeRequest">ReadMe request from Verify or Edit package page.</param>
        /// <returns>HTML content.</returns>
        public static async Task<string> GetReadMeHtmlAsync(ReadMeRequest readMeRequest)
        {
            var markdown = await GetReadMeMdAsync(readMeRequest);
            return GetReadMeHtml(markdown);
        }

        /// <summary>
        /// Get readme.md content from a ReadMeRequest object.
        /// </summary>
        /// <param name="readMeRequest">ReadMe request from Verify or Edit package page.</param>
        /// <returns>Markdown content.</returns>
        public static async Task<string> GetReadMeMdAsync(ReadMeRequest readMeRequest)
        {
            var readMeType = readMeRequest?.SourceType;

            if (TypeWritten.Equals(readMeType, StringComparison.InvariantCultureIgnoreCase))
            {
                if (readMeRequest.SourceText.Length > MaxMdLengthBytes)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                        Strings.ReadMeMaxLengthExceeded, MaxMdLengthBytes));
                }
                return readMeRequest.SourceText;
            }
            else if (TypeUrl.Equals(readMeType, StringComparison.InvariantCultureIgnoreCase))
            {
                return await GetReadMeMdFromUrlAsync(readMeRequest.SourceUrl);
            }
            else if (TypeFile.Equals(readMeType, StringComparison.InvariantCultureIgnoreCase))
            {
                return await GetReadMeMdFromPostedFileAsync(readMeRequest.SourceFile);
            }
            else
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    Strings.ReadMeInvalidSourceType, readMeType));
            }
        }

        /// <summary>
        /// Get readme.md content from a posted file.
        /// </summary>
        /// <param name="readMeMdPostedFile">Posted readme.md file.</param>
        /// <returns>Markdown content.</returns>
        private static async Task<string> GetReadMeMdFromPostedFileAsync(HttpPostedFileBase readMeMdPostedFile)
        {
            if (!Path.GetExtension(readMeMdPostedFile.FileName).Equals(Constants.MarkdownFileExtension, StringComparison.InvariantCulture))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    Strings.ReadMePostedFileExtensionInvalid, Constants.MarkdownFileExtension));
            }

            if (readMeMdPostedFile.ContentLength > MaxMdLengthBytes)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    Strings.ReadMeMaxLengthExceeded, MaxMdLengthBytes));
            }

            using (var readMeMdStream = readMeMdPostedFile.InputStream)
            {
                using (var reader = new StreamReader(readMeMdStream))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        /// <summary>
        /// Get readme.md content from a url.
        /// </summary>
        /// <param name="readMeMdUrl">ReadMe.md URL, which must be a raw github file.</param>
        /// <returns>Markdown content.</returns>
        private static async Task<string> GetReadMeMdFromUrlAsync(string readMeMdUrl)
        {
            if (!Uri.IsWellFormedUriString(readMeMdUrl, UriKind.Absolute) || new Uri(readMeMdUrl).Host != UrlHostRequirement)
            {
                throw new ArgumentException(Strings.ReadMeUrlHostInvalid, nameof(readMeMdUrl));
            }

            using (var client = new HttpClient() { Timeout = UrlTimeout, MaxResponseContentBufferSize = MaxMdLengthBytes })
            {
                using (var readMeMdStream = await client.GetStreamAsync(readMeMdUrl))
                {
                    using (var reader = new StreamReader(readMeMdStream))
                    {
                        return await reader.ReadToEndAsync();
                    }
                }
            };
        }
    }
}