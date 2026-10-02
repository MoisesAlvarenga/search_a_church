using Microsoft.AspNetCore.Mvc;
using SearchAChurch.Api.Models;

namespace SearchAChurch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly Repositories.InMemoryStore _store;

    public ReviewsController(Repositories.InMemoryStore store)
    {
        _store = store;
    }

    [HttpPost]
    public IActionResult Create([FromBody] Review review)
    {
        review.Id = Guid.NewGuid();
        review.CreatedAt = DateTime.UtcNow;
        _store.Reviews.Add(review);
        return CreatedAtAction(nameof(GetByChurch), new { id = review.ChurchId }, review);
    }

    [HttpGet("church/{id}")]
    public IActionResult GetByChurch(Guid id)
    {
        var reviews = _store.Reviews.Where(r => r.ChurchId == id).OrderByDescending(r => r.CreatedAt).ToList();
        return Ok(reviews);
    }
}
