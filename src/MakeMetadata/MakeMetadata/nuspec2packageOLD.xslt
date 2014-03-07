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

  <xsl:variable name="id" select="/nuget:package/nuget:metadata/nuget:id" />
  <xsl:variable name="version" select="/nuget:package/nuget:metadata/nuget:version" />

  <xsl:template match="/nuget:package/nuget:metadata">
    <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">    
      <ng:Package>
        <xsl:attribute name="rdf:about">
          <xsl:value-of select="translate(concat($base, nuget:id, '/', nuget:version), $uppercase, $lowercase)"/>
        </xsl:attribute>
        <ng:registration>
          <rdf:Description>
            <xsl:attribute name="rdf:about">
              <xsl:value-of select="translate(concat($base, nuget:id), $uppercase, $lowercase)"/>
            </xsl:attribute>
          </rdf:Description>
        </ng:registration>
        <xsl:for-each select="*">
          <xsl:choose>
            <xsl:when test="self::nuget:dependencies">
              <xsl:apply-templates select="."/>
            </xsl:when>
            <xsl:when test="self::nuget:references">
              <xsl:apply-templates select="."/>
            </xsl:when>
            <xsl:otherwise>
              <xsl:element name="{concat('ng:', local-name())}">
                <xsl:value-of select="."/>
              </xsl:element>
            </xsl:otherwise>
          </xsl:choose>
        </xsl:for-each>
      </ng:Package>
    </rdf:RDF>
  </xsl:template>

  <xsl:template match="nuget:dependencies/nuget:group">
    <ng:dependencyGroup>
      <rdf:Description>
        <xsl:attribute name="rdf:about">
          <xsl:value-of select="translate(concat($base, $id, '/', $version, '#dependencygroup', @targetFramework), $uppercase, $lowercase)"/>
        </xsl:attribute>
        <xsl:if test="@targetFramework">
          <ng:targetFramework>
            <xsl:value-of select="@targetFramework"/>
          </ng:targetFramework>
        </xsl:if>
        <xsl:apply-templates select="nuget:dependency" />
        <!-- TODO: pass through target-framework as template argument -->
      </rdf:Description>
    </ng:dependencyGroup>
  </xsl:template>

  <xsl:template match="nuget:dependency">
    <ng:dependency>
      <rdf:Description>
        <xsl:attribute name="rdf:about">
          <xsl:value-of select="translate(concat($base, $id, '/', $version, '#dependency', '_', @id), $uppercase, $lowercase)"/>
        </xsl:attribute>
        <ng:uri>
          <xsl:attribute name="rdf:resource">
            <xsl:value-of select="translate(concat($base, @id), $uppercase, $lowercase)"/>
          </xsl:attribute>
        </ng:uri>
        <ng:id>
          <xsl:value-of select="@id"/>
        </ng:id>
        <ng:version>
          <xsl:value-of select="@version"/>
        </ng:version>
      </rdf:Description>
    </ng:dependency>
  </xsl:template>

  <xsl:template match="nuget:references/nuget:group">
  </xsl:template>

</xsl:stylesheet>
