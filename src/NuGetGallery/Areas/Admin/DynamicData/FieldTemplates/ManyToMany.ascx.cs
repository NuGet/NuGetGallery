using System;
using System.ComponentModel;
using System.Web.DynamicData;
using System.Web.UI;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public partial class ManyToManyField : FieldTemplateUserControl
    {
        public override Control DataControl
        {
            get { return Repeater1; }
        }

        protected override void OnDataBinding(EventArgs e)
        {
            base.OnDataBinding(e);

            object entity;
            var rowDescriptor = Row as ICustomTypeDescriptor;
            if (rowDescriptor != null)
            {
                // Get the real entity from the wrapper
                entity = rowDescriptor.GetPropertyOwner(null);
            }
            else
            {
                entity = Row;
            }

            // Get the collection
            var entityCollection = Column.EntityTypeProperty.GetValue(entity, null);

            // Bind the repeater to the list of children entities
            Repeater1.DataSource = entityCollection;
            Repeater1.DataBind();
        }
    }
}