using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs
{
    public class JobDefinition
    {
        public Type Implementation { get; private set; }
        public JobDescription Description { get; private set; }

        public JobDefinition(JobDescription description, Type implementation)
        {
            Description = description;
            Implementation = implementation;
        }
    }
}
