
namespace TonieCreativeManager.UI2.Model
{
    public class GridItem
    {
        public string? ImageUrl { get; set; }
        public string? Name { get; set; }
        public string? Url { get; set; }
        public string? SubImageUrl { get; set; }
        public string? SubLeftImageUrl { get; set; }
        public bool IsTonieSubImage { get; set; }
        public bool Restricted { get; set; }
        public bool Disabled { get; set; }
        public string? SubIcon { get; set; }
        public Action? OnClick { get; set; }
        public Action? OnSubClick { get; set; }
    }
}
