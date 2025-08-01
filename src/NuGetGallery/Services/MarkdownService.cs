// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using CommonMark;
using CommonMark.Syntax;
using Ganss.Xss;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.EmphasisExtras;
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
        private static readonly Regex HtmlCommentPattern = new Regex("<!--.*?-->", RegexOptions.Singleline, RegexTimeout);
        private static readonly Regex ImageTextPattern = new Regex("!\\[\\]\\(", RegexOptions.Singleline, RegexTimeout);
        private static readonly string AltTextForImage = "alternate text is missing from this package README image";

        private readonly IFeatureFlagService _features;
        private readonly IImageDomainValidator _imageDomainValidator;
        private readonly IHtmlSanitizer _htmlSanitizer;

        public MarkdownService(IFeatureFlagService features,
            IImageDomainValidator imageDomainValidator,
            IHtmlSanitizer htmlSanitizer)
        {
            _features = features ?? throw new ArgumentNullException(nameof(features));
            _imageDomainValidator = imageDomainValidator ?? throw new ArgumentNullException(nameof(imageDomainValidator));
            _htmlSanitizer = htmlSanitizer ?? throw new ArgumentNullException(nameof(htmlSanitizer));
            SanitizerSettings();
        }

        private void SanitizerSettings()
        {
            //Configure allowed tags, attributes for the sanitizer
            _htmlSanitizer.AllowedAttributes.Add("id");
            _htmlSanitizer.AllowedAttributes.Add("class");
        }

        private string SanitizeText(string input)
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                return _htmlSanitizer.Sanitize(input);
            }
            return input;
        }

        public RenderedMarkdownResult GetHtmlFromMarkdown(string markdownString)
        {
            if (markdownString == null)
            {
                throw new ArgumentNullException(nameof(markdownString));
            }


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
            if (markdownString == null)
            {
                throw new ArgumentNullException(nameof(markdownString));
            }

            if (incrementHeadersBy < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(incrementHeadersBy), $"{nameof(incrementHeadersBy)} must be greater than or equal to 0");
            }

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
                Content = "",
                ImageSourceDisallowed = false,
                IsMarkdigMdSyntaxHighlightEnabled = false
            };

            var markdownWithoutComments = HtmlCommentPattern.Replace(markdownString, "");

            var markdownWithoutBom = markdownWithoutComments.StartsWith("\ufeff") ? markdownWithoutComments.Replace("\ufeff", "") : markdownWithoutComments;

            // HTML encode markdown, except for block quotes, to block inline html.
            var encodedMarkdown = EncodedBlockQuotePattern.Replace(HttpUtility.HtmlEncode(markdownWithoutBom), "> ");

            var settings = CommonMarkSettings.Default.Clone();

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
                            if (_features.IsImageAllowlistEnabled())
                            {
                                if (!_imageDomainValidator.TryPrepareImageUrlForRendering(inline.TargetUrl, out string readyUriString))
                                {
                                    inline.TargetUrl = string.Empty;
                                    output.ImageSourceDisallowed = true;
                                }
                                else
                                {
                                    output.ImagesRewritten = output.ImagesRewritten || (inline.TargetUrl != readyUriString);
                                    inline.TargetUrl = readyUriString;
                                }
                            }
                            else
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
            }

            output.IsMarkdigMdSyntaxHighlightEnabled = _features.IsMarkdigMdSyntaxHighlightEnabled();

            // CommonMark.Net does not support link attributes, so manually inject nofollow.
            using (var htmlWriter = new StringWriter())
            {
                CommonMarkConverter.ProcessStage3(document, htmlWriter, settings);
                string htmlContent = htmlWriter.ToString();
                htmlContent = SanitizeText(htmlContent);
                output.Content = LinkPattern.Replace(htmlContent, "$0" + " rel=\"noopener noreferrer nofollow\"").Trim();

                return output;
            }
        }

        private RenderedMarkdownResult GetHtmlFromMarkdownMarkdig(string markdownString, int incrementHeadersBy)
        {
            var output = new RenderedMarkdownResult()
            {
                ImagesRewritten = false,
                Content = "",
                ImageSourceDisallowed = false,
                IsMarkdigMdSyntaxHighlightEnabled = false
            };

            var markdownWithoutComments = HtmlCommentPattern.Replace(markdownString, "");

            var markdownWithImageAlt = ImageTextPattern.Replace(markdownWithoutComments, $"![{AltTextForImage}](");

            var markdownWithoutBom = markdownWithImageAlt.TrimStart('\ufeff');

            var pipeline = new MarkdownPipelineBuilder()
                .UseGridTables()
                .UsePipeTables()
                .UseListExtras()
                .UseTaskLists()
                .UseEmojiAndSmiley()
                .UseAutoLinks()
                .UseAlertBlocks()
                .UseReferralLinks("noopener noreferrer nofollow")
                .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
                .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
                .DisableHtml() //block inline html
                .UseBootstrap()
                .Build();

            using (var htmlWriter = new StringWriter())
            {
                var renderer = new HtmlRenderer(htmlWriter);
                pipeline.Setup(renderer);

                var document = Markdown.Parse(markdownWithoutBom, pipeline);

                foreach (var node in document.Descendants())
                {
                    if (node is Markdig.Syntax.Block)
                    {
                        // Demote heading tags so they don't overpower expander headings.
                        if (node is HeadingBlock heading)
                        {
                            heading.Level = Math.Min(heading.Level + incrementHeadersBy, 6);
                        }
                    }
                    else if (node is Markdig.Syntax.Inlines.Inline)
                    {
                        if (node is LinkInline linkInline)
                        {
                            if (linkInline.IsImage)
                            {
                                if (_features.IsImageAllowlistEnabled())
                                {
                                    if (!_imageDomainValidator.TryPrepareImageUrlForRendering(linkInline.Url, out string readyUriString))
                                    {
                                        linkInline.Url = string.Empty;
                                        output.ImageSourceDisallowed = true;
                                    }
                                    else
                                    {
                                        output.ImagesRewritten = output.ImagesRewritten || (linkInline.Url != readyUriString);
                                        linkInline.Url = readyUriString;
                                    }
                                }
                                else
                                {
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
                            }
                            else
                            {
                                // Allow only http or https links in markdown. Transform link to https for known domains.
                                if (!PackageHelper.TryPrepareUrlForRendering(linkInline.Url, out string readyUriString))
                                {
                                    if (linkInline.Url != null && !linkInline.Url.StartsWith("#")) //allow internal section links
                                    {
                                        linkInline.Url = string.Empty;
                                    }
                                }
                                else
                                {
                                    linkInline.Url = readyUriString;
                                }
                            }
                        }
                    }
                }

                renderer.Render(document);
                output.Content = htmlWriter.ToString().Trim();
                output.IsMarkdigMdSyntaxHighlightEnabled = _features.IsMarkdigMdSyntaxHighlightEnabled();
                output.Content = SanitizeText(output.Content);

                return output;
            }
        }
    }
}
