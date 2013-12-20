using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.Services.Http.Models;

namespace NuGet.Services.Jobs.Api.Models
{
    public class JobDefinitionModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Runtime { get; set; }
        public AssemblyInformationModel Assembly { get; set; }
        public Guid? EventProviderId { get; set; }
        public bool? Enabled { get; set; }

        public JobDefinitionModel() { }
        public JobDefinitionModel(JobDescription jobdef)
        {
            Name = jobdef.Name;
            Description = jobdef.Description;
            Runtime = jobdef.Runtime;
            Assembly = new AssemblyInformationModel(jobdef.Assembly);
            EventProviderId = jobdef.EventProviderId;
            Enabled = jobdef.Enabled;
        }
    }
}
