using System;
using System.Data.SqlClient;
using System.Web;
using Elmah;

namespace NuGetGallery.Infrastructure
{
    public class NuGetErrorLogPageFactory : ErrorLogPageFactory
    {
        public override IHttpHandler GetHandler(HttpContext context, string requestType, string url,
                                                string pathTranslated)
        {
            return new HttpHandlerWrapper(base.GetHandler(context, requestType, url, pathTranslated));
        }

        private class HttpHandlerWrapper : IHttpHandler
        {
            private readonly IHttpHandler _handler;

            public HttpHandlerWrapper(IHttpHandler handler)
            {
                _handler = handler;
            }


            public bool IsReusable
            {
                get { return _handler.IsReusable; }
            }

            public void ProcessRequest(HttpContext context)
            {
                try
                {
                    _handler.ProcessRequest(context);
                }
                catch (HttpUnhandledException e)
                {
                    if (e.InnerException != null && e.InnerException is SqlException &&
                        (e.InnerException.Message.IndexOf("Could not find stored procedure",
                                                          StringComparison.OrdinalIgnoreCase) > -1))
                    {
                        context.Response.Write("<h1>ELMAH not configured correctly.</h1>");
                        context.Response.Write(
                            @"<p>Run the SQL script '<em>Elmah.SqlServer.sql</em>' located in '<em>{SolutionDir}</em>\packages\elmah.sqlserver.1.2\content\App_Readme\' against your SQL database.</p>");
                        return;
                    }
                    throw;
                }
            }
        }
    }
}