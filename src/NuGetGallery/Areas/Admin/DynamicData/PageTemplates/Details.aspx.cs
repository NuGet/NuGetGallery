using System;
using System.Data.Entity.Core.Objects;
using System.Web.DynamicData;
using System.Web.UI.WebControls;

namespace NuGetGallery
{
    public partial class Details : System.Web.UI.Page
    {
        protected MetaTable table;

        protected void Page_Init(object sender, EventArgs e)
        {
            table = DynamicDataRouteHandler.GetRequestMetaTable(Context);
            FormView1.SetMetaTable(table);
            DetailsDataSource.EntityTypeFilter = table.EntityType.Name;

            DetailsDataSource.ContextCreating += (o, args) =>
            {
                args.Context = (ObjectContext)table.CreateContext();
            };
            ViewStateUserKey = User.Identity.Name;
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
