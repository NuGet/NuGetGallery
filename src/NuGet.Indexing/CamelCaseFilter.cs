// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;

namespace NuGet.Indexing
{
    public class CamelCaseFilter : TokenFilter
    {
        private readonly ITermAttribute _termAttribute;
        private readonly IOffsetAttribute _offsetAttribute;
        private readonly IPositionIncrementAttribute _positionIncrementAttribute;

        private readonly Queue<TokenAttributes> _queue = new Queue<TokenAttributes>();

        public CamelCaseFilter(TokenStream stream)
            : base(stream)
        {
            _termAttribute = AddAttribute<ITermAttribute>();
            _offsetAttribute = AddAttribute<IOffsetAttribute>();
            _positionIncrementAttribute = AddAttribute<IPositionIncrementAttribute>();
        }

        public override bool IncrementToken()
        {
            if (_queue.Count > 0)
            {
                SetAttributes(_queue.Dequeue());
                return true;
            }

            if (!input.IncrementToken())
            {
                return false;
            }

            _queue.Enqueue(new TokenAttributes
            {
                TermBuffer = _termAttribute.Term,
                StartOffset = _offsetAttribute.StartOffset,
                EndOffset = _offsetAttribute.EndOffset,
                PositionIncrement = _positionIncrementAttribute.PositionIncrement
            });

            string term = _termAttribute.Term;
            int start = _offsetAttribute.StartOffset;
            int prevStart = start;
            int positionIncrement = 0;
            string prev = string.Empty;

            foreach (string subTerm in CamelCaseSplit(term))
            {
                if (prev != string.Empty)
                {
                    string shingle = string.Format("{0}{1}", prev, subTerm);

                    if (shingle != term)
                    {
                        _queue.Enqueue(new TokenAttributes
                        {
                            TermBuffer = shingle,
                            StartOffset = prevStart,
                            EndOffset = prevStart + shingle.Length,
                            PositionIncrement = 0
                        });
                    }
                }

                if (subTerm != term && !subTerm.Any(c => Char.IsNumber(c)))
                {
                    _queue.Enqueue(new TokenAttributes
                    {
                        TermBuffer = subTerm,
                        StartOffset = start,
                        EndOffset = start + subTerm.Length,
                        PositionIncrement = positionIncrement
                    });
                }

                positionIncrement = 1;
                prevStart = start;
                start += subTerm.Length;
                prev = subTerm;
            }

            if (_queue.Count > 0)
            {
                SetAttributes(_queue.Dequeue());
                return true;
            }

            return false;
        }

        public static IEnumerable<string> CamelCaseSplit(string term)
        {
            if (term.Length == 0)
            {
                yield break;
            }

            if (term.Length == 1)
            {
                yield return term;
                yield break;
            }

            int beginWordIndex = 0;
            int length = 1;
            bool lastIsUpper = Char.IsUpper(term[0]);
            bool lastIsLetter = Char.IsLetter(term[0]);

            for (int i = 1; i < term.Length; i++)
            {
                bool currentIsUpper = Char.IsUpper(term[i]);
                bool currentIsLetter = Char.IsLetter(term[i]);
                bool currentIsNumber = Char.IsNumber(term[i]);

                if ((lastIsLetter && currentIsLetter) && (!lastIsUpper && currentIsUpper) ||
                    (lastIsLetter == currentIsNumber))
                {
                    yield return term.Substring(beginWordIndex, length);
                    length = 0;
                    beginWordIndex = i;
                }

                length++;

                lastIsUpper = currentIsUpper;
                lastIsLetter = currentIsLetter;
            }

            yield return term.Substring(beginWordIndex, length);
        }

        private void SetAttributes(TokenAttributes next)
        {
            _termAttribute.SetTermBuffer(next.TermBuffer);
            _offsetAttribute.SetOffset(next.StartOffset, next.EndOffset);
            _positionIncrementAttribute.PositionIncrement = next.PositionIncrement;
        }

        private class TokenAttributes
        {
            public string TermBuffer { get; set; }
            public int StartOffset { get; set; }
            public int EndOffset { get; set; }
            public int PositionIncrement { get; set; }
        }
    }
}
