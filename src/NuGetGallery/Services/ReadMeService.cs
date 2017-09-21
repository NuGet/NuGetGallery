// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using CommonMark;
using System.Text.RegularExpressions;

namespace NuGetGallery
{
    internal class ReadMeService : IReadMeService
    {
        internal const string TypeUrl = "Url";
        internal const string TypeFile = "File";
        internal const string TypeWritten = "Written";

        internal const int MaxMdLengthBytes = 8000;
        private const int ReadMeClampedLineCount = 10;
        private const string UrlHostRequirement = "raw.githubusercontent.com";
        private static readonly TimeSpan UrlTimeout = TimeSpan.FromSeconds(10);

        private IPackageFileService _packageFileService;
        
        public ReadMeService(IPackageFileService packageFileService)
        {
            _packageFileService = packageFileService;
        }

        /// <summary>
        /// Determine if a <see cref="ReadMeRequest"/> has readMe markdown data.
        /// </summary>
        /// <param name="readMeRequest">Request object.</param>
        /// <returns>True if input provided, false otherwise.</returns>
        public bool HasReadMeSource(ReadMeRequest readMeRequest)
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
                return readMeRequest.SourceFile != null;
            }

            return false;
        }

        /// <summary>
        /// Get the converted HTML from a <see cref="ReadMeRequest"/> request with markdown data.
        /// </summary>
        /// <param name="readMeRequest">Request object.</param>
        /// <returns>HTML from markdown conversion.</returns>
        public async Task<string> GetReadMeHtmlAsync(ReadMeRequest readMeRequest, Encoding encoding)
        {
            var markdown = await GetReadMeMdAsync(readMeRequest, encoding);
            return GetReadMeHtml(markdown);
        }

        /// <summary>
        /// Get the converted HTML from the stored ReadMe markdown.
        /// </summary>
        /// <param name="package">Package entity associated with the ReadMe.</param>
        /// <param name="model">Display package view model to populate.</param>
        /// <param name="isPending">Whether to retrieve the pending ReadMe.</param>
        /// <returns>Pending or active ReadMe converted to HTML.</returns>
        public async Task GetReadMeHtmlAsync(Package package, DisplayPackageViewModel model, bool isPending = false)
        {
            var readMeMd = await GetReadMeMdAsync(package, isPending);
            if (!string.IsNullOrWhiteSpace(readMeMd))
            {
                var readMeMdClamped = string.Join(Environment.NewLine,
                    readMeMd.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Take(ReadMeClampedLineCount));

                model.ReadMeHtml = GetReadMeHtml(readMeMd).Trim();
                model.ReadMeHtmlClamped = GetReadMeHtml(readMeMdClamped).Trim();
            }
        }

        /// <summary>
        /// Get package ReadMe markdown from storage.
        /// </summary>
        /// <param name="package">Package entity associated with the ReadMe.</param>
        /// <param name="isPending">Whether to retrieve the pending ReadMe.</param>
        /// <returns>Pending or active ReadMe markdown from storage.</returns>
        public async Task<string> GetReadMeMdAsync(Package package, bool isPending = false)
        {
            if (package.HasReadMe || isPending)
            {
                return await _packageFileService.DownloadReadMeMdFileAsync(package, isPending);
            }

            return null;
        }

        /// <summary>
        /// Save a pending ReadMe if changes are detected.
        /// </summary>
        /// <param name="package">Package entity associated with the ReadMe.</param>
        /// <param name="edit">Package edit entity.</param>
        /// <returns>True if a ReadMe is pending, false otherwise.</returns>
        public async Task<bool> SavePendingReadMeMdIfChanged(Package package, EditPackageVersionRequest edit, Encoding encoding)
        {
            var activeReadMe = package.HasReadMe ?
                NormalizeNewLines(await GetReadMeMdAsync(package)) :
                null;

            var newReadMe = HasReadMeSource(edit?.ReadMe) ?
                NormalizeNewLines(await GetReadMeMdAsync(edit.ReadMe, encoding)) :
                null;

            var hasReadMe = !string.IsNullOrWhiteSpace(newReadMe);
            if (hasReadMe && !newReadMe.Equals(activeReadMe))
            {
                await _packageFileService.SavePendingReadMeMdFileAsync(package, newReadMe);
                edit.ReadMeState = PackageEditReadMeState.Changed;
            }
            else if (!hasReadMe && !string.IsNullOrEmpty(activeReadMe))
            {
                await _packageFileService.DeleteReadMeMdFileAsync(package, isPending: true);
                edit.ReadMeState = PackageEditReadMeState.Deleted;
            }
            else
            {
                edit.ReadMeState = PackageEditReadMeState.Unchanged;
            }

            return hasReadMe;
        }

        /// <summary>
        /// Get converted HTML for readme.md string content.
        /// </summary>
        /// <param name="readMeMd">ReadMe.md content.</param>
        /// <returns>HTML content.</returns>
        internal static string GetReadMeHtml(string readMeMd)
        {
            var encodedMarkdown = HttpUtility.HtmlEncode(readMeMd);
            return CommonMarkConverter.Convert(encodedMarkdown);
        }

        /// <summary>
        /// Get readme.md content from a ReadMeRequest object.
        /// </summary>
        /// <param name="readMeRequest">ReadMe request from Verify or Edit package page.</param>
        /// <returns>Markdown content.</returns>
        internal static async Task<string> GetReadMeMdAsync(ReadMeRequest readMeRequest, Encoding encoding)
        {
            var readMeType = readMeRequest?.SourceType;
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            if (TypeWritten.Equals(readMeType, StringComparison.InvariantCultureIgnoreCase))
            {
                if (encoding.GetByteCount(readMeRequest.SourceText) > MaxMdLengthBytes)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                        Strings.ReadMeMaxLengthExceeded, MaxMdLengthBytes));
                }
                return readMeRequest.SourceText;
            }
            else if (TypeUrl.Equals(readMeType, StringComparison.InvariantCultureIgnoreCase))
            {
                return await GetReadMeMdFromUrlAsync(readMeRequest.SourceUrl, encoding);
            }
            else if (TypeFile.Equals(readMeType, StringComparison.InvariantCultureIgnoreCase))
            {
                return await GetReadMeMdFromPostedFileAsync(readMeRequest.SourceFile, encoding);
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
        private static async Task<string> GetReadMeMdFromPostedFileAsync(HttpPostedFileBase readMeMdPostedFile, Encoding encoding)
        {
            if (!Path.GetExtension(readMeMdPostedFile.FileName).Equals(Constants.MarkdownFileExtension, StringComparison.InvariantCulture))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    Strings.ReadMePostedFileExtensionInvalid, Constants.MarkdownFileExtension));
            }

            using (var readMeMdStream = readMeMdPostedFile.InputStream)
            {
                return await ReadMaxAsync(readMeMdStream, MaxMdLengthBytes, encoding);
            }
        }

        /// <summary>
        /// Get readme.md content from a url.
        /// </summary>
        /// <param name="readMeMdUrl">ReadMe.md URL, which must be a raw github file.</param>
        /// <returns>Markdown content.</returns>
        private static async Task<string> GetReadMeMdFromUrlAsync(string readMeMdUrl, Encoding encoding)
        {
            if (!Uri.IsWellFormedUriString(readMeMdUrl, UriKind.Absolute) || new Uri(readMeMdUrl).Host != UrlHostRequirement)
            {
                throw new ArgumentException(Strings.ReadMeUrlHostInvalid, nameof(readMeMdUrl));
            }

            using (var client = new HttpClient() { Timeout = UrlTimeout })
            {
                using (var httpStream = await client.GetStreamAsync(readMeMdUrl))
                {
                    return await ReadMaxAsync(httpStream, MaxMdLengthBytes, encoding);
                }
            }
        }

        private static async Task<string> ReadMaxAsync(Stream stream, int maxSize, Encoding encoding)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            int bytesRead;
            var offset = 0;
            var buffer = new byte[maxSize + 1];

            while ((bytesRead = await stream.ReadAsync(buffer, offset, buffer.Length - offset)) > 0)
            {
                offset += bytesRead;

                if (offset == buffer.Length)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                        Strings.ReadMeMaxLengthExceeded, maxSize));
                }
            }

            return encoding.GetString(buffer).Trim('\0');
        }

        private static readonly Regex NewLineRegex = new Regex(@"\n|\r\n");
        
        private static string NormalizeNewLines(string content)
        {
            return NewLineRegex.Replace(content, Environment.NewLine);
        }
    }
}