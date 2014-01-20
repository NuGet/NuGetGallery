using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Models
{
    public enum InvocationListCriteria
    {
        All,
        Active,
        Completed,
        Executing,
        Pending,
        Hidden,
        Suspended
    }
}
