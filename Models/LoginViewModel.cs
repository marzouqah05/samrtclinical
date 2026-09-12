namespace WebApplication1.Models
{
    public class LoginViewModel
    {
        public string? EmailOrUsername { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool RememberMe { get; set; }
    }
}
