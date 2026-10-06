// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class NamespaceReservationFoundryResponse
    {
        public string Text { get; }
        public long? InputTokens { get; }
        public long? OutputTokens { get; }

        public NamespaceReservationFoundryResponse(string text, long? inputTokens, long? outputTokens)
        {
            Text = text;
            InputTokens = inputTokens;
            OutputTokens = outputTokens;
        }
    }
}