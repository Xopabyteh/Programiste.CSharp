using Scalar.AspNetCore;
using StorageApi.Server;
using StorageApi.Server.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSingleton<IKvStore, InMemoryKvStore>();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/", () => Results.Redirect("/scalar"));

app.MapKvEndpoints();
app.MapControllers();

app.Run();