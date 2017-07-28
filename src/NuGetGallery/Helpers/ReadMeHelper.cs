// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using CommonMark;
using Ganss.XSS;

namespace NuGetGallery.Helpers
{
    internal static class ReadMeHelper
    {
        private const string TypeUrl = "Url";
        private const string TypeFile = "File";
        private const string TypeWritten = "Written";
        private const string UriHostRequirement = "raw.githubusercontent.com";
        private const int UrlTimeout = 10000;
        private const int MaxFileSize = 40000;

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
                    return !string.IsNullOrWhiteSpace(formData.ReadMeUrl) && Uri.IsWellFormedUriString(readMeUrl, UriKind.Absolute);
                case TypeFile:
                    return formData.ReadMeFile != null;
                case TypeWritten:
                    return !string.IsNullOrWhiteSpace(formData.ReadMeWritten);
                default: return false;
            }
        }

        /// <summary>
        /// Takes in a string containing a markdown file and converts it to HTML.
        /// </summary>
        /// <param name="readMe">A string containing a markdown file's contents</param>
        /// <returns>A string containing the HTML version of the markdown</returns>
        private static string ConvertMarkDownToHtml(string readMe)
        {
            var html = CommonMarkConverter.Convert(readMe).Trim();
            return Sanitizer.Value.Sanitize(html);
        }

        /// <summary>
        /// Takes in a Stream representing a readme file in markdown, converts it to HTML and 
        /// returns a Stream representing the HTML version of the readme.
        /// </summary>
        /// <param name="readMeMarkdownStream">Stream containing a readMe in markdown</param>
        /// <returns>A stream with the HTML version of the readMe</returns>
        public static Stream GetReadMeHtmlStream(Stream readMeMarkdownStream)
        {
            var reader = new StreamReader(readMeMarkdownStream);
            var readMeHtml = ConvertMarkDownToHtml(reader.ReadToEnd());
            return GetStreamFromWritten(readMeHtml);
        }

        /// <summary>
        /// Takes in a ReadMeRequest with a markdown ReadMe file, converts it to HTML
        /// and returns a stream with the data.
        /// </summary>
        /// <param name="readMeRequest">The readMe type and markdown file</param>
        /// <returns>A stream representing the ReadMe.html file</returns>
        public static Stream GetReadMeHtmlStream(ReadMeRequest readMeRequest)
        {
            return GetReadMeHtmlStream(GetReadMeMarkdownStream(readMeRequest));
        }

        /// <summary>
        /// Finds the highest priority ReadMe file stream and returns it. Highest priority is an uploaded file,
        /// then a repository URL inputted via the website, then a repository URL entered through the nuspec.
        /// </summary>
        /// <param name="formData">The current package's form data submitted through the verify page</param>
        /// <param name="packageMetadata">The package metadata from the nuspec file</param>
        /// <returns>A stream with the encoded ReadMe file</returns>
        public static Stream GetReadMeMarkdownStream(ReadMeRequest formData)
        {
            Stream readMeStream;
            switch (formData.ReadMeType)
            {
                case TypeUrl:
                    readMeStream = ReadMeUrlToStream(formData.ReadMeUrl);
                    break;
                case TypeFile:
                    readMeStream = formData.ReadMeFile.InputStream;
                    break;
                case TypeWritten:
                    readMeStream = GetStreamFromWritten(formData.ReadMeWritten);
                    break;
                default:
                    throw new InvalidOperationException("Form data contains an invalid ReadMeType.");
            }

            if (ValidateReadMeStreamLength(readMeStream))
            {
                return readMeStream;
            }
            else
            {
                throw new ArgumentException("ReadMe file exceeds size limitations. (" + MaxFileSize + ")");
            }
        }

        /// <param name="readMeStream">A stream representing the package readme.</param>
        /// <returns>Whether the stream is less than MaxFileSize.</returns>
        private static bool ValidateReadMeStreamLength(Stream readMeStream)
        {
            return readMeStream.AsSeekableStream().Length < MaxFileSize;
        }

        /// <summary>
        /// Converts a ReadMe's url to a file stream.
        /// </summary>
        /// <param name="readMeUrl">A link to the raw ReadMe.md file</param>
        /// <returns>A stream to allow the file to be read</returns>
        private static Stream ReadMeUrlToStream(string readMeUrl)
        {
            var readMeUri = new Uri(readMeUrl);
            if (readMeUri.Host != UriHostRequirement)
            {
                throw new ArgumentException("Url must link to a raw markdown file hosted on Github. [" + UriHostRequirement + "]");
            }
            var webRequest = WebRequest.Create(readMeUrl);
            webRequest.Timeout = UrlTimeout;
            var response = webRequest.GetResponse();
            return response.GetResponseStream().AsSeekableStream();
        }

        private static Stream GetStreamFromWritten(string writtenText)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(writtenText);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}