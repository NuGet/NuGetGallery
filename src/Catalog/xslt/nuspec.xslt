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

  <xsl:template match="*">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
  </xsl:template>

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
            <xsl:when test="self::nuget:packageTypes">
              <xsl:apply-templates select="nuget:packageType">
                <xsl:with-param name="path" select="$path" />
                <xsl:with-param name="parent_fragment" select="'#packageTypes'" />
              </xsl:apply-templates>
            </xsl:when>

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
              <!-- owners is deprecated and ignored -->
            </xsl:when>

            <xsl:when test="self::nuget:license">
              <xsl:choose>
                <xsl:when test="@type='file'">
                  <ng:licenseFile>
                    <xsl:value-of select="."/>
                  </ng:licenseFile>
                </xsl:when>
                <xsl:when test="@type='expression'">
                  <ng:licenseExpression>
                    <xsl:value-of select="."/>
                  </ng:licenseExpression>
                </xsl:when>
              </xsl:choose>
            </xsl:when>

            <xsl:when test="self::nuget:icon">
              <xsl:choose>
                <xsl:when test="@type='file'">
                  <ng:iconFile>
                    <xsl:value-of select="."/>
                  </ng:iconFile>
                </xsl:when>
                <xsl:when test="not(@type)">
                  <ng:iconFile>
                    <xsl:value-of select="."/>
                  </ng:iconFile>
                </xsl:when>
              </xsl:choose>
            </xsl:when>

            <xsl:when test="self::nuget:readme">
              <xsl:choose>
                <xsl:when test="@type='file'">
                  <ng:readmeFile>
                    <xsl:value-of select="."/>
                  </ng:readmeFile>
                </xsl:when>
                <xsl:when test="not(@type)">
                  <ng:readmeFile>
                    <xsl:value-of select="."/>
                  </ng:readmeFile>
                </xsl:when>
              </xsl:choose>
            </xsl:when>

            <xsl:when test="self::nuget:requireLicenseAcceptance">
              <ng:requireLicenseAcceptance rdf:datatype="http://www.w3.org/2001/XMLSchema#boolean">
                <xsl:value-of select="."/>
              </ng:requireLicenseAcceptance>
            </xsl:when>

            <xsl:when test="self::nuget:developmentDependency">
              <ng:developmentDependency rdf:datatype="http://www.w3.org/2001/XMLSchema#boolean">
                <xsl:value-of select="."/>
              </ng:developmentDependency>
            </xsl:when>

            <xsl:when test="self::nuget:serviceable">
              <ng:serviceable rdf:datatype="http://www.w3.org/2001/XMLSchema#boolean">
                <xsl:value-of select="."/>
              </ng:serviceable>
            </xsl:when>

            <xsl:when test="self::nuget:repository">
              <ng:repository>
                <rdf:Description>
                  <xsl:attribute name="rdf:about">
                    <xsl:value-of select="obj:LowerCase(concat($base, $path, $extension, '#repository'))"/>
                  </xsl:attribute>
                  <xsl:if test="@type">
                    <ng:type>
                      <xsl:value-of select="@type"/>
                    </ng:type>
                  </xsl:if>
                  <xsl:if test="@url">
                    <ng:url>
                      <xsl:value-of select="@url"/>
                    </ng:url>
                  </xsl:if>
                  <xsl:if test="@branch">
                    <ng:branch>
                      <xsl:value-of select="@branch"/>
                    </ng:branch>
                  </xsl:if>
                  <xsl:if test="@commit">
                    <ng:commit>
                      <xsl:value-of select="@commit"/>
                    </ng:commit>
                  </xsl:if>
                </rdf:Description>
              </ng:repository>
            </xsl:when>

            <xsl:when test="self::nuget:id and obj:IsValidPackageId(.)">
              <ng:id>
                <xsl:value-of select="."/>
              </ng:id>
            </xsl:when>

            <xsl:when test="self::nuget:version and obj:IsValidVersion(.)">
              <ng:version>
                <xsl:value-of select="obj:GetFullVersionString(.)"/>
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

            <xsl:when test="self::nuget:authors">
              <ng:authors>
                <xsl:value-of select="."/>
              </ng:authors>
            </xsl:when>

            <xsl:when test="self::nuget:description">
              <ng:description>
                <xsl:value-of select="."/>
              </ng:description>
            </xsl:when>

            <xsl:when test="self::nuget:copyright">
              <ng:copyright>
                <xsl:value-of select="."/>
              </ng:copyright>
            </xsl:when>

            <xsl:when test="self::nuget:projectUrl">
              <ng:projectUrl>
                <xsl:value-of select="."/>
              </ng:projectUrl>
            </xsl:when>

            <xsl:when test="self::nuget:releaseNotes">
              <ng:releaseNotes>
                <xsl:value-of select="."/>
              </ng:releaseNotes>
            </xsl:when>

            <xsl:when test="self::nuget:title">
              <ng:title>
                <xsl:value-of select="."/>
              </ng:title>
            </xsl:when>

            <xsl:when test="self::nuget:summary">
              <ng:summary>
                <xsl:value-of select="."/>
              </ng:summary>
            </xsl:when>

            <xsl:when test="self::nuget:language">
              <ng:language>
                <xsl:value-of select="."/>
              </ng:language>
            </xsl:when>

            <xsl:when test="self::nuget:iconUrl">
              <ng:iconUrl>
                <xsl:value-of select="."/>
              </ng:iconUrl>
            </xsl:when>

            <xsl:when test="self::nuget:licenseUrl">
              <ng:licenseUrl>
                <xsl:value-of select="."/>
              </ng:licenseUrl>
            </xsl:when>

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

  <xsl:template match="nuget:packageType">
    <xsl:param name="position" />
    <xsl:param name="path" />
    <xsl:param name="parent_fragment" />
    <ng:packageType>
      <ng:PackageType>

        <xsl:choose>
          <xsl:when test="@version">
            <xsl:variable name="fragment" select="concat(normalize-space(@name), '/', normalize-space(@version))" />
            <xsl:attribute name="rdf:about">
              <xsl:value-of select="concat(obj:LowerCase(concat($base, $path, $extension, $parent_fragment, '/')), $fragment)" />
            </xsl:attribute>
          </xsl:when>
          <xsl:otherwise>
            <xsl:attribute name="rdf:about">
              <xsl:value-of select="concat(obj:LowerCase(concat($base, $path, $extension, $parent_fragment, '/')), normalize-space(@name))" />
            </xsl:attribute>
          </xsl:otherwise>
        </xsl:choose>

        <xsl:if test="@name">
          <ng:name>
            <xsl:value-of select="normalize-space(@name)"/>
          </ng:name>
        </xsl:if>

        <xsl:if test="@version">
          <ng:version>
            <xsl:value-of select="normalize-space(@version)" />
          </ng:version>
        </xsl:if>

      </ng:PackageType>
    </ng:packageType>
  </xsl:template>

  <xsl:template match="nuget:dependency">
    <xsl:param name="path" />
    <xsl:param name="parent_fragment" />
    <xsl:if test="normalize-space(@id) != ''">
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
    </xsl:if>
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
