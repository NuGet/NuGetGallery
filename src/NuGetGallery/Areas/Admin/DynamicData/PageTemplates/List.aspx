<%@ Page Language="C#" MasterPageFile="../Site.master" CodeBehind="List.aspx.cs" Inherits="NuGetGallery.Areas.Admin.DynamicData.List" %>

<%@ Register Src="~/Areas/Admin/DynamicData/Content/GridViewPager.ascx" TagName="GridViewPager" TagPrefix="asp" %>

<asp:Content ID="headContent" ContentPlaceHolderID="head" runat="Server">
</asp:Content>

<asp:Content ID="Content1" ContentPlaceHolderID="ContentPlaceHolder1" runat="Server">
    <asp:DynamicDataManager ID="DynamicDataManager1" runat="server" AutoLoadForeignKeys="true">
        <DataControls>
            <asp:DataControlReference ControlID="GridView1" />
        </DataControls>
    </asp:DynamicDataManager>

    <h2 class="DDSubHeader"><%= table.DisplayName %></h2>

    <div class="DD">
        <asp:ValidationSummary ID="ValidationSummary1" runat="server" EnableClientScript="true"
            HeaderText="List of validation errors" CssClass="DDValidator" />
        <asp:DynamicValidator runat="server" ID="GridViewValidator" ControlToValidate="GridView1" Display="None" CssClass="DDValidator" />

        <asp:PlaceHolder ID="SearchPanel" runat="server">
            <asp:TextBox runat="server" ID="search" /><asp:Button Text="Search" runat="server" /><br />
        </asp:PlaceHolder>

        <asp:QueryableFilterRepeater runat="server" ID="FilterRepeater">
            <ItemTemplate>
                <asp:Label runat="server" Text='<%#Eval("DisplayName") %>' OnPreRender="Label_PreRender" />
                <asp:DynamicFilter runat="server" ID="DynamicFilter" OnFilterChanged="DynamicFilter_FilterChanged" />
                <br />
            </ItemTemplate>
        </asp:QueryableFilterRepeater>
        <br />
    </div>

    <asp:GridView ID="GridView1" runat="server" DataSourceID="GridDataSource" EnablePersistedSelection="true"
        AllowPaging="True" AllowSorting="True" CssClass="DDGridView"
        RowStyle-CssClass="td" HeaderStyle-CssClass="th" CellPadding="6">
        <Columns>
            <asp:TemplateField>
                <ItemTemplate>
                    <asp:DynamicHyperLink runat="server" Action="Edit" Text="Edit" />&nbsp;<asp:LinkButton runat="server" CommandName="Delete" Text="Delete"
                        OnClientClick='return confirm("Are you sure you want to delete this item?");' />&nbsp;<asp:DynamicHyperLink runat="server" Text="Details" />
                </ItemTemplate>
            </asp:TemplateField>
        </Columns>

        <PagerStyle CssClass="DDFooter" />
        <PagerTemplate>
            <asp:GridViewPager runat="server" />
        </PagerTemplate>
        <EmptyDataTemplate>
            There are currently no items in this table.
        </EmptyDataTemplate>
    </asp:GridView>

    <asp:EntityDataSource ID="GridDataSource" runat="server" EnableDelete="true" />

    <asp:QueryExtender TargetControlID="GridDataSource" ID="GridQueryExtender" runat="server">
        <asp:DynamicFilterExpression ControlID="FilterRepeater" />
        <asp:SearchExpression SearchType="Contains">
            <asp:ControlParameter ControlID="search" />
        </asp:SearchExpression>
    </asp:QueryExtender>

    <br />

    <div class="DDBottomHyperLink">
        <asp:DynamicHyperLink ID="InsertHyperLink" runat="server" Action="Insert"><img runat="server" src="~/Areas/Admin/DynamicData/Content/Images/plus.gif" alt="Insert new item" />Insert new item</asp:DynamicHyperLink>
    </div>
</asp:Content>

