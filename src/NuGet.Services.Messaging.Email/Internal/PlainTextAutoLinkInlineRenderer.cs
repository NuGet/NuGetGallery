// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Markdig.Renderers.Normalize;
using Markdig.Syntax.Inlines;

namespace NuGet.Services.Messaging.Email.Internal
{
    /// <summary>
    /// A Plain Text renderer for a <see cref="LineBreakInline"/>.
    /// </summary>
    internal class PlainTextAutoLinkInlineRenderer : NormalizeObjectRenderer<AutolinkInline>
    {
        protected override void Write(NormalizeRenderer renderer, AutolinkInline obj)
        {
            renderer.Write(obj.Url);
        }
    }
}
