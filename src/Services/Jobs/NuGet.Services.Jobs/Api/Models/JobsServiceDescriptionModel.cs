using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs.Api.Models
{
    public class JobsServiceDescriptionModel
    {
        public IEnumerable<JobDefinitionModel> Jobs { get; private set; }

        public JobsServiceDescriptionModel() { }
        public JobsServiceDescriptionModel(IEnumerable<JobDescription> jobs)
        {
            Jobs = jobs.Select(j => new JobDefinitionModel(j));
        }
    }
}
