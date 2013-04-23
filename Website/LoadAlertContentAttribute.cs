using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class LoadAlertContentAttribute : FilterAttribute, IResultFilter
    {
        public void OnResultExecuted(ResultExecutedContext filterContext)
        {
            throw new NotImplementedException();
        }

        public void OnResultExecuting(ResultExecutingContext filterContext)
        {
            throw new NotImplementedException();
        }
    }
}