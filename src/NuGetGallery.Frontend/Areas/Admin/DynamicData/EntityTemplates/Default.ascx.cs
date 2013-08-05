using System;
using System.Web.DynamicData;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public partial class DefaultEntityTemplate : EntityTemplateUserControl
    {
        private MetaColumn currentColumn;

        protected override void OnLoad(EventArgs e)
        {
            foreach (MetaColumn column in Table.GetScaffoldColumns(Mode, ContainerType))
            {
                currentColumn = column;
                Control item = new _NamingContainer();
                EntityTemplate1.ItemTemplate.InstantiateIn(item);
                EntityTemplate1.Controls.Add(item);
            }
        }

        protected void Label_Init(object sender, EventArgs e)
        {
            var label = (Label)sender;
            label.Text = currentColumn.DisplayName;
        }

        protected void DynamicControl_Init(object sender, EventArgs e)
        {
            var dynamicControl = (DynamicControl)sender;
            dynamicControl.DataField = currentColumn.Name;
        }

        public class _NamingContainer : Control, INamingContainer
        {
        }
    }
}