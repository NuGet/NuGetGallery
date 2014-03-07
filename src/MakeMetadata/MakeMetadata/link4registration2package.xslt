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
        <ng:id>
          <xsl:value-of select="nuget:id" />
        </ng:id>
        <ng:package>
          <rdf:Description>
            <xsl:attribute name="rdf:about">
              <xsl:value-of select="translate(concat($base, nuget:id, '/', nuget:version), $uppercase, $lowercase)"/>
            </xsl:attribute>
            <ng:version>
              <xsl:value-of select="nuget:version" />
            </ng:version>
          </rdf:Description>
        </ng:package>
      </ng:PackageRegistration>
   
  </rdf:RDF>
  </xsl:template>
 
</xsl:stylesheet>
