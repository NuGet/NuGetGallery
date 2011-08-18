using System;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.Web.DynamicData;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace DynamicDataEFCodeFirst {
    public partial class DateTimeField : System.Web.DynamicData.FieldTemplateUserControl {
        public override Control DataControl {
            get {
                return Literal1;
            }
        }

    }
}
