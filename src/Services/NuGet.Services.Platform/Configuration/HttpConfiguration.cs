using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace NuGet.Services.Configuration
{
    public class HttpConfiguration
    {
        [Description("The base path to host the application at")]
        public string BasePath { get; set; }

        [Description("The admin password used by external services")]
        public string AdminKey { get; set; }
    }
}
