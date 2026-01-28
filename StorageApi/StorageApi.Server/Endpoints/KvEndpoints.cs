using Microsoft.AspNetCore.Mvc;
using StorageApi.Server.Models;

namespace StorageApi.Server.Endpoints;

public static class KvEndpoints
{
    public static RouteGroupBuilder MapKvEndpoints(this IEndpointRouteBuilder app)
    {
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

            if (!ValidationRules.ValidateTtl(request.TtlSeconds, out var ttlError))
                return Results.BadRequest(new { error = ttlError });

            var result = store.Upsert(key, request.Value, request.TtlSeconds);
            
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

        kvApi.MapPost("/batch", ([FromBody] BatchUpsertRequest request, IKvStore store) =>
        {
            if (request?.Items == null || request.Items.Count == 0)
                return Results.BadRequest(new { error = "Request must include at least one item" });

            var errors = new List<object>();
            foreach (var item in request.Items)
            {
                if (!ValidationRules.ValidateKey(item.Key, out var keyError))
                    errors.Add(new { item.Key, error = keyError });
                else if (!ValidationRules.ValidateValue(item.Value, out var valueError))
                    errors.Add(new { item.Key, error = valueError });
                else if (!ValidationRules.ValidateTtl(item.TtlSeconds, out var ttlError))
                    errors.Add(new { item.Key, error = ttlError });
            }

            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            var results = new List<object>(request.Items.Count);
            foreach (var item in request.Items)
            {
                var result = store.Upsert(item.Key, item.Value, item.TtlSeconds);
                results.Add(new { key = item.Key, result = result.ToString().ToLowerInvariant() });
            }

            return Results.Ok(new { results });
        });

        return kvApi;
    }
}
