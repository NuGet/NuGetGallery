// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.CDNLogsSanitizer
{
    public class LogHeaderMetadata
    {
        public string Header { get; }

        public char Delimiter { get; }

        public LogHeaderMetadata(string header, char delimiter)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Delimiter = delimiter;
        }

        public int? GetIndex(string value)
        {
            return Header.Split(Delimiter).GetFirstIndex(value);
        }
    }
}
