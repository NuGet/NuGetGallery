<?xml version="1.0" encoding="utf-8" ?>
<xsl:stylesheet
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:nuget="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"
  xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
  xmlns:ng="http://schema.nuget.org/schema#"
  xmlns:obj="urn:helper"
  exclude-result-prefixes="nuget obj"
  version="1.0">

  <xsl:param name="base" />
  <xsl:param name="extension" />

  <xsl:variable name="lowercase" select="'abcdefghijklmnopqrstuvwxyz'" />
  <xsl:variable name="uppercase" select="'ABCDEFGHIJKLMNOPQRSTUVWXYZ'" />

  <xsl:template match="/nuget:package/nuget:metadata">
    <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
    
      <ng:Package>

        <xsl:variable name="path" select="concat(nuget:id, '.', nuget:version)" />

        <xsl:attribute name="rdf:about">
          <xsl:value-of select="translate(concat($base, $path, $extension), $uppercase, $lowercase)"/>
        </xsl:attribute>
        
        <xsl:for-each select="*">
          <xsl:choose>

            <xsl:when test="self::nuget:dependencies">
                  
              <xsl:choose>
                  
                <xsl:when test="nuget:group">
                  <xsl:apply-templates select="nuget:group">
                    <xsl:with-param name="path" select="$path" />
                    <xsl:with-param name="parent_fragment" select="'#dependencyGroup'" />
                  </xsl:apply-templates>
                </xsl:when>
                    
                <xsl:otherwise>
                  <ng:dependencyGroup>
                    <rdf:Description>
                      <xsl:attribute name="rdf:about">
                        <xsl:value-of select="translate(concat($base, $path, $extension, '#dependencyGroup'), $uppercase, $lowercase)"/>
                      </xsl:attribute>
                      <xsl:apply-templates select="nuget:dependency">
                        <xsl:with-param name="path" select="$path" />
                        <xsl:with-param name="parent_fragment" select="'#dependencyGroup'" />
                      </xsl:apply-templates>
                    </rdf:Description>
                  </ng:dependencyGroup>
                </xsl:otherwise>
                  
              </xsl:choose>
                  
            </xsl:when>

            <xsl:when test="self::nuget:references">
              <ng:references>
                <rdf:Description>
                  <xsl:attribute name="rdf:about">
                    <xsl:value-of select="translate(concat($base, $path, $extension, '#references'), $uppercase, $lowercase)"/>
                  </xsl:attribute>

                  <xsl:choose>
                    <xsl:when test="nuget:group">
                      <xsl:apply-templates select="nuget:group">
                        <xsl:with-param name="path" select="$path" />
                        <xsl:with-param name="parent_fragment" select="'#gpref'" />
                      </xsl:apply-templates>
                    </xsl:when>
                    <xsl:otherwise>
                      <ng:group>
                        <rdf:Description>
                          <xsl:attribute name="rdf:about">
                            <xsl:value-of select="translate(concat($base, $extension, '#gpref'), $uppercase, $lowercase)"/>
                          </xsl:attribute>
                          <xsl:apply-templates select="nuget:reference">
                            <xsl:with-param name="path" select="$path" />
                            <xsl:with-param name="parent_fragment" select="'#gpref'" />
                          </xsl:apply-templates>
                        </rdf:Description>
                      </ng:group>
                    </xsl:otherwise>
                  </xsl:choose>

                </rdf:Description>
              </ng:references>
            </xsl:when>

            <xsl:when test="self::nuget:tags">
              <xsl:for-each select="obj:Split(.)//item">
                <ng:tag>
                  <xsl:value-of select="." />
                </ng:tag>
              </xsl:for-each>
            </xsl:when>

            <xsl:when test="self::nuget:owners">
            </xsl:when>

            <xsl:when test="self::nuget:requireLicenseAcceptance">
              <ng:requireLicenseAcceptance rdf:datatype="http://www.w3.org/2001/XMLSchema#boolean">
                <xsl:value-of select="."/>
              </ng:requireLicenseAcceptance>
            </xsl:when>

            <xsl:when test="self::nuget:id">
              <ng:id>
                <xsl:value-of select="."/>
              </ng:id>
            </xsl:when>

            <xsl:when test="self::nuget:version">
              <ng:version>
                <xsl:value-of select="."/>
              </ng:version>
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

  <xsl:template match="nuget:group">
    <xsl:param name="path" />
    <xsl:param name="parent_fragment" />
    <ng:dependencyGroup>
      <rdf:Description>

        <xsl:variable name="fragment">
          <xsl:choose>
            <xsl:when test="@targetFramework">
              <xsl:value-of select="concat($parent_fragment, '/', @targetFramework)"/>
            </xsl:when>
            <xsl:when test="@name">
              <xsl:value-of select="concat($parent_fragment, '/', @name)"/>
            </xsl:when>
            <xsl:otherwise>
              <xsl:value-of select="$parent_fragment"/>
            </xsl:otherwise>
          </xsl:choose>
        </xsl:variable>

        <xsl:attribute name="rdf:about">
          <xsl:value-of select="translate(concat($base, $path, $extension, $fragment), $uppercase, $lowercase)"/>
        </xsl:attribute>

        <xsl:if test="@targetFramework">
          <ng:targetFramework>
            <xsl:value-of select="@targetFramework"/>
          </ng:targetFramework>
        </xsl:if>

        <xsl:if test="@name">
          <ng:name>
            <xsl:value-of select="@name"/>
          </ng:name>
        </xsl:if>

        <xsl:apply-templates select="nuget:dependency">
          <xsl:with-param name="path" select="$path" />
          <xsl:with-param name="parent_fragment" select="$fragment" />
        </xsl:apply-templates>

        <xsl:apply-templates select="nuget:reference">
          <xsl:with-param name="path" select="$path" />
          <xsl:with-param name="parent_fragment" select="$fragment" />
        </xsl:apply-templates>

        <xsl:apply-templates select="nuget:property">
          <xsl:with-param name="path" select="$path" />
          <xsl:with-param name="parent_fragment" select="$fragment" />
        </xsl:apply-templates>

      </rdf:Description>
    </ng:dependencyGroup>
  </xsl:template>

  <xsl:template match="nuget:dependency">
    <xsl:param name="path" />
    <xsl:param name="parent_fragment" />
    <ng:dependency>
      <rdf:Description>

        <xsl:variable name="fragment" select="concat($parent_fragment, '/', @id)" />

        <xsl:attribute name="rdf:about">
          <xsl:value-of select="translate(concat($base, $path, $extension, $fragment), $uppercase, $lowercase)"/>
        </xsl:attribute>

        <ng:id>
          <xsl:value-of select="@id"/>
        </ng:id>

        <ng:range>
          <xsl:value-of select="@version"/>
        </ng:range>

        <xsl:apply-templates select="nuget:property">
          <xsl:with-param name="path" select="$path" />
          <xsl:with-param name="parent_fragment" select="$fragment" />
        </xsl:apply-templates>

      </rdf:Description>
    </ng:dependency>
  </xsl:template>

  <xsl:template match="nuget:reference">
    <xsl:param name="path" />
    <xsl:param name="parent_fragment" />
    <ng:reference>
      <rdf:Description>

        <xsl:variable name="fragment" select="concat($parent_fragment, '/ref/', @file)" />

        <xsl:attribute name="rdf:about">
          <xsl:value-of select="translate(concat($base, $path, $extension, $fragment), $uppercase, $lowercase)"/>
        </xsl:attribute>

        <ng:file>
          <xsl:value-of select="@file"/>
        </ng:file>

        <xsl:apply-templates select="nuget:property">
          <xsl:with-param name="path" select="$path" />
          <xsl:with-param name="parent_fragment" select="$fragment" />
        </xsl:apply-templates>

      </rdf:Description>
    </ng:reference>
  </xsl:template>

  <xsl:template match="nuget:property">
    <xsl:param name="path" />
    <xsl:param name="parent_fragment" />
    <ng:property>
      <rdf:Description>

        <xsl:variable name="fragment" select="concat($parent_fragment, '/prop/', @name)" />

        <xsl:attribute name="rdf:about">
          <xsl:value-of select="translate(concat($base, $path, $extension, $fragment), $uppercase, $lowercase)"/>
        </xsl:attribute>

        <ng:name>
          <xsl:value-of select="@name"/>
        </ng:name>

        <ng:value>
          <xsl:value-of select="text()"/>
        </ng:value>

      </rdf:Description>
    </ng:property>
  </xsl:template>

</xsl:stylesheet>