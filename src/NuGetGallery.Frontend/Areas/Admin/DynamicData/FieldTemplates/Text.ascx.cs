using System.Web.DynamicData;
using System.Web.UI;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public partial class TextField : FieldTemplateUserControl
    {
        private const int MAX_DISPLAYLENGTH_IN_LIST = 25;

        public override string FieldValueString
        {
            get
            {
                string value = base.FieldValueString;
                if (ContainerType == ContainerType.List)
                {
                    if (value != null && value.Length > MAX_DISPLAYLENGTH_IN_LIST)
                    {
                        value = value.Substring(0, MAX_DISPLAYLENGTH_IN_LIST - 3) + "...";
                    }
                }
                return value;
            }
        }

        public override Control DataControl
        {
            get { return Literal1; }
        }
    }
}