using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Objects;
using System.Linq;
using System.Web.DynamicData;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public partial class ManyToMany_EditField : FieldTemplateUserControl
    {
        protected ObjectContext ObjectContext { get; set; }

        public override Control DataControl
        {
            get { return CheckBoxList1; }
        }

        public void Page_Load(object sender, EventArgs e)
        {
            // Register for the DataSource's updating event
            var ds = (EntityDataSource)this.FindDataSourceControl();

            ds.ContextCreated += (_, ctxCreatedEventArg) => ObjectContext = ctxCreatedEventArg.Context;

            // This field template is used both for Editing and Inserting
            ds.Updating += DataSource_UpdatingOrInserting;
            ds.Inserting += DataSource_UpdatingOrInserting;
        }

        private void DataSource_UpdatingOrInserting(object sender, EntityDataSourceChangingEventArgs e)
        {
            MetaTable childTable = ChildrenColumn.ChildTable;

            // Comments assume employee/territory for illustration, but the code is generic

            if (Mode == DataBoundControlMode.Edit)
            {
                ObjectContext.LoadProperty(e.Entity, Column.Name);
            }

            // Get the collection of territories for this employee
            dynamic entityList = Column.EntityTypeProperty.GetValue(e.Entity, null);

            // Go through all the territories (not just those for this employee)
            foreach (dynamic childEntity in childTable.GetQuery(e.Context))
            {
                // Check if the employee currently has this territory
                bool isCurrentlyInList = ListContainsEntity(childTable, entityList, childEntity);

                // Find the checkbox for this territory, which gives us the new state
                string pkString = childTable.GetPrimaryKeyString(childEntity);
                ListItem listItem = CheckBoxList1.Items.FindByValue(pkString);
                if (listItem == null)
                {
                    continue;
                }

                // If the states differs, make the appropriate add/remove change
                if (listItem.Selected)
                {
                    if (!isCurrentlyInList)
                    {
                        entityList.Add(childEntity);
                    }
                }
                else
                {
                    if (isCurrentlyInList)
                    {
                        entityList.Remove(childEntity);
                    }
                }
            }
        }

        protected void CheckBoxList1_DataBound(object sender, EventArgs e)
        {
            MetaTable childTable = ChildrenColumn.ChildTable;

            // Comments assume employee/territory for illustration, but the code is generic

            IEnumerable<object> entityList = null;

            if (Mode == DataBoundControlMode.Edit)
            {
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

                // Get the collection of territories for this employee
                entityList = (IEnumerable<object>)Column.EntityTypeProperty.GetValue(entity, null);
            }

            // Go through all the territories (not just those for this employee)
            foreach (object childEntity in childTable.GetQuery(ObjectContext))
            {
                // Create a checkbox for it
                var listItem = new ListItem(
                    childTable.GetDisplayString(childEntity),
                    childTable.GetPrimaryKeyString(childEntity));

                // Make it selected if the current employee has that territory
                if (Mode == DataBoundControlMode.Edit)
                {
                    listItem.Selected = ListContainsEntity(childTable, entityList, childEntity);
                }

                CheckBoxList1.Items.Add(listItem);
            }
        }

        private static bool ListContainsEntity(MetaTable table, IEnumerable<object> list, object entity)
        {
            return list.Any(e => AreEntitiesEqual(table, e, entity));
        }

        private static bool AreEntitiesEqual(MetaTable table, object entity1, object entity2)
        {
            var pks1 = table.GetPrimaryKeyValues(entity1);
            var pks2 = table.GetPrimaryKeyValues(entity2);

            return pks1.SequenceEqual(pks2);
        }
    }
}