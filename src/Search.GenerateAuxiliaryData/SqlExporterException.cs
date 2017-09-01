using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Search.GenerateAuxiliaryData
{
    public class SqlExporterException : Exception
    {
        public SqlExporterException(string message)
            : base(message)
        {
        }
    }
}
