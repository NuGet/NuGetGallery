<?xml version="1.0" encoding="utf-8" ?>
<xsl:stylesheet 
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:nuget="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"
  xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
  xmlns:ng="http://nuget.org/schema#"
  version="1.0">

  <xsl:param name="base" />

  <xsl:variable name="lowercase" select="'abcdefghijklmnopqrstuvwxyz'" />
  <xsl:variable name="uppercase" select="'ABCDEFGHIJKLMNOPQRSTUVWXYZ'" />

  <xsl:template match="/nuget:package/nuget:metadata">
    <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">    
      <ng:PackageRegistration>
        <xsl:attribute name="rdf:about">
          <xsl:value-of select="translate(concat($base, nuget:id), $uppercase, $lowercase)"/>
        </xsl:attribute>
        <xsl:call-template name="output-tokens">
          <xsl:with-param name="list" select="nuget:owners" />
        </xsl:call-template>
      </ng:PackageRegistration>
  </rdf:RDF>
  </xsl:template>

  <xsl:template name="output-tokens">
    <xsl:param name="list" />
    <xsl:variable name="newlist" select="concat(normalize-space($list), ' ')" />
    <xsl:variable name="first" select="substring-before($newlist, ' ')" />
    <xsl:variable name="remaining" select="substring-after($newlist, ' ')" />
    <ng:owner>
      <rdf:Description>
        <xsl:attribute name="rdf:about">
          <xsl:value-of select="translate(concat($base, 'owner/', $first), $uppercase, $lowercase)"/>
        </xsl:attribute>
        <ng:name>
          <xsl:value-of select="$first" />
        </ng:name>
      </rdf:Description>
    </ng:owner>
    <xsl:if test="$remaining">
      <xsl:call-template name="output-tokens">
        <xsl:with-param name="list" select="$remaining" />
      </xsl:call-template>
    </xsl:if>
  </xsl:template>

</xsl:stylesheet>
