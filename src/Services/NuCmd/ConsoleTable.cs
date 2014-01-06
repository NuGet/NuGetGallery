using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace NuCmd
{
    public class ConsoleTable
    {
        public IList<string> Columns { get; private set; }
        public IList<ConsoleTableRow> Rows { get; private set; }

        public ConsoleTable(IEnumerable<string> columns, IEnumerable<IEnumerable<string>> rowData)
            : this(columns, rowData.Select(r => new ConsoleTableRow(r)))
        {

        }

        public ConsoleTable(IEnumerable<string> columns, IEnumerable<ConsoleTableRow> rows)
        {
            Columns = columns.ToList();
            Rows = rows.ToList();
        }

        public static ConsoleTable For<T>(IEnumerable<T> items, params Expression<Func<T, object>>[] columns)
        {
            var columnNames = columns.Select(expr =>
            {
                MemberExpression member = null;
                if (expr.Body.NodeType == ExpressionType.Convert)
                {
                    var unary = expr.Body as UnaryExpression;
                    member = unary.Operand as MemberExpression;
                }
                else
                {
                    member = expr.Body as MemberExpression;
                }

                if (member == null)
                {
                    return String.Empty;
                }
                return member.Member.Name;
            });

            var compiled = columns.Select(e => e.Compile());

            var data = items.Select(i => compiled.Select(e => e(i).ToString()));

            return new ConsoleTable(columnNames, data);
        }

        public string GetHeader()
        {
            var maxes = CalcuateMaxes();

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < Columns.Count; i++)
            {
                builder.Append(" ");
                builder.Append(Columns[i].PadRight(maxes[i]));
                builder.Append(" ");
            }
            return builder.ToString();
        }

        public IEnumerable<string> GetRows()
        {
            var maxes = CalcuateMaxes();
            foreach(var row in Rows) 
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

        private int[] CalcuateMaxes()
        {
            var maxes = Columns.Select(name => name.Length);
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

    public class ConsoleTableRow
    {
        public IList<string> Cells { get; private set; }

        public ConsoleTableRow(IEnumerable<string> cellData)
        {
            Cells = cellData.ToList();
        }
    }
}
