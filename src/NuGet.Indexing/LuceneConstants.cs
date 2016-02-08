// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Indexing
{
    public class LuceneConstants
    {
        //  Define the size of a file in a level (exponentially) and the count of files that constitue a level
        public const int MergeFactor = 10;

        //  Except never merge segments that have more docs than this
        public const int MaxMergeDocs = 7999;              
    }
}