using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.Operations.Model
{
    public class AzureStorageResource : Resource
    {
        public static readonly string ElementName = "azureStorage";

        public string AccountName { get; set; }
    }
}
