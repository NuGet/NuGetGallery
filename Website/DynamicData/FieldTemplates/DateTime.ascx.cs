using System.Web.DynamicData;
using System.Web.UI;

namespace DynamicDataEFCodeFirst
{
    public partial class DateTimeField : FieldTemplateUserControl
    {
        public override Control DataControl
        {
            get { return Literal1; }
        }
    }
}