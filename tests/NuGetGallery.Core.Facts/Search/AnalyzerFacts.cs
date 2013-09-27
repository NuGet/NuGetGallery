using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using System;
using System.IO;
using Xunit.Extensions;

namespace NuGetGallery.Search
{
    public class AnalyzerFacts
    {
        [Theory]

        //  identifier

        [InlineData("", new string [] {})]
        [InlineData("Aluminium", new [] { "Aluminium" })]
        [InlineData("ALUMINIUM", new [] { "ALUMINIUM" })]
        [InlineData("Microsoft.Data.EntityFramework", new [] { "Microsoft", "Data", "Entity", "Framework" })]
        [InlineData("EntityFramework", new [] { "Entity", "Framework" })]
        [InlineData("DotNetRdf", new [] { "Dot", "Net", "Rdf" })]
        [InlineData("dotNetRDF.Data.Virtuoso", new [] { "dot", "Net", "RDF", "Data", "Virtuoso" })]
        [InlineData("dotNetRDF.Query.FullText", new [] { "dot", "Net", "RDF", "Query", "Full", "Text" })]
        [InlineData("dotNetRDF.Data.Sql", new [] { "dot", "Net", "RDF", "Data", "Sql" })]
        [InlineData("ServiceStack.Text", new [] { "Service", "Stack", "Text" })]
        [InlineData("ServiceStack Text", new [] { "Service", "Stack", "Text" })]

        //  titles
        //  - some of these strings suggest we should use a different analyzers for Title
        //  - for example one that drops stops words and commas
        //  - consider camelcase filter over standard tokenization

        [InlineData("C++ JSON Parser", new [] { "C++", "JSON", "Parser" })]
        [InlineData("C# is a Programming Langauge", new [] { "C#", "is", "a", "Programming", "Langauge" })]
        [InlineData("My C++ project. JSON Parser", new [] { "My", "C++", "project", "JSON", "Parser" })]
        [InlineData("Cobol PIC", new [] { "Cobol", "PIC" })]
        [InlineData("Secret Chiefs 3", new [] { "Secret", "Chiefs", "3" })]
        [InlineData("300-B", new [] { "300", "B" })]
        [InlineData("300B", new [] { "300B" })]
        [InlineData("6SN7", new [] { "6SN7" })]
        [InlineData("6sl7", new [] { "6sl7" })]
        [InlineData("6922", new [] { "6922" })]
        [InlineData("5U4", new [] { "5U4" })]
        [InlineData("KT-88", new [] { "KT", "88" })]
        [InlineData("Where black is the color, where none is the number", new [] { "Where", "black", "is", "the", "color,", "where", "none", "is", "the", "number" })]
        public void TestIdentifier(string input, string[] outputs)
        {
            TestAnalyzer(new IdentifierAnalyzer(), input, outputs);
        }

        private static void TestAnalyzer(Analyzer analyzer, string input, params string[] tokens)
        {
            bool verbose = true;

            if (verbose)
            {
                Console.WriteLine(input);
            }

            using (TextReader reader = new StringReader(input))
            {
                TokenStream tokenStream = analyzer.TokenStream("Test", reader);

                tokenStream.Reset();

                ITermAttribute termAttribute = tokenStream.AddAttribute<ITermAttribute>();
                IOffsetAttribute offsetAttribute = tokenStream.AddAttribute<IOffsetAttribute>();
                IPositionIncrementAttribute positionIncrementAttribute = tokenStream.AddAttribute<IPositionIncrementAttribute>();
                ITypeAttribute typeAttribute = tokenStream.AddAttribute<ITypeAttribute>();

                int i = 0;

                while (tokenStream.IncrementToken())
                {
                    string term = termAttribute.Term;

                    if (i < tokens.Length && tokens[i] != term)
                    {
                        throw new Exception(string.Format("expected [{0}] got [{1}]", tokens[i], term));
                    }

                    i++;

                    if (verbose)
                    {
                        Console.WriteLine("\tTerm: {0}, Offset Start: {1}, End: {2}, PositionIncrement: {3}, Type: {4}",
                          termAttribute.Term,
                          offsetAttribute.StartOffset, offsetAttribute.EndOffset,
                          positionIncrementAttribute.PositionIncrement,
                          typeAttribute.Type);
                    }
                }

                if (verbose)
                {
                    Console.WriteLine();
                }

                if (i != tokens.Length)
                {
                    throw new Exception(string.Format("expected {0} tokens but got {1}", tokens.Length, i));
                }
            }
        }
    }
}
