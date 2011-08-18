using System;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.Web.DynamicData;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace DynamicDataEFCodeFirst {
    public partial class ForeignKeyField : System.Web.DynamicData.FieldTemplateUserControl {
        private bool _allowNavigation = true;

        public string NavigateUrl {
            get;
            set;
        }

        public bool AllowNavigation {
            get {
                return _allowNavigation;
            }
            set {
                _allowNavigation = value;
            }
        }

        protected string GetDisplayString() {
            object value = FieldValue;

            if (value == null) {
                return FormatFieldValue(ForeignKeyColumn.GetForeignKeyString(Row));
            }
            else {
                return FormatFieldValue(ForeignKeyColumn.ParentTable.GetDisplayString(value));
            }
        }

        protected string GetNavigateUrl() {
            if (!AllowNavigation) {
                return null;
            }

            if (String.IsNullOrEmpty(NavigateUrl)) {
                return ForeignKeyPath;
            }
            else {
                return BuildForeignKeyPath(NavigateUrl);
            }
        }

        public override Control DataControl {
            get {
                return HyperLink1;
            }
        }

    }
}
