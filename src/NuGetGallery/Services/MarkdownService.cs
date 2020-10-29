// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;
using System.Web;
using CommonMark;
using CommonMark.Syntax;
using Markdig;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace NuGetGallery
{
    public class MarkdownService : IMarkdownService
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMinutes(1);
        private static readonly Regex EncodedBlockQuotePattern = new Regex("^ {0,3}&gt;", RegexOptions.Multiline, RegexTimeout);
        private static readonly Regex CommonMarkLinkPattern = new Regex("<a href=([\"\']).*?\\1", RegexOptions.None, RegexTimeout);

        public RenderedMarkdownResult GetHtmlFromMarkdown(string markdownString)
        {
            return GetHtmlFromMarkdown(markdownString, 1);
        }

        public RenderedMarkdownResult GetHtmlFromMarkdown(string markdownString, int incrementHeadersBy)
        {
            var output = new RenderedMarkdownResult()
            {
                ImagesRewritten = false,
                Content = ""
            };

            var readmeWithoutBom = markdownString.TrimStart('\ufeff');

            // HTML encode markdown, except for block quotes, to block inline html.
            var encodedMarkdown = EncodedBlockQuotePattern.Replace(HttpUtility.HtmlEncode(readmeWithoutBom), "> ");

            var pipeline = new MarkdownPipelineBuilder()
                .UseSoftlineBreakAsHardlineBreak()
                .Build();

            var document = Markdown.Parse(encodedMarkdown, pipeline);

            foreach (var node in document.Descendants())
            {
                if (node is Markdig.Syntax.Block)
                {
                    // Demote heading tags so they don't overpower expander headings.
                    if (node is HeadingBlock heading)
                    {
                        heading.Level = (byte)Math.Min(heading.Level + incrementHeadersBy, 6);
                    }

                }
                else if (node is Markdig.Syntax.Inlines.Inline)
                {
                    if (node is LinkInline linkInline)
                    {
                        if (linkInline.IsImage)
                        {
                            // Allow only http or https links in markdown. Transform link to https for known domains.
                            if (!PackageHelper.TryPrepareUrlForRendering(linkInline.Url, out string readyUriString, rewriteAllHttp: true))
                            {
                                linkInline.Url = string.Empty;
                            }
                            else
                            {
                                output.ImagesRewritten = output.ImagesRewritten || (linkInline.Url != readyUriString);
                                linkInline.Url = readyUriString;
                            }
                        }
                        else
                        {
                            if (!PackageHelper.TryPrepareUrlForRendering(linkInline.Url, out string readyUriString))
                            {
                                linkInline.Url = string.Empty;
                            }
                            else
                            {
                                linkInline.Url = readyUriString;
                            }
                        }
                    }
                }
            }

            StringWriter htmlWriter = new StringWriter();
            var renderer = new HtmlRenderer(htmlWriter);
            renderer.Render(document);
            htmlWriter.Flush();
            //manually inject nofollow since markdig doesn't support inject nofollw in encode 
            output.Content = CommonMarkLinkPattern.Replace(htmlWriter.ToString(), "$0" + " rel=\"nofollow\"").Trim();
            return output;
        }
    }
}