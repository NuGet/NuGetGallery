// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Markdig;
using NuGet.Services.Messaging.Email.Internal;

namespace NuGet.Services.Messaging.Email
{
    /// <summary>
    /// Abstract base class for building email messages relying on Markdown syntax.
    /// </summary>
    public abstract class MarkdownEmailBuilder : EmailBuilder
    {
        protected override string GetPlainTextBody()
        {
            var markdown = GetMarkdownBody();

            var writer = new StringWriter();
            var pipeline = new MarkdownPipelineBuilder().Build();

            // We override the renderer with our own writer
            var renderer = new PlainTextRenderer(writer);

            pipeline.Setup(renderer);

            var document = Markdown.Parse(markdown, pipeline);
            renderer.Render(document);
            writer.Flush();

            return writer.ToString();
        }
    }
}