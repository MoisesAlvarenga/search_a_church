using Microsoft.AspNetCore.Mvc;
using SearchAChurch.Api.Models;

namespace SearchAChurch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfilesController : ControllerBase
{
    private readonly Repositories.InMemoryStore _store;

    public ProfilesController(Repositories.InMemoryStore store)
    {
        _store = store;
    }

    [HttpPost]
    public IActionResult Create([FromBody] UserProfile profile)
    {
        profile.Id = Guid.NewGuid();
        _store.Profiles.Add(profile);
        return CreatedAtAction(nameof(Get), new { id = profile.Id }, profile);
    }

    [HttpGet("{id}")]
    public IActionResult Get(Guid id)
    {
        var profile = _store.Profiles.FirstOrDefault(p => p.Id == id);
        if (profile == null) return NotFound();
        return Ok(profile);
    }
}
