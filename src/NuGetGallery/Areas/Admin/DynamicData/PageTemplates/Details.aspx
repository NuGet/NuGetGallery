<%@ Page Language="C#" MasterPageFile="../Site.master" CodeBehind="Details.aspx.cs" Inherits="NuGetGallery.Areas.Admin.DynamicData.Details" %>


<asp:Content ID="headContent" ContentPlaceHolderID="head" runat="Server">
</asp:Content>

<asp:Content ID="Content1" ContentPlaceHolderID="ContentPlaceHolder1" runat="Server">
    <asp:DynamicDataManager ID="DynamicDataManager1" runat="server" AutoLoadForeignKeys="true">
        <DataControls>
            <asp:DataControlReference ControlID="DetailsView1" />
        </DataControls>
    </asp:DynamicDataManager>

    <h2 class="DDSubHeader">Entry from table <%= table.DisplayName %></h2>

    <asp:ValidationSummary ID="ValidationSummary1" runat="server" EnableClientScript="true"
        HeaderText="List of validation errors" CssClass="DDValidator" />
    <asp:DynamicValidator runat="server" ID="DetailsViewValidator" ControlToValidate="DetailsView1" Display="None" CssClass="DDValidator" />

    <asp:DetailsView runat="server" ID="DetailsView1" DataSourceID="DetailsDataSource" DefaultMode="ReadOnly"
        OnItemDeleted="DetailsView1_ItemDeleted" RenderOuterTable="false" BorderWidth="0" AutoGenerateEditButton="True">
        <FieldHeaderStyle BorderWidth="0" />
        <EmptyDataTemplate>
            <div class="DDNoItem">No such item.</div>
        </EmptyDataTemplate>
    </asp:DetailsView>

    <asp:EntityDataSource ID="DetailsDataSource" runat="server" EnableDelete="true" />

    <asp:QueryExtender TargetControlID="DetailsDataSource" ID="DetailsQueryExtender" runat="server">
        <asp:DynamicRouteExpression />
    </asp:QueryExtender>

    <br />

    <div class="DDBottomHyperLink">
        <asp:DynamicHyperLink ID="ListHyperLink" runat="server" Action="List">Show all items</asp:DynamicHyperLink>
    </div>
</asp:Content>

