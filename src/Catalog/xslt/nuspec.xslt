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

  <xsl:template match="/nuget:package/nuget:metadata">
    
    <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">

      <ng:PackageDetails>

        <xsl:variable name="path" select="concat(nuget:id, '.', obj:NormalizeVersion(nuget:version))" />

        <xsl:attribute name="rdf:about">
          <xsl:value-of select="obj:LowerCase(concat($base, $path, $extension))"/>
        </xsl:attribute>

        <xsl:if test="@minClientVersion">
          <ng:minClientVersion>
            <xsl:value-of select="@minClientVersion" />
          </ng:minClientVersion>
        </xsl:if>
          
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
                    <ng:PackageDependencyGroup>
                      <xsl:attribute name="rdf:about">
                        <xsl:value-of select="obj:LowerCase(concat($base, $path, $extension, '#dependencyGroup'))"/>
                      </xsl:attribute>
                      <xsl:apply-templates select="nuget:dependency">
                        <xsl:with-param name="path" select="$path" />
                        <xsl:with-param name="parent_fragment" select="'#dependencyGroup'" />
                      </xsl:apply-templates>
                    </ng:PackageDependencyGroup>
                  </ng:dependencyGroup>
                </xsl:otherwise>
                  
              </xsl:choose>
                  
            </xsl:when>

            <xsl:when test="self::nuget:references">
              <ng:references>
                <rdf:Description>
                  <xsl:attribute name="rdf:about">
                    <xsl:value-of select="obj:LowerCase(concat($base, $path, $extension, '#references'))"/>
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
                            <xsl:value-of select="obj:LowerCase(concat($base, $extension, '#gpref'))"/>
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
                <xsl:value-of select="obj:NormalizeVersion(.)"/>
              </ng:version>
              <ng:verbatimVersion>
                <xsl:value-of select="."/>
              </ng:verbatimVersion>
              <ng:isPrerelease rdf:datatype="http://www.w3.org/2001/XMLSchema#boolean">
                <xsl:value-of select="obj:IsPrerelease(.)"/>
              </ng:isPrerelease>
            </xsl:when>
            
            <xsl:when test="self::nuget:frameworkAssemblies">
              <xsl:apply-templates select=".">
                <xsl:with-param name="path" select="$path" />
              </xsl:apply-templates>
            </xsl:when>
            
            <xsl:otherwise>
              <xsl:element name="{concat('ng:', local-name())}">
                <xsl:value-of select="."/>
              </xsl:element>
            </xsl:otherwise>

          </xsl:choose>
        </xsl:for-each>

      </ng:PackageDetails>
    </rdf:RDF>
  </xsl:template>

  <xsl:template match="nuget:group">
    <xsl:param name="path" />
    <xsl:param name="parent_fragment" />
    <ng:dependencyGroup>
      <ng:PackageDependencyGroup>

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
          <xsl:value-of select="obj:LowerCase(concat($base, $path, $extension, $fragment))"/>
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

      </ng:PackageDependencyGroup>
    </ng:dependencyGroup>
  </xsl:template>

  <xsl:template match="nuget:dependency">
    <xsl:param name="path" />
    <xsl:param name="parent_fragment" />
    <ng:dependency>
      <ng:PackageDependency>

        <xsl:variable name="fragment" select="concat($parent_fragment, '/', @id)" />

        <xsl:attribute name="rdf:about">
          <xsl:value-of select="obj:LowerCase(concat($base, $path, $extension, $fragment))" />
        </xsl:attribute>
        
        <ng:id>
          <xsl:value-of select="@id"/>        
        </ng:id>

        <ng:range>
          <xsl:value-of select="obj:NormalizeVersionRange(@version)" />
        </ng:range>

      </ng:PackageDependency>
    </ng:dependency>
  </xsl:template>

  <xsl:template match="nuget:reference">
    <xsl:param name="path" />
    <xsl:param name="parent_fragment" />
    <ng:reference>
      <rdf:Description>

        <xsl:variable name="fragment" select="concat($parent_fragment, '/ref/', @file)" />

        <xsl:attribute name="rdf:about">
          <xsl:value-of select="obj:LowerCase(concat($base, $path, $extension, $fragment))"/>
        </xsl:attribute>

        <ng:file>
          <xsl:value-of select="@file"/>
        </ng:file>

      </rdf:Description>
    </ng:reference>
  </xsl:template>

  <xsl:template match="nuget:frameworkAssemblies">
    <xsl:param name="path" />
    <xsl:apply-templates select="nuget:frameworkAssembly">
      <xsl:with-param name="path" select="$path" />
    </xsl:apply-templates>
  </xsl:template>

  <xsl:template match="nuget:frameworkAssembly">
    <xsl:param name="path" />

      <xsl:choose>
        <xsl:when test="string-length(@targetFramework) &gt; 0">
          <xsl:variable name="assemblyName" select="@assemblyName" />
          <xsl:for-each select="obj:Split(@targetFramework)//item">
            <ng:frameworkAssemblyGroup>
              <rdf:Description>
                <xsl:attribute name="rdf:about">
                  <xsl:value-of select="obj:LowerCase(concat($base, $path, $extension, '#frameworkAssemblyGroup/', .))"/>
                </xsl:attribute>
                <ng:assembly>
                  <xsl:value-of select="$assemblyName" />
                </ng:assembly>
                <ng:targetFramework>
                  <xsl:value-of select="." />
                </ng:targetFramework>
              </rdf:Description>
            </ng:frameworkAssemblyGroup>
          </xsl:for-each>
        </xsl:when>
        <xsl:otherwise>
          <ng:frameworkAssemblyGroup>
            <rdf:Description>
              <xsl:attribute name="rdf:about">
                <xsl:value-of select="obj:LowerCase(concat($base, $path, $extension, '#frameworkAssemblyGroup'))"/>
              </xsl:attribute>
              <ng:assembly>
                <xsl:value-of select="@assemblyName" />
              </ng:assembly>
            </rdf:Description>
          </ng:frameworkAssemblyGroup>
        </xsl:otherwise>
      </xsl:choose>
          
  </xsl:template>
  
</xsl:stylesheet>