// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using CommonMark;
using CommonMark.Syntax;
using Markdig;


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

            var readmeWithoutBom = markdownString.StartsWith("\ufeff") ? markdownString.Replace("\ufeff", "") : markdownString;

            // HTML encode markdown, except for block quotes, to block inline html.
            var encodedMarkdown = EncodedBlockQuotePattern.Replace(HttpUtility.HtmlEncode(readmeWithoutBom), "> ");

            var settings = CommonMarkSettings.Default.Clone();
            settings.RenderSoftLineBreaksAsLineBreaks = true;

            // Parse executes CommonMarkConverter's ProcessStage1 and ProcessStage2.
            var document = CommonMarkConverter.Parse(encodedMarkdown, settings);
            foreach (var node in document.AsEnumerable())
            {
                if (node.IsOpening)
                {
                    var block = node.Block;
                    if (block != null)
                    {
                        switch (block.Tag)
                        {
                            // Demote heading tags so they don't overpower expander headings.
                            case BlockTag.AtxHeading:
                            case BlockTag.SetextHeading:
                                var level = (byte)Math.Min(block.Heading.Level + incrementHeadersBy, 6);
                                block.Heading = new HeadingData(level);
                                break;

                            // Decode preformatted blocks to prevent double encoding.
                            // Skip BlockTag.BlockQuote, which are partially decoded upfront.
                            case BlockTag.FencedCode:
                            case BlockTag.IndentedCode:
                                if (block.StringContent != null)
                                {
                                    var content = block.StringContent.TakeFromStart(block.StringContent.Length);
                                    var unencodedContent = HttpUtility.HtmlDecode(content);
                                    block.StringContent.Replace(unencodedContent, 0, unencodedContent.Length);
                                }
                                break;
                        }
                    }

                    var inline = node.Inline;
                    if (inline != null)
                    {
                        if (inline.Tag == InlineTag.Link)
                        {
                            // Allow only http or https links in markdown. Transform link to https for known domains.
                            if (!PackageHelper.TryPrepareUrlForRendering(inline.TargetUrl, out string readyUriString))
                            {
                                inline.TargetUrl = string.Empty;
                            }
                            else
                            {
                                inline.TargetUrl = readyUriString;
                            }
                        }

                        else if (inline.Tag == InlineTag.Image)
                        {
                            if (!PackageHelper.TryPrepareUrlForRendering(inline.TargetUrl, out string readyUriString, rewriteAllHttp: true))
                            {
                                inline.TargetUrl = string.Empty;
                            }
                            else
                            {
                                output.ImagesRewritten = output.ImagesRewritten || (inline.TargetUrl != readyUriString);
                                inline.TargetUrl = readyUriString;
                            }
                        }
                    }
                }
            }

            // CommonMark.Net does not support link attributes, so manually inject nofollow.
            using (var htmlWriter = new StringWriter())
            {
                CommonMarkConverter.ProcessStage3(document, htmlWriter, settings);

                output.Content = CommonMarkLinkPattern.Replace(htmlWriter.ToString(), "$0" + " rel=\"nofollow\"").Trim();
                return output;
            }
        }

        public RenderedMarkdownResult GetHtmlFromMarkdownMarkdig(string markdownString, int incrementHeadersBy)
        {
            var output = new RenderedMarkdownResult()
            {
                ImagesRewritten = false,
                Content = ""
            };

            var readmeWithoutBom = markdownString.StartsWith("\ufeff") ? markdownString.Replace("\ufeff", "") : markdownString;

            // HTML encode markdown, except for block quotes, to block inline html.
            var encodedMarkdown = EncodedBlockQuotePattern.Replace(HttpUtility.HtmlEncode(readmeWithoutBom), "> ");

            var builder = 




        }
    }
}