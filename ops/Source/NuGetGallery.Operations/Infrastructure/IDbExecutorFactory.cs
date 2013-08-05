using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnglicanGeek.DbExecutor;

namespace NuGetGallery.Operations.Infrastructure
{
    public interface IDbExecutorFactory
    {
        IDbExecutor OpenConnection(string connectionString);
    }
}
