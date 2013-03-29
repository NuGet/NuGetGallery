<%@ Control Language="C#" CodeBehind="ForeignKey.ascx.cs" Inherits="NuGetGallery.Areas.Admin.DynamicData.ForeignKeyField" %>

<asp:HyperLink ID="HyperLink1" runat="server"
    Text="<%#GetDisplayString() %>"
    NavigateUrl="<%#GetNavigateUrl() %>"  />

