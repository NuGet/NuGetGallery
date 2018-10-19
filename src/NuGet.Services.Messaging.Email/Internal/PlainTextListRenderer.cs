// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Markdig.Syntax;

namespace NuGet.Services.Messaging.Email.Internal
{
    /// <summary>
    /// A Plain Text renderer for a <see cref="ListBlock"/>.
    /// </summary>
    internal class PlainTextListRenderer : PlainTextRenderer<ListBlock>
    {
        protected override void Write(PlainTextRenderer renderer, ListBlock listBlock)
        {
            renderer.EnsureLine();

            foreach (var item in listBlock)
            {
                renderer.EnsureLine();

                renderer.WriteChildren((ListItemBlock)item);
            }
        }
    }
}
