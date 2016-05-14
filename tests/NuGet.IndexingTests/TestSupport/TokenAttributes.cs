// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.IndexingTests.TestSupport
{
    public class TokenAttributes
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="term">The value extracted.</param>
        /// <param name="startOffset">Starting position of the token in the original input string, that was used to extract the term.</param>
        /// <param name="endOffset">End position of the token in the original input string, that was used to extract the term.</param>
        /// <param name="positionIncrement">Need to expand on the meaning of this.</param>
        public TokenAttributes(string term, int startOffset, int endOffset, int? positionIncrement = null)
        {
            Term = term;
            StartOffset = startOffset;
            EndOffset = endOffset;
            PositionIncrement = positionIncrement;
        }

        public string Term { get; set; }
        public int StartOffset { get; set; }
        public int EndOffset { get; set; }
        public int? PositionIncrement { get; set; }

        public override string ToString()
        {
            return
                "{" +
                $"Term: '{Term}', " +
                $"Offset: ({StartOffset}, {EndOffset}), " +
                $"PositionIncrement: {PositionIncrement?.ToString() ?? "null"}" +
                "}";
        }

        public override bool Equals(object obj)
        {
            var other = obj as TokenAttributes;

            if (other == null)
            {
                return false;
            }

            return string.Equals(Term, other.Term) &&
                StartOffset == other.StartOffset &&
                EndOffset == other.EndOffset &
                PositionIncrement == other.PositionIncrement;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }
}