// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Search;

namespace NuGet.Indexing
{
    public class CustomSimilarity : DefaultSimilarity
    {
        public override float LengthNorm(string fieldName, int numTerms)
        {
            if (fieldName == "TokenizedId" || fieldName == "ShingledId" || fieldName == "Owners" || fieldName == "Title")
            {
                return 1;
            }
            else if (fieldName == "Tags" && numTerms <= 15)
            {
                return 1;
            }
            else
            {
                return base.LengthNorm(fieldName, numTerms);
            }
        }
    }
}
