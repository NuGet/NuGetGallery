## Overview
A major prerequisite for OAuth and enhanced Auth in general is the ability to Merge Users. To reduce friction during the OAuth flow, we will always automatically create accounts when there isn't already one associated with that credential in our system. However, on first log-in via this mechanism, we can inform the users of a "merge" feature to allow all information associated with that account to be rolled in to another account. And if we're going to do that, it's natural at this point to just make a first-class merge feature.

## Stories
Frank has always used the alias "superhappyfunguy", and NuGet.org is no different. He's got a bunch of packages listed there and he has a pretty big group of followers. However, he's getting concerned that "superhappyfunguy" might not be the most professional username. So, he creates a new account with the name "franksmith". Then, he clicks on his User Name in the top right and selects Merge Accounts. He logs in with "superhappyfunguy" and accepts the merge. All his packages now show his more professional avatar.

**Stretch Feature:** [This story will be transferred to a new feature, just want to capture it somewhere]
Frank is happy with his new "franksmith" alias. However, some of the packages he's uploaded belong to his workplace, Contosoware. He uploaded them will full permission (since Contosoware offered them for free, under an open-source license, anyway) because Contosoware wasn't ready to have an official NuGet upload. Now, they're ready and his boss Bill has created a 'contosoware' account. Frank wants to transfer the packages, but there are 20 different packages, that's a lot of clicking! Instead, he starts the process on one package, but on the "Manage Package Owners" page, he sees a link: "Want to transfer multiple packages to a different owner?" He clicks it and is asked to enter the user he wants to add as an owner to his packages, he enters "contosoware". Then, he gets a checkbox list of packages to transfer (it looks a lot like the Merge accounts UI, it shows his account on the left and 
'contosoware' on the right). He chooses the Contosoware packages and clicks "Add Owner". The next screen informs him that an email has been sent to 'contosoware' to confirm the transfer (in one batch) and that he's done for now :).

Bill logs in to his email and sees the message about the transfer. He reviews the packages in the list and clicks the confirmation URL. The page informs him that he has accepted the transfer request and is now an owner of those packages.

## Design Goals
1. Users can easily merge user accounts
2. Merges are immediate
3. Merges are audited so that the NuGet Support can see a complete history of the changes made to the user
 * Audit logs need not be stored in an indexed data store (i.e. DB), they can be in a long-term persistent store like Blob storage
4. [Stretch Goal/Future Feature] The merge flow can also be used to transfer ownership of multiple packages without affecting other data.

## NON Goals
1. Multi-way merge
2. Users need not be able to view a history of their merges
3. This feature need not free up the source username

## Activity Flow
The activity flow is as follows, for merging User B's data IN TO User A:
1. User A selects the "Merge" option.
2. User A is informed about the merge process and is prompted to log in to User B's account
3. User A is shown a UI displaying the changes to be made:
 * User A can choose which email address and/or username to keep?
 * User B's username and email will be discarded, but will **remain reserved** (Question 1)
 * All packages that User B owns will have User A added as an owner and User B **removed permanently**
4. User A approves the changes by clicking submit
5. An Audit Log is written to blob storage indicating the list of changes to be made (see Auditing below)
6. The changes are applied to the database. User B's row in Users remains present but is marked as Disabled with a Status of "Merged with User A"

## Auditing
A sub-feature of this is Auditing. It's time to get a richer auditing flow in the Gallery and I think we can do it iteratively. My view of the flow is as follows:
* Audit logs are JSON files in Blob storage (in an "audit" container)
* Logs are sorted into folders by Entity Type and Entity Identifier (i.e. "users/anurse", "packages/jQuery/1.9.1")
* Logs are named with a UTC Timestamp and Guid (i.e. "07312013T1130Z_GUID.json")
* The "Guid" is the Audit Log ID and can uniquely identify an audit record if necessary
* The database holds a quick summary of the last Audit Record for each entity in some fashion. Specifically: it holds the GUID and a simple textual description. This could be via a separate table (AuditRecords) and FK relationships, or via a completely independent separate table (UserAuditRecords, etc.), or even just columns in each table (LastReportId, etc.). But it is still a one-to-one relationship. Timestamps can be used to gather a history from blob storage.
* **Question:** Maybe the audit log blob has a link to the previous blob?

## Questions:
1. When doing a complete merge, should User B's username be freed for a new user to claim? Or should that be left for support

## Implementation Notes:

### Database Changes:
1. New Columns in User:
 * AuditId - Int (NULL) - FK Reference to AuditRecords
 * Disabled - Bit (NOT NULL) - Default False. If True, user is "cloaked" and should appear to not exist to all Gallery functions.
2. New Table: AuditRecords:
 * AuditId - Int IDENTITY - Primary Key
 * EntityType - String - Name of the table containing the entity in question ("Packages", "Users")
 * EntityId - Int - Reference to the ID of the entity in the table in question (NOT an FK, as it could be in multiple different tables)
 * ReportId - String - GUID, or maybe full URL, to track back to the report
 * ReportTimestampUtc - datetime - Timestamp used in the report (must be identical to report name). If ReportId is a URL, this may be unnecessary
3. Changes to user lookup:
 * Add "AND Disabled = 0" to all queries for users across the system (login, package ownership, profile pages, etc.)