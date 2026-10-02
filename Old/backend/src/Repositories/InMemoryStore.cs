using SearchAChurch.Api.Models;

namespace SearchAChurch.Api.Repositories;

public class InMemoryStore
{
    public List<Church> Churches { get; } = new List<Church>();
    public List<UserProfile> Profiles { get; } = new List<UserProfile>();
    public List<Review> Reviews { get; } = new List<Review>();

    public InMemoryStore()
    {
        // seed sample churches
        Churches.AddRange(new [] {
            new Church { Id = Guid.NewGuid(), Name = "St. Mary", Address = "1 Main St", Latitude = -23.55052, Longitude = -46.633308, ProfileTags = new List<string>{"traditional","portuguese"}, ServiceType = "traditional", Source = "app", IsRegistered=true },
            new Church { Id = Guid.NewGuid(), Name = "Grace Church", Address = "2 Elm St", Latitude = -23.559616, Longitude = -46.625, ProfileTags = new List<string>{"contemporary","english"}, ServiceType = "contemporary", Source = "app", IsRegistered=true },
            new Church { Id = Guid.NewGuid(), Name = "Open Worship", Address = "3 Oak St", Latitude = -23.562, Longitude = -46.64, ProfileTags = new List<string>{"contemporary","english"}, ServiceType = "contemporary", Source = "maps", IsRegistered=false }
        });
    }
}
