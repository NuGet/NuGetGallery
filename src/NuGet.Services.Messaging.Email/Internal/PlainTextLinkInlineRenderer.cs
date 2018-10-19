// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Markdig.Renderers.Normalize;
using Markdig.Syntax.Inlines;

namespace NuGet.Services.Messaging.Email.Internal
{
    /// <summary>
    /// A Plain Text renderer for a <see cref="LinkInline"/>.
    /// </summary>
    internal class PlainTextLinkInlineRenderer : NormalizeObjectRenderer<LinkInline>
    {
        protected override void Write(NormalizeRenderer renderer, LinkInline link)
        {
            if (link.IsImage)
            {
                return;
            }

            renderer.WriteChildren(link);

            if (link.Label != null)
            {
                renderer.Write(link.Label);
            }
            else if (!string.IsNullOrEmpty(link.Url) && !UrlMatchesChildInline(link))
            {
                renderer.Write(" (").Write(link.Url).Write(")");

                if (!string.IsNullOrEmpty(link.Title))
                {
                    renderer.Write(" \"");
                    renderer.Write(link.Title.Replace(@"""", @"\"""));
                    renderer.Write("\"");
                }
            }
        }

        /// <summary>
        /// For a given [text](url), checks whether the text value matches the url.
        /// </summary>
        private static bool UrlMatchesChildInline(LinkInline link)
        {
            if (link.FirstChild != null
                && link.FirstChild == link.LastChild
                && link.FirstChild is LiteralInline literalInline
                && string.Equals(link.Url, literalInline.Content.ToString()))
            {
                return true;
            }

            return false;
        }
    }
}
