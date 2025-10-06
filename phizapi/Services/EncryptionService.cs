using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace phizapi.Services
{
    public class EncryptionService
    {
        private readonly IConfiguration _config;
        public EncryptionService(IConfiguration config)
        {
            _config = config;
        }
        public string Sha256(string input)
        {
            string key = _config["Sha256:Key"];
            string combined = key + input;

            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(combined);
                byte[] hash = sha256.ComputeHash(bytes);
                return Convert.ToHexString(hash).ToLower();
            }
        }

    }
}
