# 07.12-admin-physical-removal: Discuss Admin area physical file removal strategy

## Objective
Discuss and decide whether Admin area files need to be physically removed from NoAdmin artifact for security.

## Current State (Post-Migration)
- Both artifacts contain identical DLLs with all Admin code
- Both artifacts contain Areas/Admin files on disk
- NoAdmin artifact has AdminPanelEnabled=false, making Admin routes inaccessible
- Runtime configuration prevents Admin functionality

## Discussion Points

### Option 1: Keep Current (Runtime Only)
**Pros:**
- Simplest approach
- Matches current .NET Framework behavior
- Easy to toggle without rebuild

**Cons:**
- Admin code present in NoAdmin artifact (security concern?)
- Slightly larger deployment size

### Option 2: Physical File Removal
**Approach**: MSBuild target in NoAdmin.pubxml to delete Areas/Admin after publish

**Pros:**
- Admin files physically absent from NoAdmin artifact
- Clearer security posture
- Smaller deployment

**Cons:**
- More complex build
- Need to list all files/folders to remove

### Option 3: Conditional Compilation
**Approach**: Use #if ADMIN_ENABLED to exclude Admin code at compile time

**Pros:**
- Admin code not in DLLs for NoAdmin build
- Strongest security

**Cons:**
- Most complex (requires separate build configurations)
- Different from current approach
- Harder to maintain

## Recommendation
Discuss with user and implement chosen approach.

## Dependencies
- Blocked on: 07.11 (need migration complete to evaluate)

## Done When
- Decision made
- Implementation complete (if needed)
- Both artifacts verified
