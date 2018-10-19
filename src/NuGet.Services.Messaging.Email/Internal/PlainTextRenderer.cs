// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Markdig.Renderers;
using Markdig.Renderers.Normalize;
using Markdig.Renderers.Normalize.Inlines;
using Markdig.Syntax;

namespace NuGet.Services.Messaging.Email.Internal
{
    /// <summary>
    /// A Plain Text renderer.
    /// </summary>
    internal class PlainTextRenderer : NormalizeRenderer
    {
        public PlainTextRenderer(TextWriter writer)
            : base(writer)
        {
            writer.NewLine = "\r\n";

            // Replace some default inline renderers to work-around plain-text conversion bugs in Markdig.Markdown.ToPlainText().
            ObjectRenderers.Replace<AutolinkInlineRenderer>(new PlainTextAutoLinkInlineRenderer());
            ObjectRenderers.Replace<EmphasisInlineRenderer>(new PlainTextEmphasisInlineRenderer());
            ObjectRenderers.Replace<LinkInlineRenderer>(new PlainTextLinkInlineRenderer());
        }
    }

    /// <summary>
    /// A base class for Plain Text rendering <see cref="Block"/> and <see cref="Markdig.Syntax.Inlines.Inline"/> Markdown objects.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    internal abstract class PlainTextRenderer<TObject>
        : MarkdownObjectRenderer<PlainTextRenderer, TObject> where TObject : MarkdownObject
    {
    }
}
