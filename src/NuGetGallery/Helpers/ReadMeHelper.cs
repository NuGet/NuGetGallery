// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Text;
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
        internal const int MaxReadMeLengthBytes = 8000;
        private const string UriHostRequirement = "raw.githubusercontent.com";
        private static readonly TimeSpan UrlTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Returns if posted package form contains readme.md source data.
        /// 
        /// Note the following differences between source types:
        /// * 'TypeUrl' validates the URL string, allowing empty or whitespace-only files.
        /// * 'TypeFile' validates file length, allowing whitespace-only (but not empty) files.
        /// * 'TypeWritten' validates string content, NOT allowing empty or whitespace-only files.
        /// </summary>
        /// <param name="readMeRequest">Request associated with package edit that can post readme.md source data.</param>
        /// <returns>True if the request contains readme.md source data, false otherwise.</returns>
        public static bool HasReadMe(ReadMeRequest readMeRequest)
        {
            var readMeType = readMeRequest?.ReadMeSourceType;
            if (TypeUrl.Equals(readMeType, StringComparison.InvariantCultureIgnoreCase))
            {
                var readMeUrl = readMeRequest.SourceUrl;
                return !string.IsNullOrWhiteSpace(readMeUrl) && Uri.IsWellFormedUriString(readMeUrl, UriKind.Absolute);
            }
            else if (TypeWritten.Equals(readMeType, StringComparison.InvariantCultureIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(readMeRequest.SourceText);
            }
            else if (TypeFile.Equals(readMeType, StringComparison.InvariantCultureIgnoreCase))
            {
                var readMeFile = readMeRequest.SourceFile;
                return readMeFile != null && readMeFile.ContentLength > 0;
            }

            return false;
        }
        
        public static string GetReadMeHtml(string markdown)
        {
            var encodedMarkdown = HttpUtility.HtmlEncode(markdown);
            return CommonMarkConverter.Convert(encodedMarkdown);
        }
        
        public static async Task<string> GetReadMeHtmlAsync(ReadMeRequest readMeRequest)
        {
            using (var markdownStream = await GetReadMeMarkdownStream(readMeRequest))
            {
                return GetReadMeHtml(await markdownStream.ReadToEndAsync());
            }
        }

        /// <summary>
        /// Gets the readme.md source data from a request.
        /// </summary>
        /// <param name="readMeRequest">Request containing readme.md source data.</param>
        /// <returns>Stream containing the readme.md data.</returns>
        public static async Task<Stream> GetReadMeMarkdownStream(ReadMeRequest readMeRequest)
        {
            Stream readMeStream;
            var readMeType = readMeRequest?.ReadMeSourceType;
            if (TypeUrl.Equals(readMeType, StringComparison.InvariantCultureIgnoreCase))
            {
                readMeStream = await GetReadMeStreamFromUrl(readMeRequest.SourceUrl);
            }
            else if (TypeWritten.Equals(readMeType, StringComparison.InvariantCultureIgnoreCase))
            {
                readMeStream = new MemoryStream(Encoding.UTF8.GetBytes(readMeRequest.SourceText));
            }
            else if (TypeFile.Equals(readMeType, StringComparison.InvariantCultureIgnoreCase))
            {
                var uploadFileName = readMeRequest.SourceFile.FileName;
                if (!Path.GetExtension(uploadFileName).Equals(Constants.MarkdownFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"ReadMe file must end with extension '{Constants.MarkdownFileExtension}'.");
                }

                readMeStream = readMeRequest.SourceFile.InputStream;
            }
            else
            {
                throw new InvalidOperationException($"'{readMeType}' is not a valid ReadMeType.");
            }

            readMeStream = readMeStream.AsSeekableStream();
            if (readMeStream.Length >= MaxReadMeLengthBytes)
            {
                throw new InvalidOperationException($"ReadMe file must be less than '{MaxReadMeLengthBytes}' bytes.");
            }
            return readMeStream;
        }

        /// <summary>
        /// Gets the readme.md source data from a url.
        /// </summary>
        /// <param name="readMeRequest">Request containing readme.md source data.</param>
        /// <returns>Stream containing the readme.md data.</returns>
        private static async Task<Stream> GetReadMeStreamFromUrl(string readMeUrl)
        {
            var readMeUri = new Uri(readMeUrl);
            if (readMeUri.Host != UriHostRequirement)
            {
                throw new ArgumentException($"ReadMe URL must be a raw markdown file hosted on GitHub.");
            }

            using (var client = new HttpClient())
            {
                client.Timeout = UrlTimeout;
                return (await client.GetStreamAsync(readMeUri)).AsSeekableStream();
            };
        }
    }
}