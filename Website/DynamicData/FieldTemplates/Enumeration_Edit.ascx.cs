﻿using System;
using System.Collections.Specialized;
using System.Web.DynamicData;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace DynamicDataEFCodeFirst
{
    public partial class Enumeration_EditField : FieldTemplateUserControl
    {
        public override Control DataControl
        {
            get { return DropDownList1; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            DropDownList1.ToolTip = Column.Description;

            if (DropDownList1.Items.Count == 0)
            {
                if (Mode == DataBoundControlMode.Insert || !Column.IsRequired)
                {
                    DropDownList1.Items.Add(new ListItem("[Not Set]", String.Empty));
                }
                PopulateListControl(DropDownList1);
            }

            SetUpValidator(RequiredFieldValidator1);
            SetUpValidator(DynamicValidator1);
        }

        protected override void OnDataBinding(EventArgs e)
        {
            base.OnDataBinding(e);

            if (Mode == DataBoundControlMode.Edit && FieldValue != null)
            {
                string selectedValueString = GetSelectedValueString();
                ListItem item = DropDownList1.Items.FindByValue(selectedValueString);
                if (item != null)
                {
                    DropDownList1.SelectedValue = selectedValueString;
                }
            }
        }

        protected override void ExtractValues(IOrderedDictionary dictionary)
        {
            string value = DropDownList1.SelectedValue;
            if (String.IsNullOrEmpty(value))
            {
                value = null;
            }
            dictionary[Column.Name] = ConvertEditedValue(value);
        }
    }
}