// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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

        // Allowlist of HTML tags modeled after GitHub's Markdown sanitization filter.
        // See: https://github.com/jch/html-pipeline/blob/main/lib/html_pipeline/sanitization_filter.rb
        // Note: "input" is NOT in this list. Markdig task list checkboxes are preserved via
        // the RemovingTag event which allows only <input type="checkbox" disabled>.
        private static readonly HashSet<string> AllowedTagsList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "abbr", "b", "bdo", "blockquote", "br", "caption", "cite", "code",
            "dd", "del", "details", "dfn", "div", "dl", "dt",
            "em", "figcaption", "figure",
            "h1", "h2", "h3", "h4", "h5", "h6", "hr",
            "i", "img", "ins",
            "kbd", "li", "mark",
            "ol", "p", "picture", "pre",
            "q", "rp", "rt", "ruby",
            "s", "samp", "small", "source", "span", "strike", "strong", "sub", "summary", "sup",
            "table", "tbody", "td", "tfoot", "th", "thead", "time", "tr", "tt",
            "ul", "var", "wbr"
        };

        // Allowlist of HTML attributes modeled after GitHub's sanitization filter.
        // This is the union of GitHub's global "all" list plus their per-element attributes
        // (href, src, longdesc, loading, srcset, itemscope, itemtype) applied globally since
        // the Ganss.Xss HtmlSanitizer does not support per-element attribute restrictions.
        // Per-element attributes on stripped tags are harmless (e.g., "action" without "form").
        // We add "class" (needed for Markdig CSS classes like "table", "img-fluid", "task-list-item").
        // We intentionally exclude "style" to prevent CSS-based attacks (positioning overlays,
        // background-image tracking, opacity clickjacking).
        private static readonly HashSet<string> AllowedAttributesList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // GitHub's global "all" attributes
            "abbr", "accept", "accept-charset", "action", "align", "alt",
            "aria-describedby", "aria-hidden", "aria-label", "aria-labelledby",
            "axis", "border", "char", "charoff", "charset", "checked", "clear",
            "cols", "colspan", "compact", "coords", "datetime", "dir", "disabled",
            "enctype", "for", "frame", "headers", "height", "hreflang", "hspace",
            "id", "ismap", "label", "lang", "maxlength", "media", "method", "multiple",
            "name", "nohref", "noshade", "nowrap", "open", "progress", "prompt",
            "readonly", "rel", "rev", "role", "rows", "rowspan", "rules", "scope",
            "selected", "shape", "size", "span", "start", "summary", "tabindex",
            "title", "type", "usemap", "valign", "value", "width", "itemprop",
            // GitHub's per-element attributes (applied globally)
            "href", "src", "longdesc", "loading", "srcset", "itemscope", "itemtype",
            // NuGet additions
            "class"
        };

        private void ConfigureSanitizer()
        {
            // Replace the HtmlSanitizer's permissive defaults with our GitHub-modeled allowlists.
            _htmlSanitizer.AllowedTags.Clear();
            _htmlSanitizer.AllowedTags.UnionWith(AllowedTagsList);

            _htmlSanitizer.AllowedAttributes.Clear();
            _htmlSanitizer.AllowedAttributes.UnionWith(AllowedAttributesList);

            // No style attribute means no CSS processing is needed.
            _htmlSanitizer.AllowedCssProperties.Clear();

            // Allow Markdig task list checkboxes through while stripping all other inputs.
            // GitHub handles this via a post-sanitization filter; we use RemovingTag since
            // Ganss.Xss doesn't support multi-stage pipelines.
            _htmlSanitizer.RemovingTag += (sender, args) =>
            {
                if (args.Tag is IHtmlInputElement input
                    && string.Equals(input.Type, "checkbox", StringComparison.OrdinalIgnoreCase)
                    && input.IsDisabled)
                {
                    args.Cancel = true;
                }
            };

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
                .UseBootstrap();

            if (!_features.IsHtmlInMarkdownEnabled())
            {
                pipeline.DisableHtml();
            }

            var builtPipeline = pipeline.Build();

            using (var htmlWriter = new StringWriter())
            {
                var renderer = new HtmlRenderer(htmlWriter);
                builtPipeline.Setup(renderer);

                var document = Markdown.Parse(markdownWithoutBom, builtPipeline);

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
