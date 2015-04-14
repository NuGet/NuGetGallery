using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGet.Services.Publish
{
    public class EditValidationResult : ValidationResult
    {
        public bool Listed { get; set; }
        public JObject EditMetadata { get; set; }
        public JObject CatalogEntry { get; set; }
    }
}