-- Bind a refresh session to the tenant the user selected, so rotating a refresh
-- token can re-issue a tenant-scoped access token without an extra round trip.
ALTER TABLE "sessions" ADD COLUMN "tenant_id" UUID;

ALTER TABLE "sessions"
  ADD CONSTRAINT "sessions_tenant_id_fkey"
  FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id")
  ON DELETE SET NULL ON UPDATE CASCADE;
