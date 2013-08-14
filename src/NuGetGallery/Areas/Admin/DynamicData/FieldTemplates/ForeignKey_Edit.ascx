<%@ Control Language="C#" CodeBehind="ForeignKey_Edit.ascx.cs" Inherits="NuGetGallery.Areas.Admin.DynamicData.ForeignKey_EditField" %>

<asp:DropDownList ID="DropDownList1" runat="server" CssClass="DDDropDown">
</asp:DropDownList>

<asp:RequiredFieldValidator runat="server" ID="RequiredFieldValidator1" CssClass="DDControl DDValidator" ControlToValidate="DropDownList1" Display="Static" Enabled="false" />
<asp:DynamicValidator runat="server" ID="DynamicValidator1" CssClass="DDControl DDValidator" ControlToValidate="DropDownList1" Display="Static" />

