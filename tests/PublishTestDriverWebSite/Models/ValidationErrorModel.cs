using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PublishTestDriverWebSite.Models
{
    public class ValidationErrorModel
    {
        public ValidationErrorModel(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }
    }
}