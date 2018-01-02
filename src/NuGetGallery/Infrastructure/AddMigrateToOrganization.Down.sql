-- Copyright (c) .NET Foundation. All rights reserved.
-- Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

SET ANSI_NULLS ON

IF OBJECT_ID('[dbo].[MigrateToOrganization]', 'P') IS NOT NULL
  DROP PROCEDURE  [dbo].[MigrateToOrganization]
GO