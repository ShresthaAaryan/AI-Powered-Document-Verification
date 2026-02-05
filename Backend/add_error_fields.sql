-- Add ErrorMessage and UserActionRequired columns to Verifications table
-- Run this script on your PostgreSQL database

ALTER TABLE "Verifications" 
ADD COLUMN IF NOT EXISTS "ErrorMessage" VARCHAR(2000) NULL,
ADD COLUMN IF NOT EXISTS "UserActionRequired" VARCHAR(1000) NULL;

