-- If the columns already exist but the migration record is missing, run this:
-- This will mark the migration as applied without trying to add the columns again

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251213161255_AddErrorMessageAndUserActionRequiredColumns', '8.0.8')
ON CONFLICT ("MigrationId") DO NOTHING;

