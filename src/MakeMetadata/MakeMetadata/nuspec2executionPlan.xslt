<?xml version="1.0" encoding="utf-8" ?>
<xsl:stylesheet 
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:nuget="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"
  xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
  xmlns:ng="http://nuget.org/schema#"
  version="1.0">
  
  <xsl:variable name="lowercase" select="'abcdefghijklmnopqrstuvwxyz'" />
  <xsl:variable name="uppercase" select="'ABCDEFGHIJKLMNOPQRSTUVWXYZ'" />
  
  <xsl:template match="/nuget:package/nuget:metadata">
    <ng:ExecutionPlan>
      <xsl:attribute name="name">
        <xsl:value-of select="translate(concat(nuget:id, '/', nuget:version), $uppercase, $lowercase)"/>
      </xsl:attribute>
      <xsl:call-template name="output-tokens">
        <xsl:with-param name="transform" select="'nuspec2owner.xslt'" />
        <xsl:with-param name="frame" select="'ownerFrame.json'" />
        <xsl:with-param name="list" select="nuget:owners" />
      </xsl:call-template>
      <ng:Stage transform="nuspec2registration.xslt" frame="registrationFrame.json">
        <xsl:attribute name="output">
          <xsl:value-of select="translate(nuget:id, $uppercase, $lowercase)"/>
        </xsl:attribute>
      </ng:Stage>
      <ng:Stage transform="nuspec2package.xslt" frame="packageFrame.json">
        <xsl:attribute name="output">
          <xsl:value-of select="translate(concat(nuget:id, '/', nuget:version), $uppercase, $lowercase)"/>
        </xsl:attribute>
      </ng:Stage>
      <xsl:call-template name="output-tokens">
        <xsl:with-param name="transform" select="'link4owner2registration.xslt'" />
        <xsl:with-param name="frame" select="'ownerFrame.json'" />
        <xsl:with-param name="list" select="nuget:owners" />
      </xsl:call-template>
      <ng:Stage transform="link4registration2package.xslt" frame="registrationFrame.json">
        <xsl:attribute name="output">
          <xsl:value-of select="translate(nuget:id, $uppercase, $lowercase)"/>
        </xsl:attribute>
      </ng:Stage>
    </ng:ExecutionPlan>
  </xsl:template>

  <xsl:template name="output-tokens">
    <xsl:param name="transform" />
    <xsl:param name="frame" />
    <xsl:param name="list" />
    <xsl:variable name="newlist" select="concat(normalize-space($list), ' ')" />
    <xsl:variable name="first" select="substring-before($newlist, ' ')" />
    <xsl:variable name="remaining" select="substring-after($newlist, ' ')" />
    
    <ng:Stage>
      <xsl:attribute name="transform">
        <xsl:value-of select="$transform"/>
      </xsl:attribute>
      <xsl:attribute name="frame">
        <xsl:value-of select="$frame"/>
      </xsl:attribute>
      <xsl:attribute name="output">
        <xsl:value-of select="translate(concat('owner/', $first), $uppercase, $lowercase)"/>
      </xsl:attribute>
      <ng:Arg>
        <xsl:attribute name="name">
          <xsl:value-of select="'owner'"/>
        </xsl:attribute>
        <xsl:attribute name="value">
          <xsl:value-of select="$first"/>
        </xsl:attribute>
      </ng:Arg>
    </ng:Stage>
    
    <xsl:if test="$remaining">
      <xsl:call-template name="output-tokens">
        <xsl:with-param name="transform" select="$transform" />
        <xsl:with-param name="frame" select="$frame" />
        <xsl:with-param name="list" select="$remaining" />
      </xsl:call-template>
    </xsl:if>
  </xsl:template>

</xsl:stylesheet>
