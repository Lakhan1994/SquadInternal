namespace SquadInternal.Models
{
    public class AssetCategory
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public int CreatedBy { get; set; }

        public ICollection<Asset> Assets { get; set; }
            = new List<Asset>();
    }
}
