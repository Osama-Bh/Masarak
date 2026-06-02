using System.ComponentModel.DataAnnotations;

namespace GoWork.DTOs.NotificationDTOs
{
    public class RegisterDeviceTokenDTO
    {
        [Required(ErrorMessage = "Token is required.")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Device type is required.")]
        public string DeviceType { get; set; } = string.Empty;
    }
}
