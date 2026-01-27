var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL database
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("verdure_mcp_data")
    .WithPgAdmin(
       c => c.WithImage("dpage/pgadmin4")
             .WithImageTag("9.9.0")
             .WithHostPort(5052)
    );

var verdureMcpDb = postgres.AddDatabase("DefaultConnection");

// Add API service (now includes Blazor WebAssembly static files)
// The API project references the Web project, so Blazor WASM will be served from the API
builder.AddProject<Projects.Verdure_Mcp_Server>("api")
    .WithReference(verdureMcpDb)
    .WaitFor(postgres)
    .WithExternalHttpEndpoints();

// Note: Web project is no longer needed as a separate service
// It's now served as static files from the API project

builder.Build().Run();
