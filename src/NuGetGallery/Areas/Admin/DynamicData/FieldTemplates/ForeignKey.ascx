<%@ Control Language="C#" CodeBehind="ForeignKey.ascx.cs" Inherits="NuGetGallery.ForeignKeyField" %>

<asp:HyperLink ID="HyperLink1" runat="server"
    Text="<%# GetDisplayString() %>"
    NavigateUrl="<%# GetNavigateUrl() %>"  />

