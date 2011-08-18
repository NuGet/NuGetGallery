using System;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.Web.DynamicData;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace DynamicDataEFCodeFirst {
    public partial class EmailAddressField : System.Web.DynamicData.FieldTemplateUserControl {
        protected override void OnDataBinding(EventArgs e) {
            string url = FieldValueString;
            if (!url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) {
                url = "mailto:" + url;
            }
            HyperLink1.NavigateUrl = url;
        }

        public override Control DataControl {
            get {
                return HyperLink1;
            }
        }

    }
}
