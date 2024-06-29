using System;
using System.ComponentModel;
using System.Data.Entity.Core.Objects.DataClasses;
using System.Web.UI;

namespace NuGetGallery
{
    public partial class ManyToManyField : System.Web.DynamicData.FieldTemplateUserControl
    {
        protected override void OnDataBinding(EventArgs e)
        {
            base.OnDataBinding(e);

            object entity;
            if (Row is ICustomTypeDescriptor rowDescriptor)
            {
                entity = rowDescriptor.GetPropertyOwner(null);
            }
            else
            {
                entity = Row;
            }

            var entityCollection = Column.EntityTypeProperty.GetValue(entity, null);
            if (entityCollection is RelatedEnd realEntityCollection && !realEntityCollection.IsLoaded)
            {
                realEntityCollection.Load();
            }

            Repeater1.DataSource = entityCollection;
            Repeater1.DataBind();
        }

        public override Control DataControl
        {
            get
            {
                return Repeater1;
            }
        }

    }
}
