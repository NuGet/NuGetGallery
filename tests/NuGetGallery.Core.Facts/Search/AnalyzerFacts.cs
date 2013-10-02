using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using System;
using System.IO;
using Xunit.Extensions;

namespace NuGetGallery.Search
{
    public class AnalyzerFacts
    {
        //  identifier

        [Theory]
        [InlineData("", new string[] { })]
        [InlineData("Aluminium", new [] { "aluminium" })]
        [InlineData("ALUMINIUM", new[] { "aluminium" })]
        [InlineData("Microsoft.Data.EntityFramework", new [] { "microsoft", "data", "entity", "framework" })]
        [InlineData("EntityFramework", new [] { "entity", "framework" })]
        [InlineData("DotNetRdf", new [] { "dot", "net", "rdf" })]
        [InlineData("dotNetRDF.Data.Virtuoso", new [] { "dot", "net", "rdf", "data", "virtuoso" })]
        [InlineData("dotNetRDF.Query.FullText", new [] { "dot", "net", "rdf", "query", "full", "text" })]
        [InlineData("dotNetRDF.Data.Sql", new [] { "dot", "net", "rdf", "data", "sql" })]
        [InlineData("ServiceStack.Text", new [] { "service", "stack", "text" })]
        [InlineData("ServiceStack Text", new [] { "service", "stack", "text" })]
        [InlineData("300-B", new[] { "300", "b" })]
        [InlineData("300B", new[] { "300b" })]
        [InlineData("6SN7", new[] { "6sn7" })]
        [InlineData("6sl7", new[] { "6sl7" })]
        [InlineData("6922", new[] { "6922" })]
        [InlineData("5U4", new[] { "5u4" })]
        [InlineData("KT-88", new[] { "kt", "88" })]
        [InlineData("Hello6SN7", new[] { "hello6sn7" })]
        public void TestIdentifier(string input, string[] outputs)
        {
            TestAnalyzer(new IdentifierAnalyzer(), input, outputs);
        }

        //  description

        [Theory]
        [InlineData("C++ JSON Parser", new[] { "c++", "json", "parser" })]
        [InlineData("C# is a Programming Langauge", new[] { "c#", "programming", "langauge" })]
        [InlineData("My C++ project. JSON Parser", new[] { "my", "c++", "project", "json", "parser" })]
        [InlineData("Cobol PIC", new[] { "cobol", "pic" })]
        [InlineData("Secret Chiefs 3", new[] { "secret", "chiefs", "3" })]
        [InlineData("Where black is the color, where none is the number", new[] { "where", "black", "color", "where", "none", "number" })]
        [InlineData("Highway 61; Revisited", new[] { "highway", "61", "revisited" })]
        [InlineData("This is my package that does a bunch of interesting stuff.", new[] { "my", "package", "does", "bunch", "interesting", "stuff" })]
        [InlineData("C++ is a wonderful Programming Langauge", new[] { "c++", "wonderful", "programming", "langauge" })]
        [InlineData("It would be nice, if we removed some more punctuation! The trick is leaving stuff we care about like ++ or #. It's that OK?", new[] { "would", "nice", "removed", "some", "more", "punctuation!", "trick", "leaving", "stuff", "care", "about", "like", "++", "#", "ok?" })]
        public void TestDescription(string input, string[] outputs)
        {
            TestAnalyzer(new DescriptionAnalyzer(), input, outputs);
        }

        [Theory]
        [InlineData("C++ C# F# JSON Parser serializer XML", new[] { "c++", "c#", "f#", "json", "parser", "serializer", "xml" })]
        [InlineData("C++, C#, F#, JSON, Parser, serializer, XML", new[] { "c++", "c#", "f#", "json", "parser", "serializer", "xml" })]
        public void TestTags(string input, string[] outputs)
        {
            TestAnalyzer(new TagsAnalyzer(), input, outputs);
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
