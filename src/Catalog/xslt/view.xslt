<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:msxsl="urn:schemas-microsoft-com:xslt"
    xmlns:xhtml="http://www.w3.org/1999/xhtml"
    exclude-result-prefixes="msxsl"
>
  <xsl:output method="xml"/>

  <xsl:param name="resource" />
  <xsl:param name="frame" />
  <xsl:param name="base" />

  <xsl:template match="@* | node()">
      <xsl:copy>
          <xsl:apply-templates select="@* | node()"/>
      </xsl:copy>
  </xsl:template>

  <xsl:template match="xhtml:a[@id='resource']/@href">
    <xsl:attribute name="href">
      <xsl:value-of select="$resource" />
    </xsl:attribute>
  </xsl:template>

  <xsl:template match="xhtml:div[@id='frame']/text()">
    <xsl:value-of select="$frame" />
  </xsl:template>

  <xsl:template match="xhtml:script[@src]/@src">
    <xsl:attribute name="src">
      <xsl:value-of select="concat($base,.)" />
    </xsl:attribute>
  </xsl:template>

  <xsl:template match="xhtml:link[@href]/@href">
    <xsl:attribute name="href">
      <xsl:value-of select="concat($base,.)" />
    </xsl:attribute>
  </xsl:template>

</xsl:stylesheet>
