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
          <xsl:value-of select="nuget:id"/>
        </ng:id>
        <ng:owners>
          <xsl:value-of select="nuget:owners"/>
        </ng:owners>
      </ng:PackageRegistration>

      <ng:Package>

        <xsl:variable name="path" select="concat(nuget:id, '/', nuget:version)" />

        <xsl:attribute name="rdf:about">
          <xsl:value-of select="translate(concat($base, $path), $uppercase, $lowercase)"/>
        </xsl:attribute>
        
        <ng:registration>
            <xsl:attribute name="rdf:resource">
              <xsl:value-of select="translate(concat($base, nuget:id), $uppercase, $lowercase)"/>
            </xsl:attribute>
        </ng:registration>
        
        <xsl:for-each select="*">
          <xsl:choose>

            <xsl:when test="self::nuget:dependencies">
              <ng:dependencies>
                <rdf:Description>
                  <xsl:attribute name="rdf:about">
                    <xsl:value-of select="translate(concat($base, $path, '#dependencies'), $uppercase, $lowercase)"/>
                  </xsl:attribute>
                  <xsl:apply-templates select="nuget:group">
                    <xsl:with-param name="parent" select="$path" />
                    <xsl:with-param name="type" select="'gpdep'" />
                  </xsl:apply-templates>
                  <xsl:apply-templates select="nuget:dependency">
                    <xsl:with-param name="parent" select="$path" />
                  </xsl:apply-templates>
                </rdf:Description>
              </ng:dependencies>
            </xsl:when>
            
            <xsl:when test="self::nuget:references">
              <ng:references>
                <rdf:Description>
                  <xsl:attribute name="rdf:about">
                    <xsl:value-of select="translate(concat($base, $path, '#references'), $uppercase, $lowercase)"/>
                  </xsl:attribute>
                  <xsl:apply-templates select="nuget:group">
                    <xsl:with-param name="parent" select="$path" />
                    <xsl:with-param name="type" select="'gpref'" />
                  </xsl:apply-templates>
                  <xsl:apply-templates select="nuget:reference">
                    <xsl:with-param name="parent" select="$path" />
                  </xsl:apply-templates>
                </rdf:Description>
              </ng:references>
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
    <xsl:param name="parent" />
    <xsl:param name="type" />
    <ng:group>
      <rdf:Description>

        <xsl:variable name="group">
          <xsl:choose>
            <xsl:when test="@targetFramework">
              <xsl:value-of select="concat('#', $type, '_', @targetFramework)"/>
            </xsl:when>
            <xsl:when test="@name">
              <xsl:value-of select="concat('#', $type, '_', @name)"/>
            </xsl:when>
            <xsl:otherwise>
              <xsl:value-of select="concat('#', $type)"/>
            </xsl:otherwise>
          </xsl:choose>
        </xsl:variable>

        <xsl:variable name="path" select="concat($parent, $group)" />

        <xsl:attribute name="rdf:about">
          <xsl:value-of select="translate(concat($base, $path), uppercase, $lowercase)"/>
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
          <xsl:with-param name="parent" select="$path" />
        </xsl:apply-templates>

        <xsl:apply-templates select="nuget:reference">
          <xsl:with-param name="parent" select="$path" />
        </xsl:apply-templates>

        <xsl:apply-templates select="nuget:property">
          <xsl:with-param name="parent" select="$path" />
        </xsl:apply-templates>

      </rdf:Description>
    </ng:group>
  </xsl:template>

  <xsl:template match="nuget:dependency">
    <xsl:param name="parent" />
    <ng:dependency>
      <rdf:Description>

        <xsl:variable name="path" select="concat($parent, '_dep_', @id)" />

        <xsl:attribute name="rdf:about">
          <xsl:value-of select="translate(concat($base, $path), $uppercase, $lowercase)"/>
        </xsl:attribute>

        <ng:id>
          <xsl:value-of select="@id"/>
        </ng:id>

        <ng:range>
          <xsl:value-of select="@version"/>
        </ng:range>

        <ng:registration>
          <xsl:attribute name="rdf:resource">
            <xsl:value-of select="translate(concat($base, @id), $uppercase, $lowercase)"/>
          </xsl:attribute>
        </ng:registration>

        <xsl:apply-templates select="nuget:property">
          <xsl:with-param name="parent" select="$path" />
        </xsl:apply-templates>

      </rdf:Description>
    </ng:dependency>
  </xsl:template>

  <xsl:template match="nuget:reference">
    <xsl:param name="parent" />
    <ng:reference>
      <rdf:Description>

        <xsl:variable name="path" select="concat($parent, '_ref_', @file)" />

        <xsl:attribute name="rdf:about">
          <xsl:value-of select="translate(concat($base, $path), $uppercase, $lowercase)"/>
        </xsl:attribute>

        <ng:file>
          <xsl:value-of select="@file"/>
        </ng:file>

        <xsl:apply-templates select="nuget:property">
          <xsl:with-param name="parent" select="$path" />
        </xsl:apply-templates>

      </rdf:Description>
    </ng:reference>
  </xsl:template>

  <xsl:template match="nuget:property">
    <xsl:param name="parent" />
    <ng:property>
      <rdf:Description>

        <xsl:variable name="path" select="concat($parent, '_prop_', @name)" />

        <xsl:attribute name="rdf:about">
          <xsl:value-of select="translate(concat($base, $path), $uppercase, $lowercase)"/>
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
