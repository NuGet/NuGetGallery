using System;
using System.Web.DynamicData;
using System.Web.UI;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public partial class ForeignKeyField : FieldTemplateUserControl
    {
        private bool _allowNavigation = true;

        public string NavigateUrl { get; set; }

        public bool AllowNavigation
        {
            get { return _allowNavigation; }
            set { _allowNavigation = value; }
        }

        public override Control DataControl
        {
            get { return HyperLink1; }
        }

        protected string GetDisplayString()
        {
            object value = FieldValue;

            if (value == null)
            {
                return FormatFieldValue(ForeignKeyColumn.GetForeignKeyString(Row));
            }
            else
            {
                return FormatFieldValue(ForeignKeyColumn.ParentTable.GetDisplayString(value));
            }
        }

        protected string GetNavigateUrl()
        {
            if (!AllowNavigation)
            {
                return null;
            }

            if (String.IsNullOrEmpty(NavigateUrl))
            {
                return ForeignKeyPath;
            }
            else
            {
                return BuildForeignKeyPath(NavigateUrl);
            }
        }
    }
}