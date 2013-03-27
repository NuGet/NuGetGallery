<%@ Control Language="C#" CodeBehind="Integer_Edit.ascx.cs" Inherits="NuGetGallery.Areas.Admin.DynamicData.Integer_EditField" %>

<asp:TextBox ID="TextBox1" runat="server" Text="<%#FieldValueEditString %>" Columns="10" CssClass="DDTextBox"></asp:TextBox>

<asp:RequiredFieldValidator runat="server" ID="RequiredFieldValidator1" CssClass="DDControl DDValidator" ControlToValidate="TextBox1" Display="Static" Enabled="false" />
<asp:CompareValidator runat="server" ID="CompareValidator1" CssClass="DDControl DDValidator" ControlToValidate="TextBox1" Display="Static"
    Operator="DataTypeCheck" Type="Integer"/>
<asp:RegularExpressionValidator runat="server" ID="RegularExpressionValidator1" CssClass="DDControl DDValidator" ControlToValidate="TextBox1" Display="Static" Enabled="false" />
<asp:RangeValidator runat="server" ID="RangeValidator1" CssClass="DDControl DDValidator" ControlToValidate="TextBox1" Type="Integer"
    Enabled="false" EnableClientScript="true" MinimumValue="0" MaximumValue="100" Display="Static" />
<asp:DynamicValidator runat="server" ID="DynamicValidator1" CssClass="DDControl DDValidator" ControlToValidate="TextBox1" Display="Static" />

