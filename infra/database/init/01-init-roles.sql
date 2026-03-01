-- Create roles
CREATE ROLE dotnet_backend WITH LOGIN PASSWORD 'your_backend_pw';
CREATE ROLE nuxt_frontend WITH LOGIN PASSWORD 'your_frontend_pw';
-- Grant access to the database Docker just created for us
GRANT CONNECT
ON DATABASE heimdall_db TO dotnet_backend,
nuxt_frontend;
-- Grant schema permissions
GRANT USAGE
ON SCHEMA public TO dotnet_backend,
nuxt_frontend;
GRANT SELECT
   ,
   INSERT,
   UPDATE ,
   DELETE 
ON ALL TABLES IN SCHEMA public TO dotnet_backend;
GRANT USAGE SELECT
   
ON ALL SEQUENCES IN SCHEMA public TO dotnet_backend;
-- Restrict frontend to only what it needs (Better-Auth tables)
GRANT SELECT
   ,
   INSERT,
   UPDATE ,
   DELETE 
ON TABLE "user",
   "session",
   "account",
   "verification" TO nuxt_frontend;
