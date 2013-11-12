using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class CamelCaseFilter : TokenFilter
    {
        ITermAttribute _termAttribute;
        IOffsetAttribute _offsetAttribute;
        Queue<string> _queue;
        int _startOffset;
        int _endOffset;

        public CamelCaseFilter(TokenStream stream)
            : base(stream)
        {
            _termAttribute = AddAttribute<ITermAttribute>();
            _offsetAttribute = AddAttribute<IOffsetAttribute>();
            _queue = new Queue<string>();
        }

        public override bool IncrementToken()
        {
            if (_queue.Count > 0)
            {
                string next = _queue.Dequeue();
                _termAttribute.SetTermBuffer(next);
                _startOffset = _endOffset;
                _endOffset = _startOffset + next.Length;
                _offsetAttribute.SetOffset(_startOffset, _endOffset);
                return true;
            }

            if (!input.IncrementToken())
            {
                return false;
            }

            string term = _termAttribute.Term;

            _startOffset = _offsetAttribute.StartOffset;

            foreach (string subTerm in TokenizingHelper.CamelCaseSplit(term))
            {
                _queue.Enqueue(subTerm);
            }

            if (_queue.Count > 1)
            {
                _termAttribute.SetTermBuffer(term);
                _offsetAttribute.SetOffset(_startOffset, _startOffset + term.Length);
                _endOffset = _startOffset;
                return true;
            }

            if (_queue.Count > 0)
            {
                string next = _queue.Dequeue();
                _termAttribute.SetTermBuffer(next);
                _endOffset = _startOffset + next.Length;
                _offsetAttribute.SetOffset(_startOffset, _endOffset);
                return true;
            }

            return false;
        }
    }
}
