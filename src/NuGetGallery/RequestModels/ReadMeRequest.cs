using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.RequestModels
{
    public class ReadMeRequest
    {
        public HttpPostedFileBase ReadMeFile { get; set; }
        public string ReadMeWritten { get; set; }
        public string ReadMeUrl { get; set; }
    }
}