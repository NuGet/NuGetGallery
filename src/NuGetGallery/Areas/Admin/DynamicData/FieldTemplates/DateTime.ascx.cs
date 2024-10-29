using System.Web.UI;

namespace NuGetGallery
{
    public partial class DateTimeField : System.Web.DynamicData.FieldTemplateUserControl {
        public override Control DataControl {
            get {
                return Literal1;
            }
        }
    
    }
}
