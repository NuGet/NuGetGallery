using System;
using System.Web.DynamicData;
using System.Web.UI;

namespace DynamicDataEFCodeFirst
{
    public partial class EnumerationField : System.Web.DynamicData.FieldTemplateUserControl
    {
        public override Control DataControl
        {
            get
            {
                return Literal1;
            }
        }

        public string EnumFieldValueString
        {
            get
            {
                if (FieldValue == null)
                {
                    return FieldValueString;
                }

                Type enumType = Column.GetEnumType();
                if (enumType != null)
                {
                    object enumValue = System.Enum.ToObject(enumType, FieldValue);
                    return FormatFieldValue(enumValue);
                }

                return FieldValueString;
            }
        }

    }
}
