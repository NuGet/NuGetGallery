// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.IndexingTests.TestSupport
{
    public class TokenAttributes
    {
        public TokenAttributes()
        {
        }

        public TokenAttributes(string term, int startOffset, int endOffset) : this(term, startOffset, endOffset, null)
        {
        }

        public TokenAttributes(string term, int startOffset, int endOffset, int? positionIncrement)
        {
            this.Term = term;
            this.StartOffset = startOffset;
            this.EndOffset = endOffset;
            this.PositionIncrement = positionIncrement;
        }

        public string Term { get; set; }
        public int StartOffset { get; set; }
        public int EndOffset { get; set; }
        public int? PositionIncrement { get; set; }

        public override string ToString()
        {
            return string.Format(
                "{{Term: '{0}', Offset: ({1}, {2}), PositionIncrement: {3}}}",
                this.Term,
                this.StartOffset,
                this.EndOffset,
                this.PositionIncrement.HasValue ? this.PositionIncrement.Value.ToString() : "null");
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
            return this.ToString().GetHashCode();
        }
    }
}