-- RUNNING THIS SCRIPT
--  1. Connect to the database
--  2. Execute the script
--  3. Save the output table for your records

DECLARE @affected TABLE(
    username nvarchar(255),
    field nvarchar(255),
    how nvarchar(255)
)

INSERT INTO @affected(username, field, how)
SELECT u.Username as username, 'ApiKey' AS field, 'OLD API key lost' as how
FROM Users u
INNER JOIN Credentials c ON c.Type = 'apikey.v1' AND u.[Key] = c.UserKey
WHERE LOWER(u.ApiKey) != c.Value

INSERT INTO @affected(username, field, how)
SELECT u.Username as username, 'Password' AS field, 'OLD SHA1 password lost' as how
FROM Users u
INNER JOIN Credentials c ON c.Type = 'password.sha1' AND u.[Key] = c.UserKey AND u.PasswordHashAlgorithm = 'SHA1'
WHERE u.HashedPassword != c.Value

INSERT INTO @affected(username, field, how)
SELECT u.Username as username, 'Password' AS field, 'OLD PBKDF2 password lost' as how
FROM Users u
INNER JOIN Credentials c ON c.Type = 'password.pbkdf2' AND u.[Key] = c.UserKey AND u.PasswordHashAlgorithm = 'PBKDF2'
WHERE u.HashedPassword != c.Value

SELECT * FROM @affected

ALTER TABLE Users ALTER COLUMN ApiKey uniqueidentifier NULL
ALTER TABLE Users ALTER COLUMN PasswordHashAlgorithm nvarchar(max) NULL