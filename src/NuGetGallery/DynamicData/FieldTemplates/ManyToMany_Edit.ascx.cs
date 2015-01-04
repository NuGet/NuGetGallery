using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Core.Objects.DataClasses;
using System.Linq;
using System.Web.DynamicData;
using System.Web.UI;
using System.Web.UI.WebControls;
using EntityDataSource = Microsoft.AspNet.EntityDataSource.EntityDataSource;
using EntityDataSourceChangingEventArgs = Microsoft.AspNet.EntityDataSource.EntityDataSourceChangingEventArgs;

namespace NuGetGallery
{
    public partial class ManyToMany_EditField : FieldTemplateUserControl
    {
        protected ObjectContext ObjectContext { get; set; }

        public void Page_Load(object sender, EventArgs e)
        {
            EntityDataSource ds = (EntityDataSource)this.FindDataSourceControl();

            ds.ContextCreated += (_, ctxCreatedEnventArgs) => ObjectContext = ctxCreatedEnventArgs.Context;

            ds.Updating += new EventHandler<EntityDataSourceChangingEventArgs>(DataSource_UpdatingOrInserting);
            ds.Inserting += new EventHandler<EntityDataSourceChangingEventArgs>(DataSource_UpdatingOrInserting);
        }

        void DataSource_UpdatingOrInserting(object sender, EntityDataSourceChangingEventArgs e)
        {
            MetaTable childTable = ChildrenColumn.ChildTable;

            if (Mode == DataBoundControlMode.Edit)
            {
                ObjectContext.LoadProperty(e.Entity, Column.Name);
            }

            dynamic entityCollection = Column.EntityTypeProperty.GetValue(e.Entity, null);

            foreach (dynamic childEntity in childTable.GetQuery(e.Context))
            {
                var isCurrentlyInList = ListContainsEntity(childTable, entityCollection, childEntity);

                string pkString = childTable.GetPrimaryKeyString(childEntity);
                ListItem listItem = CheckBoxList1.Items.FindByValue(pkString);
                if (listItem == null)
                    continue;

                if (listItem.Selected)
                {
                    if (!isCurrentlyInList)
                        entityCollection.Add(childEntity);
                }
                else
                {
                    if (isCurrentlyInList)
                        entityCollection.Remove(childEntity);
                }
            }
        }

        private static bool ListContainsEntity(MetaTable table, IEnumerable<object> list, object entity)
        {
            return list.Any(e => AreEntitiesEqual(table, e, entity));
        }

        private static bool AreEntitiesEqual(MetaTable table, object entity1, object entity2)
        {
            return Enumerable.SequenceEqual(table.GetPrimaryKeyValues(entity1), table.GetPrimaryKeyValues(entity2));
        }

        protected void CheckBoxList1_DataBound(object sender, EventArgs e)
        {
            MetaTable childTable = ChildrenColumn.ChildTable;

            IEnumerable<object> entityCollection = null;

            if (Mode == DataBoundControlMode.Edit)
            {
                object entity;
                ICustomTypeDescriptor rowDescriptor = Row as ICustomTypeDescriptor;
                if (rowDescriptor != null)
                {
                    entity = rowDescriptor.GetPropertyOwner(null);
                }
                else
                {
                    entity = Row;
                }

                entityCollection = (IEnumerable<object>)Column.EntityTypeProperty.GetValue(entity, null);
                var realEntityCollection = entityCollection as RelatedEnd;
                if (realEntityCollection != null && !realEntityCollection.IsLoaded)
                {
                    realEntityCollection.Load();
                }
            }

            foreach (object childEntity in childTable.GetQuery(ObjectContext))
            {
                ListItem listItem = new ListItem(
                    childTable.GetDisplayString(childEntity),
                    childTable.GetPrimaryKeyString(childEntity));

                if (Mode == DataBoundControlMode.Edit)
                {
                    listItem.Selected = ListContainsEntity(childTable, entityCollection, childEntity);
                }
                CheckBoxList1.Items.Add(listItem);
            }
        }

        public override Control DataControl
        {
            get
            {
                return CheckBoxList1;
            }
        }

    }
}
