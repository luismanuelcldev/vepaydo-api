-- One-time temporary passwords: a member created by an OWNER must change it
-- before reaching any tenant route (section 9.5 / Phase 2).
ALTER TABLE "users" ADD COLUMN "must_change_password" BOOLEAN NOT NULL DEFAULT false;
