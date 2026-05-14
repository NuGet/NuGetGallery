// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using Ganss.Xss;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.TaskLists;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace NuGetGallery
{
    public class MarkdownService : IMarkdownService
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMinutes(1);
        private static readonly Regex HtmlCommentPattern = new Regex("<!--.*?-->", RegexOptions.Singleline, RegexTimeout);
        private static readonly Regex ImageTextPattern = new Regex("!\\[\\]\\(", RegexOptions.Singleline, RegexTimeout);
        private static readonly string AltTextForImage = "alternate text is missing from this package README image";

        private readonly IFeatureFlagService _features;
        private readonly IHtmlSanitizer _htmlSanitizer;
        private bool _imagesRewritten;

        public MarkdownService(IFeatureFlagService features,
            IHtmlSanitizer htmlSanitizer)
        {
            _features = features ?? throw new ArgumentNullException(nameof(features));
            _htmlSanitizer = htmlSanitizer ?? throw new ArgumentNullException(nameof(htmlSanitizer));
            ConfigureSanitizer();
        }

        private void ConfigureSanitizer()
        {
            // HtmlSanitizer ships with 95 default allowed attributes including href, src, alt, width,
            // height, style, align, colspan, rowspan, border, color, disabled, checked, readonly,
            // target, title, type, value, name, lang, dir, tabindex, shape, coords, usemap, rel, rev,
            // and many others. However, "class" and "id" are explicitly excluded from the defaults to
            // prevent CSS-based attacks and element ID conflicts. "aria-label" is also not in the defaults.
            // We add all three here because they are needed for our rendered markdown.
            _htmlSanitizer.AllowedAttributes.Add("id");
            _htmlSanitizer.AllowedAttributes.Add("class");
            _htmlSanitizer.AllowedAttributes.Add("aria-label");

            _htmlSanitizer.PostProcessNode += (sender, args) =>
            {
                if (args.Node is IHtmlAnchorElement anchor)
                {
                    anchor.SetAttribute("rel", "noopener noreferrer nofollow");

                    RewriteHttpToHttps(anchor, "href");
                }
                else if (args.Node is IHtmlImageElement image)
                {
                    if (RewriteHttpToHttps(image, "src"))
                    {
                        _imagesRewritten = true;
                    }
                }
            };
        }

        private static bool RewriteHttpToHttps(AngleSharp.Dom.IElement element, string attribute)
        {
            var url = element.GetAttribute(attribute);
            if (url != null &&
                Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttp)
            {
                var builder = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 };
                element.SetAttribute(attribute, builder.Uri.AbsoluteUri);
                return true;
            }
            return false;
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

            return GetHtmlFromMarkdownMarkdig(markdownString, 1);
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

            return GetHtmlFromMarkdownMarkdig(markdownString, incrementHeadersBy);
        }

        private RenderedMarkdownResult GetHtmlFromMarkdownMarkdig(string markdownString, int incrementHeadersBy)
        {
            var output = new RenderedMarkdownResult()
            {
                ImagesRewritten = false,
                Content = "",
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
                            // Image HTTP→HTTPS rewriting is handled by the PostProcessNode sanitizer hook
                            // rather than here, since both Markdig-rendered and raw HTML images pass through
                            // the sanitizer as <img> elements.
                            if (!linkInline.IsImage)
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
                        else if (node is TaskList taskList)
                        {
                            // Add aria-label to task list checkboxes for accessibility.
                            taskList.GetAttributes().AddProperty("aria-label", taskList.Checked ? "Completed" : "Not completed");
                        }
                    }
                }

                renderer.Render(document);
                output.Content = htmlWriter.ToString().Trim();
                output.IsMarkdigMdSyntaxHighlightEnabled = _features.IsMarkdigMdSyntaxHighlightEnabled();

                // Reset before sanitization; PostProcessNode sets this to true if any image src is rewritten.
                _imagesRewritten = false;
                output.Content = SanitizeText(output.Content);
                output.ImagesRewritten = _imagesRewritten;

                return output;
            }
        }
    }
}
