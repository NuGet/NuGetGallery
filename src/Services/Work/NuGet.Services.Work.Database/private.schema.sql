-- Schema that only admins have access to
-- Designed for encapsulation, not security. 
-- The users that non-human clients connect with will not have access to this, 
-- thus they cannot accidentally mess with things
CREATE SCHEMA [private] AUTHORIZATION dbo
