﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using NuGet.Packaging;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    internal class ReadMeService : IReadMeService
    {

        internal const string TypeUrl = "Url";
        internal const string TypeFile = "File";
        internal const string TypeWritten = "Written";

        internal const int MaxAllowedReadmeBytes = GalleryConstants.MaxFileLengthBytes;
        private const string UrlHostRequirement = "raw.githubusercontent.com";

        private static readonly TimeSpan UrlTimeout = TimeSpan.FromSeconds(10);
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageFileService _packageFileService;
        private readonly IMarkdownService _markdownService;
        private readonly ICoreReadmeFileService _coreReadmeFileService;

        public ReadMeService(
            IPackageFileService packageFileService,
            IEntitiesContext entitiesContext,
            IMarkdownService markdownService,
            ICoreReadmeFileService coreReadmeFileService)
        {
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _markdownService = markdownService ?? throw new ArgumentNullException(nameof(markdownService));
            _coreReadmeFileService = coreReadmeFileService ?? throw new ArgumentNullException(nameof(coreReadmeFileService));
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
        public async Task<RenderedMarkdownResult> GetReadMeHtmlAsync(ReadMeRequest readMeRequest, Encoding encoding)
        {
            var markdown = await GetReadMeMdAsync(readMeRequest, encoding);
            return _markdownService.GetHtmlFromMarkdown(markdown);
        }

        /// <summary>
        /// Get the converted HTML from the stored ReadMe markdown.
        /// </summary>
        /// <param name="package">Package entity associated with the ReadMe.</param>
        /// <returns>ReadMe converted to HTML.</returns>
        public async Task<RenderedMarkdownResult> GetReadMeHtmlAsync(Package package)
        {
            var readMeMd = await GetReadMeMdAsync(package);
            var result = new RenderedMarkdownResult
            {
                Content = readMeMd,
                ImagesRewritten = false,
                ImageSourceDisallowed = false
            };

            return string.IsNullOrEmpty(readMeMd) ?
                result :
                _markdownService.GetHtmlFromMarkdown(readMeMd);
        }

        /// <summary>
        /// Get the converted HTML from the package with Readme markdown.
        /// </summary>
        /// <param name="readmeFileName">The path of Readme markdown.</param>
        /// <param name="packageArchiveReader">
        /// The <see cref="PackageArchiveReader"/> instance providing the package metadata.
        /// </param>
        /// <returns>ReadMe converted to HTML.</returns>
        public async Task<RenderedMarkdownResult> GetReadMeHtmlAsync(string readmeFileName, PackageArchiveReader packageArchiveReader, Encoding encoding)
        {
            var readmeMd = await GetReadMeMdAsync(readmeFileName, packageArchiveReader, encoding);
            var result = new RenderedMarkdownResult
            {
                Content = readmeMd,
                ImagesRewritten = false,
                ImageSourceDisallowed = false
            };

            return string.IsNullOrEmpty(readmeMd) ?
                result :
                _markdownService.GetHtmlFromMarkdown(readmeMd);
        }

        /// <summary>
        /// Get package ReadMe markdown from storage.
        /// </summary>
        /// <param name="package">Package entity associated with the ReadMe.</param>
        /// <returns>ReadMe markdown from storage.</returns>
        public async Task<string> GetReadMeMdAsync(Package package)
        {
            if (package.HasReadMe)
            {
                if (package.HasEmbeddedReadme)
                {
                    return await _coreReadmeFileService.DownloadReadmeFileAsync(package);
                }
                else
                {
                    return await _packageFileService.DownloadReadMeMdFileAsync(package);
                }
            }

            return null;
        }

        /// <summary>
        /// Get content of Readme markdown.
        /// </summary>
        /// <param name="readmeFileName">Package entity associated with the ReadMe.</param>
        /// <param name="packageArchiveReader">
        /// The <see cref="PackageArchiveReader"/> instance providing the package metadata.
        /// </param>
        /// <returns>ReadMe markdown from package metadata.</returns>
        private async Task<string> GetReadMeMdAsync(string readmeFileName, PackageArchiveReader packageArchiveReader, Encoding encoding)
        {
            using (var readmeFileStream = packageArchiveReader.GetStream(readmeFileName))
            using (var streamReader = new StreamReader(readmeFileStream, encoding))
            {
                 return await streamReader.ReadToEndAsync();
            }
        }

        /// <summary>
        /// Save a pending ReadMe if changes are detected.
        /// </summary>
        /// <param name="package">Package entity associated with the ReadMe.</param>
        /// <param name="edit">Package version edit readme request.</param>
        /// <param name="encoding">The encoding used when reading the existing readme.</param>
        /// <param name="commitChanges">Whether or not to commit the pending changes to the database.</param>
        /// <returns>True if the package readme changed, otherwise false.</returns>
        public async Task<bool> SaveReadMeMdIfChanged(
            Package package,
            EditPackageVersionReadMeRequest edit,
            Encoding encoding,
            bool commitChanges)
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
                await _packageFileService.SaveReadMeMdFileAsync(package, newReadMe);
                edit.ReadMeState = PackageEditReadMeState.Changed;

                // Save entity to db.
                package.HasReadMe = true;

                if (commitChanges)
                {
                    await _entitiesContext.SaveChangesAsync();
                }
            }
            else if (!hasReadMe && !string.IsNullOrEmpty(activeReadMe))
            {
                await _packageFileService.DeleteReadMeMdFileAsync(package);
                edit.ReadMeState = PackageEditReadMeState.Deleted;

                // Save entity to db.
                package.HasReadMe = false;

                if (commitChanges)
                {
                    await _entitiesContext.SaveChangesAsync();
                }
            }
            else
            {
                edit.ReadMeState = PackageEditReadMeState.Unchanged;
            }

            return edit.ReadMeState != PackageEditReadMeState.Unchanged;
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
                if (encoding.GetByteCount(readMeRequest.SourceText) > MaxAllowedReadmeBytes)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                        Strings.ReadMeMaxLengthExceeded, MaxAllowedReadmeBytes));
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
            if (!Path.GetExtension(readMeMdPostedFile.FileName).Equals(ServicesConstants.MarkdownFileExtension, StringComparison.InvariantCulture))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    Strings.ReadMePostedFileExtensionInvalid, ServicesConstants.MarkdownFileExtension));
            }

            using (var readMeMdStream = readMeMdPostedFile.InputStream)
            {
                return await ReadMaxAsync(readMeMdStream, MaxAllowedReadmeBytes, encoding);
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
                    return await ReadMaxAsync(httpStream, MaxAllowedReadmeBytes, encoding);
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

        private static readonly Regex NewLineRegex = RegexEx.CreateWithTimeout(@"\n|\r\n", RegexOptions.None);

        private static string NormalizeNewLines(string content)
        {
            if (content == null) return null;

            return NewLineRegex.Replace(content, Environment.NewLine);
        }
    }
}