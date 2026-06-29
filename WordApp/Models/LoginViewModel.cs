using System.ComponentModel.DataAnnotations;

namespace WordApp.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
        [Display(Name = "Kullanıcı Adı")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "CAPTCHA cevabı zorunludur.")]
        [Display(Name = "Güvenlik Sorusu")]
        public string CaptchaAnswer { get; set; } = string.Empty;
    }
}
