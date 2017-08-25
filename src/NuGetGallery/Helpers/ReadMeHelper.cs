// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CommonMark;
using Ganss.XSS;

namespace NuGetGallery.Helpers
{
    internal static class ReadMeHelper
    {
        internal const string TypeUrl = "Url";
        internal const string TypeFile = "File";
        internal const string TypeWritten = "Written";
        internal const int MaxFileSize = 40000;
        private const int UrlTimeout = 10000;
        private const string UriHostRequirement = "raw.githubusercontent.com";

        private static Lazy<HtmlSanitizer> Sanitizer = new Lazy<HtmlSanitizer>(() => new HtmlSanitizer());

        /// <summary>
        /// Returns if posted package form contains a ReadMe.
        /// </summary>
        /// <param name="formData">A ReadMeRequest with the ReadMe data from the form.</param>
        /// <returns>Whether there is a ReadMe to upload.</returns>
        public static bool HasReadMe(ReadMeRequest formData)
        {
            switch (formData?.ReadMeType)
            {
                case TypeUrl:
                    var readMeUrl = formData.ReadMeUrl;
                    return !string.IsNullOrWhiteSpace(readMeUrl) && Uri.IsWellFormedUriString(readMeUrl, UriKind.Absolute);

                case TypeFile:
                    var readMeFile = formData.ReadMeFile;
                    return readMeFile != null && readMeFile.ContentLength > 0;

                case TypeWritten:
                    return !string.IsNullOrWhiteSpace(formData.ReadMeWritten);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Takes in a Stream representing a readme file in markdown, converts it to HTML and 
        /// returns a Stream representing the HTML version of the readme.
        /// </summary>
        /// <param name="readMeMarkdownStream">Stream containing a readMe in markdown</param>
        /// <returns>A stream with the HTML version of the readMe</returns>
        public static async Task<Stream> GetReadMeHtmlStream(Stream readMeMarkdownStream)
        {
            var markdown = await readMeMarkdownStream.ReadToEndAsync();
            var html = CommonMarkConverter.Convert(markdown);
            var sanitizedHtml = Sanitizer.Value.Sanitize(html).Trim();

            return new MemoryStream(Encoding.UTF8.GetBytes(sanitizedHtml));
        }

        /// <summary>
        /// Takes in a ReadMeRequest with a markdown ReadMe file, converts it to HTML
        /// and returns a stream with the data.
        /// </summary>
        /// <param name="readMeRequest">The readMe type and markdown file</param>
        /// <returns>A stream representing the ReadMe.html file</returns>
        public static async Task<Stream> GetReadMeHtmlStream(ReadMeRequest readMeRequest)
        {
            var markdownStream = await GetReadMeMarkdownStream(readMeRequest);

            return await GetReadMeHtmlStream(markdownStream);
        }

        /// <summary>
        /// Finds the highest priority ReadMe file stream and returns it. Highest priority is an uploaded file,
        /// then a repository URL inputted via the website, then a repository URL entered through the nuspec.
        /// </summary>
        /// <param name="readMeRequest">The current package's form data submitted through the verify page</param>
        /// <param name="packageMetadata">The package metadata from the nuspec file</param>
        /// <returns>A stream with the encoded ReadMe file</returns>
        public static async Task<Stream> GetReadMeMarkdownStream(ReadMeRequest readMeRequest)
        {
            Stream readMeStream;
            var readMeType = readMeRequest.ReadMeType;
            if (readMeType.Equals(TypeUrl, StringComparison.InvariantCultureIgnoreCase))
            {
                readMeStream = await GetReadMeStreamFromUrl(readMeRequest.ReadMeUrl);
            }
            else if (readMeType.Equals(TypeWritten, StringComparison.InvariantCultureIgnoreCase))
            {
                readMeStream = new MemoryStream(Encoding.UTF8.GetBytes(readMeRequest.ReadMeWritten));
            }
            else if (readMeType.Equals(TypeFile, StringComparison.InvariantCultureIgnoreCase))
            {
                readMeStream = readMeRequest.ReadMeFile.InputStream;
            }
            else
            {
                throw new InvalidOperationException("Form data contains an invalid ReadMeType.");
            }

            readMeStream = readMeStream.AsSeekableStream();
            if (readMeStream.Length >= MaxFileSize)
            {
                throw new ArgumentException("ReadMe file exceeds size limitations. (" + MaxFileSize + ")");
            }
            return readMeStream;
        }

        /// <summary>
        /// Converts a ReadMe's url to a file stream.
        /// </summary>
        /// <param name="readMeUrl">A link to the raw ReadMe markdown file</param>
        /// <returns>A stream to allow the file to be read</returns>
        private static async Task<Stream> GetReadMeStreamFromUrl(string readMeUrl)
        {
            var readMeUri = new Uri(readMeUrl);
            if (readMeUri.Host != UriHostRequirement)
            {
                throw new ArgumentException("Url must link to a raw markdown file hosted on Github. [" + UriHostRequirement + "]");
            }

            var webRequest = WebRequest.Create(readMeUrl);
            webRequest.Timeout = UrlTimeout;

            var response = await webRequest.GetResponseAsync();
            return response.GetResponseStream().AsSeekableStream();
        }
    }
}