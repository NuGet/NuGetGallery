// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public class AzurePipelinesCriteriaFacts
    {
        [Fact]
        public void NormalizesAllValues()
        {
            var criteria = new AzurePipelinesCriteria
            {
                OrganizationId = " organization ",
                ProjectId = " project ",
                DefinitionId = " definition ",
                RepositoryId = " repository ",
                RepositoryRef = " refs/heads/main ",
            };

            Assert.Equal("organization", criteria.OrganizationId);
            Assert.Equal("project", criteria.ProjectId);
            Assert.Equal("definition", criteria.DefinitionId);
            Assert.Equal("repository", criteria.RepositoryId);
            Assert.Equal("refs/heads/main", criteria.RepositoryRef);
        }

        [Fact]
        public void RejectsAllMissingValues()
        {
            var error = new AzurePipelinesCriteria().Validate();

            Assert.NotNull(error);
            Assert.Contains("organization ID", error);
            Assert.Contains("project ID", error);
            Assert.Contains("pipeline definition ID", error);
            Assert.Contains("repository ID", error);
            Assert.Contains("repository ref", error);
        }

        [Fact]
        public void AcceptsOpaqueNonEmptyValues()
        {
            var criteria = new AzurePipelinesCriteria
            {
                OrganizationId = "not-a-guid",
                ProjectId = "also-not-a-guid",
                DefinitionId = "not-a-number",
                RepositoryId = "provider-specific-repository",
                RepositoryRef = "provider-specific-ref",
            };

            Assert.Null(criteria.Validate());
        }

        [Fact]
        public void RoundTripsDatabaseJson()
        {
            var criteria = new AzurePipelinesCriteria
            {
                OrganizationId = "organization",
                ProjectId = "project",
                DefinitionId = "definition",
                RepositoryId = "repository",
                RepositoryRef = "refs/heads/main",
            };

            var json = criteria.ToDatabaseJson();
            var restored = AzurePipelinesCriteria.FromDatabaseJson(json);

            Assert.Equal(criteria.OrganizationId, restored.OrganizationId);
            Assert.Equal(criteria.ProjectId, restored.ProjectId);
            Assert.Equal(criteria.DefinitionId, restored.DefinitionId);
            Assert.Equal(criteria.RepositoryId, restored.RepositoryId);
            Assert.Equal(criteria.RepositoryRef, restored.RepositoryRef);
            Assert.Contains("\"org_id\":\"organization\"", json);
            Assert.Contains("\"prj_id\":\"project\"", json);
            Assert.Contains("\"def_id\":\"definition\"", json);
            Assert.Contains("\"rpo_id\":\"repository\"", json);
            Assert.Contains("\"rpo_ref\":\"refs/heads/main\"", json);
        }
    }
}
