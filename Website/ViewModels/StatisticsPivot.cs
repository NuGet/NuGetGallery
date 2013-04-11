
using System;
using System.Collections.Generic;
using System.Globalization;

//  The implementation here is generic with respect to the specific properties in the data
//  if can produce any pivot from any data. The result of the pivot is a 2-d array because
//  that is easier to product html tables from. The intermediary data structure is decorated is
//  decorated with sub-totals and sub-counts at every level. The sub-counts are there to simplify
//  the creation of html table rowspans.

namespace NuGetGallery
{
    public class StatisticsPivot
    {
        public static Tuple<TableEntry[][], int> GroupBy(IList<StatisticsFact> facts, string[] pivot)
        {
            //  firstly take the facts and the pivot vector and produce a tree structure

            Level level = InnerGroupBy(facts, pivot);

            //  secondly print this tree structure into a sparse 2-d array (for creating html tables)

            TableEntry[][] table = new TableEntry[level.Count][];
            for (int i = 0; i < level.Count; i++)
            {
                table[i] = new TableEntry[pivot.Length + 1];
            }

            PopulateTable(level, table);

            return new Tuple<TableEntry[][], int>(table, level.Total);
        }

        public static int Total(IList<StatisticsFact> facts)
        {
            int total = 0;
            foreach (StatisticsFact fact in facts)
            {
                total += fact.Amount;
            }
            return total;
        }

        private static void InnerPopulateTable(Level level, TableEntry[][] table, ref int row, int col)
        {
            foreach (KeyValuePair<string, Level> item in level.Next)
            {
                if (item.Value.Next == null)
                {
                    table[row][col] = new TableEntry { Data = item.Key };
                    table[row][col + 1] = new TableEntry { Data = item.Value.Amount.ToString(CultureInfo.InvariantCulture) };
                    row++;
                }
                else
                {
                    table[row][col] = new TableEntry { Data = item.Key, Rowspan = item.Value.Count };
                    InnerPopulateTable(item.Value, table, ref row, col + 1);
                }
            }
        }

        private static void PopulateTable(Level level, TableEntry[][] table)
        {
            int row = 0;
            InnerPopulateTable(level, table, ref row, 0);
        }

        private static Level InnerGroupBy(IList<StatisticsFact> facts, string[] groupBy)
        {
            Level result = new Level(new Dictionary<string, Level>());

            foreach (StatisticsFact fact in facts)
            {
                Expand(result, fact, groupBy, 0);
                Assign(result, fact, groupBy, 0);
            }

            Count(result);
            Total(result);

            return result;
        }

        private static void Expand(Level result, StatisticsFact fact, string[] dimensions, int depth)
        {
            if (depth == dimensions.Length)
            {
                return;
            }

            if (!result.Next.ContainsKey(fact.Dimensions[dimensions[depth]]))
            {
                if (depth == dimensions.Length - 1)
                {
                    result.Next[fact.Dimensions[dimensions[depth]]] = new Level(0);
                    return;
                }
                else
                {
                    result.Next[fact.Dimensions[dimensions[depth]]] = new Level(new Dictionary<string, Level>());
                }
            }

            Expand(result.Next[fact.Dimensions[dimensions[depth]]], fact, dimensions, depth + 1);
        }

        private static void Assign(Level result, StatisticsFact fact, string[] dimensions, int depth)
        {
            if (depth == dimensions.Length)
            {
                return;
            }

            if (depth == dimensions.Length - 1)
            {
                int current = result.Next[fact.Dimensions[dimensions[depth]]].Amount;

                result.Next[fact.Dimensions[dimensions[depth]]].Amount = current + fact.Amount;
            }
            else
            {
                Assign(result.Next[fact.Dimensions[dimensions[depth]]], fact, dimensions, depth + 1);
            }
        }

        private static int Count(Level level)
        {
            int count = 0;
            foreach (KeyValuePair<string, Level> item in level.Next)
            {
                if (item.Value.Next == null)
                {
                    count++;
                }
                else
                {
                    count += Count(item.Value);
                }
            }

            level.Count = count;

            return count;
        }

        private static int Total(Level level)
        {
            int total = 0;
            foreach (KeyValuePair<string, Level> item in level.Next)
            {
                if (item.Value.Next == null)
                {
                    total += item.Value.Amount;
                }
                else
                {
                    total += Total(item.Value);
                }
            }

            level.Total = total;

            return total;
        }

        public class TableEntry
        {
            public string Data { get; set; }
            public int Rowspan { get; set; }
            public string Uri { get; set; }
        }

        private class Level
        {
            public Level(int amount)
            {
                Amount = amount;
            }

            public Level(IDictionary<string, Level> next)
            {
                Next = next;
            }

            public IDictionary<string, Level> Next { get; set; }
            public int Amount { get; set; }
            public int Count { get; set; }
            public int Total { get; set; }
        }
    }
}
