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
        private static readonly Regex LinkPattern = new Regex("<a href=([\"\']).*?\\1", RegexOptions.None, RegexTimeout);

        private readonly IFeatureFlagService _features;

        public MarkdownService(IFeatureFlagService features)
        {
            _features = features ?? throw new ArgumentNullException(nameof(features));
        }

        public RenderedMarkdownResult GetHtmlFromMarkdown(string markdownString)
        {
            if (_features.IsMarkdigMdRenderingEnabled()) 
            { 
                return GetHtmlFromMarkdownMarkdig(markdownString, 1);
            }
            else
            {
                return GetHtmlFromMarkdownCommonMark(markdownString, 1);
            }
        }

        public RenderedMarkdownResult GetHtmlFromMarkdown(string markdownString, int incrementHeadersBy)
        {
            if (_features.IsMarkdigMdRenderingEnabled())
            {
                return GetHtmlFromMarkdownMarkdig(markdownString, incrementHeadersBy);
            }
            else
            {
                return GetHtmlFromMarkdownCommonMark(markdownString, incrementHeadersBy);
            }

        }

        private RenderedMarkdownResult GetHtmlFromMarkdownCommonMark(string markdownString, int incrementHeadersBy)
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

                output.Content = LinkPattern.Replace(htmlWriter.ToString(), "$0" + " rel=\"nofollow\"").Trim();
                return output;
            }
        }

        private RenderedMarkdownResult GetHtmlFromMarkdownMarkdig(string markdownString, int incrementHeadersBy)
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
            output.Content = LinkPattern.Replace(htmlWriter.ToString(), "$0" + " rel=\"nofollow\"").Trim();
            return output;
        }
    }
}