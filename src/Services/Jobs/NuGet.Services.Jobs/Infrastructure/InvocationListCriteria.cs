using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs
{
    public enum InvocationListCriteria
    {
        All,
        Active,
        Completed,
        Pending,
        Hidden,
        Suspended
    }
}
