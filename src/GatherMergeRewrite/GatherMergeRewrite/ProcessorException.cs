using System;
using System.Collections.Generic;
using System.IO;
using VDS.RDF;

namespace GatherMergeRewrite
{
    class ProcessorException : Exception
    {
        public State State { get; private set; }

        public ProcessorException(State state, string message, Exception exception)
            : base(message, exception)
        {
            State = state;
        }

        public void WriteTo(TextWriter writer)
        {
            writer.WriteLine("resources:");
            foreach (KeyValuePair<Uri, Tuple<string, string>> item in State.Resources)
            {
                writer.WriteLine("{0} {1} {2}", item.Key, item.Value.Item1, item.Value.Item2);
            }

            writer.WriteLine("store:");
            foreach (Triple triple in State.Store.Triples)
            {
                writer.WriteLine("{0} {1} {2}", triple.Subject, triple.Predicate, triple.Object);
            }
        }
    }
}
