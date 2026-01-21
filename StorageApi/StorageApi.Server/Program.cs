using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using StorageApi.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IKvStore, InMemoryKvStore>();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/", () => Results.Redirect("/scalar"));

var kvApi = app.MapGroup("/kv")
	.WithTags("Key value store");

kvApi.MapGet("/", ([FromQuery] string? prefix, IKvStore store) =>
{
	var keys = store.ListKeys(prefix ?? "");
	return Results.Ok(new { keys });
});

kvApi.MapGet("/{key}", ([FromRoute] string key, IKvStore store) =>
{
	if (!ValidationRules.ValidateKey(key, out var keyError))
		return Results.BadRequest(new { error = keyError });

	if (!store.TryGet(key, out var value))
		return Results.NotFound();
	
	return Results.Ok(new { key, value });
});

kvApi.MapPut("/{key}", ([FromRoute] string key, [FromBody] SetValueRequest request, IKvStore store) =>
{
	if (!ValidationRules.ValidateKey(key, out var keyError))
		return Results.BadRequest(new { error = keyError });

	if (!ValidationRules.ValidateValue(request.Value, out var valueError))
		return Results.BadRequest(new { error = valueError });

	var result = store.Upsert(key, request.Value);
	
	return result == UpsertResult.Created 
		? Results.Created($"/kv/{key}", null) 
		: Results.NoContent();
});

kvApi.MapDelete("/{key}", ([FromRoute] string key, IKvStore store) =>
{
	if (!ValidationRules.ValidateKey(key, out var keyError))
		return Results.BadRequest(new { error = keyError });

	if (store.TryRemove(key))
		return Results.NoContent();
	
	return Results.NotFound();
});

app.Run();

public sealed class SetValueRequest
{
	public string? Value { get; set; }
}