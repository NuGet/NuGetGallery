using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.Operations.Model
{
    public class AzureRoleService : Service
    {
        public static readonly string ElementName = "azureRole";

        public string CloudServiceName { get; set; }
    }
}
