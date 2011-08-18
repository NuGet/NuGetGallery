using System;
using System.Data;
using System.Data.Objects;
using System.Data.Objects.DataClasses;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;
using System.Xml.Linq;
using System.Web.DynamicData;

namespace DynamicDataEFCodeFirst {
    public partial class ManyToMany_EditField : System.Web.DynamicData.FieldTemplateUserControl {
        public void Page_Load(object sender, EventArgs e) {
            // Register for the DataSource's updating event
            EntityDataSource ds = (EntityDataSource)this.FindDataSourceControl();

            // This field template is used both for Editing and Inserting
            ds.Updating += new EventHandler<EntityDataSourceChangingEventArgs>(DataSource_UpdatingOrInserting);
            ds.Inserting += new EventHandler<EntityDataSourceChangingEventArgs>(DataSource_UpdatingOrInserting);
        }

        void DataSource_UpdatingOrInserting(object sender, EntityDataSourceChangingEventArgs e) {
            MetaTable childTable = ChildrenColumn.ChildTable;

            // Comments assume employee/territory for illustration, but the code is generic

            // Get the collection of territories for this employee
            RelatedEnd entityCollection = (RelatedEnd)Column.EntityTypeProperty.GetValue(e.Entity, null);

            // In Edit mode, make sure it's loaded (doesn't make sense in Insert mode)
            if (Mode == DataBoundControlMode.Edit && !entityCollection.IsLoaded) {
                entityCollection.Load();
            }

            // Get an IList from it (i.e. the list of territories for the current employee)
            // REVIEW: we should be using EntityCollection directly, but EF doesn't have a
            // non generic type for it. They will add this in vnext
            IList entityList = ((IListSource)entityCollection).GetList();

            // Go through all the territories (not just those for this employee)
            foreach (object childEntity in childTable.GetQuery(e.Context)) {

                // Check if the employee currently has this territory
                bool isCurrentlyInList = entityList.Contains(childEntity);

                // Find the checkbox for this territory, which gives us the new state
                string pkString = childTable.GetPrimaryKeyString(childEntity);
                ListItem listItem = CheckBoxList1.Items.FindByValue(pkString);
                if (listItem == null)
                    continue;

                // If the states differs, make the appropriate add/remove change
                if (listItem.Selected) {
                    if (!isCurrentlyInList)
                        entityList.Add(childEntity);
                }
                else {
                    if (isCurrentlyInList)
                        entityList.Remove(childEntity);
                }
            }
        }

        protected void CheckBoxList1_DataBound(object sender, EventArgs e) {
            MetaTable childTable = ChildrenColumn.ChildTable;

            // Comments assume employee/territory for illustration, but the code is generic

            IList entityList = null;
            ObjectContext objectContext = null;

            if (Mode == DataBoundControlMode.Edit) {
                object entity;
                ICustomTypeDescriptor rowDescriptor = Row as ICustomTypeDescriptor;
                if (rowDescriptor != null) {
                    // Get the real entity from the wrapper
                    entity = rowDescriptor.GetPropertyOwner(null);
                }
                else {
                    entity = Row;
                }

                // Get the collection of territories for this employee and make sure it's loaded
                RelatedEnd entityCollection = Column.EntityTypeProperty.GetValue(entity, null) as RelatedEnd;
                if (entityCollection == null) {
                    throw new InvalidOperationException(String.Format("The ManyToMany template does not support the collection type of the '{0}' column on the '{1}' table.", Column.Name, Table.Name));
                }
                if (!entityCollection.IsLoaded) {
                    entityCollection.Load();
                }

                // Get an IList from it (i.e. the list of territories for the current employee)
                // REVIEW: we should be using EntityCollection directly, but EF doesn't have a
                // non generic type for it. They will add this in vnext
                entityList = ((IListSource)entityCollection).GetList();

                // Get the current ObjectContext
                // REVIEW: this is quite a dirty way of doing this. Look for better alternative
                ObjectQuery objectQuery = (ObjectQuery)entityCollection.GetType().GetMethod(
                    "CreateSourceQuery").Invoke(entityCollection, null);
                objectContext = objectQuery.Context;
            }

            // Go through all the territories (not just those for this employee)
            foreach (object childEntity in childTable.GetQuery(objectContext)) {
                MetaTable actualTable = MetaTable.GetTable(childEntity.GetType());
                // Create a checkbox for it
                ListItem listItem = new ListItem(
                    actualTable.GetDisplayString(childEntity),
                    actualTable.GetPrimaryKeyString(childEntity));

                // Make it selected if the current employee has that territory
                if (Mode == DataBoundControlMode.Edit) {
                    listItem.Selected = entityList.Contains(childEntity);
                }

                CheckBoxList1.Items.Add(listItem);
            }
        }

        public override Control DataControl {
            get {
                return CheckBoxList1;
            }
        }

    }
}
