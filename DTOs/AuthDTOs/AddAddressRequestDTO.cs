using System.ComponentModel.DataAnnotations;

namespace GoWork.DTOs.AuthDTOs
{
    public class AddAddressRequestDTO
    {
        [Required(ErrorMessage = "Country is required.")]
        public int CountryId { get; set; }

        [Required(ErrorMessage = "Governate is required.")]
        public int GovernateId { get; set; }

        [Required(ErrorMessage = "Address line is required.")]
        [StringLength(255, ErrorMessage = "Address line cannot exceed 255 characters.")]
        public string AddressLine1 { get; set; } = null!;
    }
}
