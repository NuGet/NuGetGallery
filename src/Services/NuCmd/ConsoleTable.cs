using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NuCmd
{
    public class ConsoleTable
    {
        public IList<ConsoleTableColumn> Columns { get; private set; }
        public IList<ConsoleTableRow> Rows { get; private set; }

        public ConsoleTable(IEnumerable<ConsoleTableColumn> columns, IEnumerable<IEnumerable<string>> rowData)
            : this(columns, rowData.Select(r => new ConsoleTableRow(r)))
        {

        }

        public ConsoleTable(IEnumerable<ConsoleTableColumn> columns, IEnumerable<ConsoleTableRow> rows)
        {
            Columns = columns.ToList();
            Rows = rows.ToList();
        }

        public static ConsoleTable For<T>(IEnumerable<T> items, Func<T, object> selector)
        {
            // Build the rows
            var data = items.Select(selector).ToList();

            if (!data.Any())
            {
                return new ConsoleTable(Enumerable.Empty<ConsoleTableColumn>(), Enumerable.Empty<ConsoleTableRow>());
            }
            else
            {
                var columns = data
                    .First()
                    .GetType()
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Select(p => new ConsoleTableColumn(p.Name, p))
                    .ToList();

                var rows = data.Select(d => new ConsoleTableRow(
                    columns.Select(c => {
                        var val = c.Property.GetValue(d);
                        return val == null ? "<<null>>" : val.ToString();
                    })));

                return new ConsoleTable(columns, rows);
            }
        }

        public string GetHeader()
        {
            if (!Columns.Any())
            {
                return "<< Empty >>";
            }

            var maxes = CalcuateMaxes();

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < Columns.Count; i++)
            {
                builder.Append(" ");
                builder.Append(Columns[i].Name.PadRight(maxes[i]));
                builder.Append(" ");
            }
            builder.AppendLine();
            for (int i = 0; i < Columns.Count; i++)
            {
                builder.Append(" ");
                builder.Append(new String('-', maxes[i]));
                builder.Append(" ");
            }
            return builder.ToString();
        }

        public IEnumerable<string> GetRows()
        {
            if (Rows.Any())
            {
                var maxes = CalcuateMaxes();
                foreach (var row in Rows)
                {
                    StringBuilder builder = new StringBuilder();
                    for (int i = 0; i < Columns.Count; i++)
                    {
                        builder.Append(" ");
                        builder.Append(row.Cells[i].PadRight(maxes[i]));
                        builder.Append(" ");
                    }
                    yield return builder.ToString();
                }
            }
        }

        private int[] CalcuateMaxes()
        {
            var maxes = Columns.Select(col => col.Name.Length);
            if (Rows.Any())
            {
                maxes = Enumerable.Zip(
                    maxes,
                    Rows
                        .Select(r => r.Cells.Select(d => d.Length))
                        .Aggregate((row1, row2) =>
                            row1.Zip(row2, Math.Max)),
                    Math.Max);
            }
            var maxesArray = maxes.ToArray();
            return maxesArray;
        }
    }

    public class ConsoleTableColumn
    {
        public string Name { get; private set; }
        public PropertyInfo Property { get; private set; }

        public ConsoleTableColumn(string name, PropertyInfo property)
        {
            Name = name;
            Property = property;
        }
    }

    public class ConsoleTableRow
    {
        public IList<string> Cells { get; private set; }

        public ConsoleTableRow(IEnumerable<string> cellData)
        {
            Cells = cellData.ToList();
        }
    }
}
