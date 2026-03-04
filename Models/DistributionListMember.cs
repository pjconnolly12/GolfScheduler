using System.ComponentModel.DataAnnotations;

namespace MyApp.Models
{
    public class DistributionListMember
    {
        [Required]
        public string OwnerUserId { get; set; } = string.Empty;

        [Required]
        public string MemberUserId { get; set; } = string.Empty;
    }
}
