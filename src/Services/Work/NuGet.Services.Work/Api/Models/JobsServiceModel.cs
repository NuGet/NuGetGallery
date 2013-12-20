using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs.Api.Models
{
    public class JobsServiceModel
    {
        public IEnumerable<JobDefinitionModel> Jobs { get; private set; }

        public JobsServiceModel() { }
        public JobsServiceModel(IEnumerable<JobDescription> jobs)
        {
            Jobs = jobs.Select(j => new JobDefinitionModel(j));
        }
    }
}
