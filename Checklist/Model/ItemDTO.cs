using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace JustAnotherListAPI.Checklist.Model
{
    public class ItemDTO
    {
        [Required]
        public required string Name { get; set; }
        public string? Description { get; set; }
        [DefaultValue(false)]
        public bool IsComplete { get; set; }

        public static ItemDTO Create(Item item)
        {
            return new()
            {
                Name = item.Name,
                Description = item.Description,
                IsComplete = item.IsComplete
            };
        }
    }
}
