// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using System;
using System.Collections.Generic;

namespace Ng
{
    public static class ResetLucene
    {
        static void PrintUsage()
        {
            Console.WriteLine("Usage: ng clearlucene -luceneDirectoryType file|azure [-lucenePath <file-path>] | [-luceneStorageAccountName <azure-acc> -luceneStorageKeyValue <azure-key> -luceneStorageContainer <azure-container>]");
        }

        public static void Run(string[] args)
        {
            IDictionary<string, string> arguments = CommandHelpers.GetArguments(args, 1);
            if (arguments == null)
            {
                PrintUsage();
                return;
            }

            Lucene.Net.Store.Directory directory = CommandHelpers.GetLuceneDirectory(arguments);
            if (directory == null)
            {
                PrintUsage();
                return;
            }

            if (IndexReader.IndexExists(directory))
            {
                using (IndexWriter writer = new IndexWriter(directory, new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30), true, IndexWriter.MaxFieldLength.UNLIMITED))
                {
                    writer.DeleteAll();
                    writer.Commit(new Dictionary<string, string>());
                }
            }

            Console.WriteLine("All Done");
        }
    }
}
