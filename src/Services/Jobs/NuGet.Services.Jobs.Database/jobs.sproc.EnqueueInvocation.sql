CREATE PROCEDURE [jobs].[EnqueueInvocation]
	@Job nvarchar(50),
	@Source nvarchar(50),
	@Payload nvarchar(MAX) = NULL,
	@NextVisibleAt datetime2,
	@InstanceName nvarchar(100)
AS
	-- Insert a row with a completely new Id
	INSERT INTO [private].InvocationsStore(
            [Id],
            [Job],
            [Source],
            [Payload],
            [Status],
            [Result],
            [ResultMessage],
            [UpdatedBy],
            [LogUrl],
            [DequeueCount],
            [IsContinuation],
            [Complete],
            [LastDequeuedAt],
            [LastSuspendedAt],
            [CompletedAt],
            [QueuedAt],
            [NextVisibleAt],
            [UpdatedAt])
	OUTPUT  inserted.*
	VALUES(
		    NEWID(),			-- Id
		    @Job,				-- Job
		    @Source,			-- Source
		    @Payload,			-- Payload
		    1,			        -- Status (Queued)
		    0,		            -- Result (Incomplete)
            NULL,               -- Result Message
		    @InstanceName,		-- UpdatedBy
            NULL,               -- LogUrl
		    0,					-- DequeueCount
		    0,					-- IsContinuation
		    0,					-- Complete
            NULL,               -- LastDequeuedAt
            NULL,               -- LastSuspendedAt
            NULL,               -- CompletedAt
		    SYSUTCDATETIME(),	-- QueuedAt
		    @NextVisibleAt,		-- NextVisibleAt
		    SYSUTCDATETIME())	-- UpdatedAt