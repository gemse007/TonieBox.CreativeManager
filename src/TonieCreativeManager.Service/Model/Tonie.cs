
namespace TonieCreativeManager.Service.Model
{
    public class Tonie
    {
        public Tonie(string id)
        {
            Id = id;
        }

        public string Id { get; set; }
        public string? Name { get; set; }
        public string? CurrentMediaPath { get; set; }
        public string? ImageUrl { get; set; }
    }
}
