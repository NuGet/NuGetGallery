<%@ Control Language="C#" CodeBehind="ManyToMany.ascx.cs" Inherits="DynamicDataEFCodeFirst.ManyToManyField" %>

<asp:Repeater ID="Repeater1" runat="server">
    <ItemTemplate>
      <asp:DynamicHyperLink runat="server"></asp:DynamicHyperLink>
    </ItemTemplate>
</asp:Repeater>

