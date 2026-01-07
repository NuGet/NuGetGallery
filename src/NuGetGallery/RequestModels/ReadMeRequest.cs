// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using System.Web.Mvc;

namespace NuGetGallery
{
    /// <summary>
    /// ReadMe markdown request model for package version edits.
    /// </summary>
    public sealed class ReadMeRequest
    {
        /// <summary>
        /// Source type for an imported readme.md file. Supported types are 'file', 'url' or 'written'.
        /// </summary>
        public string SourceType { get; set; }

        /// <summary>
        /// Source posted file, used for readme.md import when <see cref="SourceType"/> is 'file'.
        /// </summary>
        public HttpPostedFileBase SourceFile { get; set; }

        /// <summary>
        /// Source text, used for readme.md import when <see cref="SourceType"/> is 'written'.
        /// </summary>
        [AllowHtml]
        public string SourceText { get; set; }

        /// <summary>
        /// Source uri, used for readme.md import when <see cref="SourceType"/> is 'url'.
        /// </summary>
        public string SourceUrl { get; set; }
    }
}