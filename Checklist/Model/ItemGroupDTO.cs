using System.ComponentModel.DataAnnotations;

namespace JustAnotherListAPI.Checklist.Model
{
    public class ItemGroupDTO
    {
        [Required]
        public required string Name { get; set; }

        public static ItemGroupDTO Create(ItemGroup itemGroup)
        {
            return new()
            {
                Name = itemGroup.Name
            };
        }
    }
}