<%@ Control Language="C#" CodeBehind="Default_Insert.ascx.cs" Inherits="NuGetGallery.Areas.Admin.DynamicData.Default_InsertEntityTemplate" %>

<%@ Reference Control="~/Areas/Admin/DynamicData/EntityTemplates/Default.ascx" %>

<asp:EntityTemplate runat="server" ID="EntityTemplate1">
    <ItemTemplate>
        <tr class="td">
            <td class="DDLightHeader">
                <asp:Label runat="server" OnInit="Label_Init" OnPreRender="Label_PreRender" />
            </td>
            <td>
                <asp:DynamicControl runat="server" ID="DynamicControl" Mode="Insert" OnInit="DynamicControl_Init" />
            </td>
        </tr>
    </ItemTemplate>
</asp:EntityTemplate>

