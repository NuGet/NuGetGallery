using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Resolver.Metadata
{
    public class SemanticVersionSpan
    {
        public enum SpanType { MaxMajorSpan, MinMajorSpan, MaxMinorSpan, Specified };

        public static SemanticVersionSpan MaxMajor = new SemanticVersionSpan(SpanType.MaxMajorSpan);
        public static SemanticVersionSpan MinMajor = new SemanticVersionSpan(SpanType.MinMajorSpan);
        public static SemanticVersionSpan MaxMinor = new SemanticVersionSpan(SpanType.MaxMinorSpan);

        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Patch { get; private set; }

        public bool MajorExcluding { get; private set; }
        public bool MinorExcluding { get; private set; }

        public SpanType Span { get; private set; }

        private SemanticVersionSpan(SpanType spanType)
        {
            Span = spanType;
        }

        private SemanticVersionSpan(int major, int minor, int patch)
            : this(major, minor, patch, false, false)
        {
            Span = SpanType.Specified;
        }

        private SemanticVersionSpan(int major, int minor, int patch, bool majorExcluding, bool minorExcluding)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        //TODO: using 999 is the wrong idea - we need an explicit notion of max

        public static SemanticVersionSpan FromMajor(int major)
        {
            return new SemanticVersionSpan(major, 999, 999);
        }
        
        public static SemanticVersionSpan FromMinor(int minor)
        {
            return new SemanticVersionSpan(0, minor, 999);
        }

        public static SemanticVersionSpan FromPatch(int patch)
        {
            return new SemanticVersionSpan(0, 0, patch);
        }

        public static SemanticVersionSpan FromMajorExcluding(int major)
        {
            return new SemanticVersionSpan(major, 0, 0, true, false);
        }

        public static SemanticVersionSpan FromMinorExcluding(int minor)
        {
            return new SemanticVersionSpan(0, minor, 0, false, true);
        }
    }
}
