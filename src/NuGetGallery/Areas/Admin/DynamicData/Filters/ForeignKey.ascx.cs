using System;
using System.Collections;
using System.Linq;
using System.Web.DynamicData;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public partial class ForeignKeyFilter : QueryableFilterUserControl
    {
        private const string NullValueString = "[null]";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2222:DoNotDecreaseInheritedMemberVisibility")]
        private new MetaForeignKeyColumn Column
        {
            get { return (MetaForeignKeyColumn)base.Column; }
        }

        public override Control FilterControl
        {
            get { return DropDownList1; }
        }

        protected void Page_Init(object sender, EventArgs e)
        {
            if (!Page.IsPostBack)
            {
                if (!Column.IsRequired)
                {
                    DropDownList1.Items.Add(new ListItem("[Not Set]", NullValueString));
                }
                PopulateListControl(DropDownList1);
                // Set the initial value if there is one
                string initialValue = DefaultValue;
                if (!String.IsNullOrEmpty(initialValue))
                {
                    DropDownList1.SelectedValue = initialValue;
                }
            }
        }

        protected void DropDownList1_SelectedIndexChanged(object sender, EventArgs e)
        {
            OnFilterChanged();
        }

        public override IQueryable GetQueryable(IQueryable source)
        {
            string selectedValue = DropDownList1.SelectedValue;
            if (String.IsNullOrEmpty(selectedValue))
            {
                return source;
            }

            if (selectedValue == NullValueString)
            {
                return ApplyEqualityFilter(source, Column.Name, null);
            }

            IDictionary dict = new Hashtable();
            Column.ExtractForeignKey(dict, selectedValue);
            foreach (DictionaryEntry entry in dict)
            {
                var key = (string)entry.Key;
                if (DefaultValues != null)
                {
                    DefaultValues[key] = entry.Value;
                }
                source = ApplyEqualityFilter(source, Column.GetFilterExpression(key), entry.Value);
            }
            return source;
        }
    }
}