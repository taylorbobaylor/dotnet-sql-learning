-- ============================================================
-- 01 - Create Database
-- ============================================================
-- Creates the InterviewDemoDB database used for all hands-on
-- SQL performance scenarios.
-- ============================================================

USE master;
GO

IF DB_ID('InterviewDemoDB') IS NOT NULL
BEGIN
    ALTER DATABASE InterviewDemoDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE InterviewDemoDB;
END;
GO

CREATE DATABASE InterviewDemoDB
    COLLATE SQL_Latin1_General_CP1_CI_AS;
GO

USE InterviewDemoDB;
GO

PRINT '✅ Database InterviewDemoDB created.';
GO
