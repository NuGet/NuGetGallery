using System;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public partial class GridViewPager : UserControl
    {
        private GridView _gridView;

        protected void Page_Load(object sender, EventArgs e)
        {
            Control c = Parent;
            while (c != null)
            {
                if (c is GridView)
                {
                    _gridView = (GridView)c;
                    break;
                }
                c = c.Parent;
            }
        }

        protected void TextBoxPage_TextChanged(object sender, EventArgs e)
        {
            if (_gridView == null)
            {
                return;
            }
            int page;
            if (int.TryParse(TextBoxPage.Text.Trim(), out page))
            {
                if (page <= 0)
                {
                    page = 1;
                }
                if (page > _gridView.PageCount)
                {
                    page = _gridView.PageCount;
                }
                _gridView.PageIndex = page - 1;
            }
            TextBoxPage.Text = (_gridView.PageIndex + 1).ToString(CultureInfo.CurrentCulture);
        }

        protected void DropDownListPageSize_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_gridView == null)
            {
                return;
            }
            var dropdownlistpagersize = (DropDownList)sender;
            _gridView.PageSize = Convert.ToInt32(dropdownlistpagersize.SelectedValue, CultureInfo.CurrentCulture);
            int pageindex = _gridView.PageIndex;
            _gridView.DataBind();
            if (_gridView.PageIndex != pageindex)
            {
                //if page index changed it means the previous page was not valid and was adjusted. Rebind to fill control with adjusted page
                _gridView.DataBind();
            }
        }

        protected void Page_PreRender(object sender, EventArgs e)
        {
            if (_gridView != null)
            {
                LabelNumberOfPages.Text = _gridView.PageCount.ToString(CultureInfo.CurrentCulture);
                TextBoxPage.Text = (_gridView.PageIndex + 1).ToString(CultureInfo.CurrentCulture);
                DropDownListPageSize.SelectedValue = _gridView.PageSize.ToString(CultureInfo.CurrentCulture);
            }
        }
    }
}