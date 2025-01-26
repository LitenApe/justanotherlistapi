using System.ComponentModel.DataAnnotations;

namespace JustAnotherListAPI.Checklist.Model
{
    public class ItemGroupDTO
    {
        public Guid Id { get; set; }
        [Required]
        public required string Name { get; set; }
        public ICollection<Item> Items { get; set; } = [];

        public static ItemGroupDTO Create(ItemGroup itemGroup)
        {
            return new()
            {
                Id = itemGroup.Id,
                Name = itemGroup.Name,
                Items = itemGroup.Items
            };
        }
    }
}