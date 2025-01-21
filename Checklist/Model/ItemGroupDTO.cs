using System.ComponentModel.DataAnnotations;

namespace JustAnotherListAPI.Checklist.Model
{
    public class ItemGroupDTO
    {
        public Guid Id { get; set; }
        [Required]
        public required string Name { get; set; }
        public int Complete { get; set; } = 0;
        public int Incomplete { get; set; } = 0;

        public static ItemGroupDTO Create(ItemGroup itemGroup)
        {
            return new()
            {
                Id = itemGroup.Id,
                Name = itemGroup.Name,
                Complete = itemGroup.Items == null ? 0 : itemGroup.Items.Where(i => i.IsComplete).Count(),
                Incomplete = itemGroup.Items == null ? 0 : itemGroup.Items.Where(i => i.IsComplete == false).Count()
            };
        }
    }
}