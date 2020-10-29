// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public interface IMarkdownService
    {
        /// <summary>
        /// Returns HTML from the supplied markdown
        /// </summary>
        /// <param name="markdownString">markdown input</param>
        /// <returns>HTML data</returns>
        RenderedMarkdownResult GetHtmlFromMarkdown(string markdownString);

        /// <summary>
        /// Returns HTML from the supplied markdown
        /// </summary>
        /// <param name="markdownString">markdown input</param>
        /// <param name="incrementHeadersBy">headers can be incremented by this value, eg if 2 supplied then h1 will become h3</param>
        /// <returns>HTML data</returns>
        RenderedMarkdownResult GetHtmlFromMarkdown(string markdownString, int incrementHeadersBy);
    }
}