using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;

namespace SearchService.Controllers
{
    public class FeedbackController : Controller
    {
        //GET: /Feedback

        [ActionName("Feedback")]
        [HttpGet]
        public virtual ActionResult Feedback()
        {
            Trace.TraceInformation("Feedback");

            try
            {
                string query = Request.QueryString["query"];
                string prerelease = Request.QueryString["prerelease"];
                string sortBy = Request.QueryString["sortBy"];
                string expectedPackageId = Request.QueryString["expectedPackageId"];
                string contactDetails = Request.QueryString["contactDetails"];

                Trace.TraceInformation("Feedback query: {0} prerelease: {1}, sortBy: {2} expectedPackageId: {3} contactDetails: {4}",
                    query, prerelease, sortBy, expectedPackageId, contactDetails);

                string connectionString = WebConfigurationManager.ConnectionStrings["Feedback"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string sql = @"INSERT Feedback VALUES ( @query, @prerelease, @sortBy, @expectedPackageId, @contactDetails )";

                    SqlCommand command = new SqlCommand(sql, connection);

                    command.Parameters.AddWithValue("query", query);
                    command.Parameters.AddWithValue("prerelease", prerelease);
                    command.Parameters.AddWithValue("sortBy", sortBy);
                    command.Parameters.AddWithValue("expectedPackageId", expectedPackageId);
                    command.Parameters.AddWithValue("contactDetails", contactDetails);

                    command.ExecuteNonQuery();
                }

                return new HttpStatusCodeResult(200);
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
                Trace.TraceError(e.StackTrace);

                if (e.InnerException != null)
                {
                    Trace.TraceError(e.InnerException.Message);
                    Trace.TraceError(e.InnerException.StackTrace);
                }

                throw;
            }
        }
    }
}
