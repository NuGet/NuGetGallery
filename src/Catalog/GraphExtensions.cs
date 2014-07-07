using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VDS.RDF
{
    public static class GraphExtensions
    {
        public static void Assert(this IGraph self, INode subject, Uri predicate, string value)
        {
            Assert(self, subject, predicate, value, null);
        }

        public static void Assert(this IGraph self, INode subject, Uri predicate, Uri value)
        {
            Assert(self, subject, predicate, value.ToString());
        }

        public static void Assert(this IGraph self, INode subject, Uri predicate, string value, Uri dataType)
        {
            Assert(self, subject, predicate, self.CreateLiteralNode(value.ToString(), dataType));
        }

        public static void Assert(this IGraph self, INode subject, Uri predicate, INode value)
        {
            self.Assert(subject, self.CreateUriNode(predicate), value);
        }
    }
}