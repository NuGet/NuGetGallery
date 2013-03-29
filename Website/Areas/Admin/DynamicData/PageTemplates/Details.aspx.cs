using System;
using System.Web.DynamicData;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public partial class Details : Page
    {
        protected MetaTable table;

        protected void Page_Init(object sender, EventArgs e)
        {
            table = DynamicDataRouteHandler.GetRequestMetaTable(Context);
            FormView1.SetMetaTable(table);
            DetailsDataSource.EntityTypeFilter = table.EntityType.Name;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            Title = table.DisplayName;
            DetailsDataSource.Include = table.ForeignKeyColumnsNames;
        }

        protected void FormView1_ItemDeleted(object sender, FormViewDeletedEventArgs e)
        {
            if (e.Exception == null || e.ExceptionHandled)
            {
                Response.Redirect(table.ListActionPath);
            }
        }
    }
}