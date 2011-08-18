using System;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.Web.DynamicData;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace DynamicDataEFCodeFirst {
    public partial class EnumerationField : System.Web.DynamicData.FieldTemplateUserControl {
        public override Control DataControl {
            get {
                return Literal1;
            }
        }

        public string EnumFieldValueString {
            get {
                if (FieldValue == null) {
                    return FieldValueString;
                }

                Type enumType = Column.GetEnumType();
                if (enumType != null) {
                    object enumValue = System.Enum.ToObject(enumType, FieldValue);
                    return FormatFieldValue(enumValue);
                }

                return FieldValueString;
            }
        }

    }
}
