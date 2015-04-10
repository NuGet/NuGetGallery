using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGet.Services.Publish
{
    public class ListValidationResult : ValidationResult
    {
        public bool Listed { get; set; }
    }
}