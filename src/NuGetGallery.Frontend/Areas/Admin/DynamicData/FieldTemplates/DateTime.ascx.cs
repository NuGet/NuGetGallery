using System.Web.DynamicData;
using System.Web.UI;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public partial class DateTimeField : FieldTemplateUserControl
    {
        public override Control DataControl
        {
            get { return Literal1; }
        }
    }
}