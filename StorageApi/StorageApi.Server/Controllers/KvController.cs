using Microsoft.AspNetCore.Mvc;
using StorageApi.Server.Models;

namespace StorageApi.Server.Controllers;

[ApiController]
[Route("api/v2/kv")]
[Tags("Key value store v2")]
public class KvController : ControllerBase
{
    private readonly IKvStore _store;

    public KvController(IKvStore store)
    {
        _store = store;
    }

    [HttpGet]
    public IActionResult ListKeys([FromQuery] string? prefix)
    {
        var keys = _store.ListKeys(prefix ?? "");
        return Ok(new { keys });
    }

    [HttpGet("{key}")]
    public IActionResult GetValue([FromRoute] string key)
    {
        if (!ValidationRules.ValidateKey(key, out var keyError))
            return BadRequest(new { error = keyError });

        if (!_store.TryGet(key, out var value))
            return NotFound();
        
        return Ok(new { key, value });
    }

    [HttpPut("{key}")]
    public IActionResult SetValue([FromRoute] string key, [FromBody] SetValueRequest request)
    {
        if (!ValidationRules.ValidateKey(key, out var keyError))
            return BadRequest(new { error = keyError });

        if (!ValidationRules.ValidateValue(request.Value, out var valueError))
            return BadRequest(new { error = valueError });

        if (!ValidationRules.ValidateTtl(request.TtlSeconds, out var ttlError))
            return BadRequest(new { error = ttlError });

        var result = _store.Upsert(key, request.Value, request.TtlSeconds);
        
        return result == UpsertResult.Created 
            ? Created($"/api/v2/kv/{key}", null) 
            : NoContent();
    }

    [HttpDelete("{key}")]
    public IActionResult DeleteValue([FromRoute] string key)
    {
        if (!ValidationRules.ValidateKey(key, out var keyError))
            return BadRequest(new { error = keyError });

        if (_store.TryRemove(key))
            return NoContent();
        
        return NotFound();
    }

    [HttpPost("batch")]
    public IActionResult BatchUpsert([FromBody] BatchUpsertRequest request)
    {
        if (request?.Items == null || request.Items.Count == 0)
            return BadRequest(new { error = "Request must include at least one item" });

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
            return BadRequest(new { errors });

        var results = new List<object>(request.Items.Count);
        foreach (var item in request.Items)
        {
            var result = _store.Upsert(item.Key, item.Value, item.TtlSeconds);
            results.Add(new { key = item.Key, result = result.ToString().ToLowerInvariant() });
        }

        return Ok(new { results });
    }
}
