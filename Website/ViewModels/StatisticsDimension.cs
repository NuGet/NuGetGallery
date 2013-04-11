
namespace NuGetGallery
{
    public class StatisticsDimension
    {
        public string Name { get; set; }
        public string Id { get { return "dimension-" + Name; } }
        public bool IsChecked { get; set; }
    }
}