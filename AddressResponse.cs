// Models/AddressResponse.cs
using Newtonsoft.Json;

namespace CryptoWalletBot.Models
{
    public class AddressResponse
    {
        [JsonProperty("address")]
        public string Address { get; set; }
    }
}