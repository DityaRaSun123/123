// Services/BinanceService.cs
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;

namespace CryptoWalletBot.Services
{
    public class BinanceService
    {
        private readonly string _apiKey;
        private readonly string _secretKey;
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.binance.com";

        public BinanceService(string apiKey, string secretKey)
        {
            _apiKey = apiKey;
            _secretKey = secretKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", _apiKey);
        }

        public async Task<decimal> GetCurrentPrice(string symbol = "TRXUSDT")
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"{BaseUrl}/api/v3/ticker/price?symbol={symbol}");
                var data = JObject.Parse(response);
                return decimal.Parse(data["price"].ToString());
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error getting price for {symbol}", ex);
                return 0;
            }
        }

        public async Task<decimal> GetAccountBalance(string asset = "TRX")
        {
            try
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                var queryString = $"timestamp={timestamp}";
                var signature = CreateSignature(queryString);

                var response = await _httpClient.GetStringAsync(
                    $"{BaseUrl}/api/v3/account?{queryString}&signature={signature}");

                var data = JObject.Parse(response);
                var balances = data["balances"].ToObject<JArray>();
                var assetBalance = balances.FirstOrDefault(b => b["asset"].ToString() == asset);

                return assetBalance != null ? decimal.Parse(assetBalance["free"].ToString()) : 0;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error getting balance for {asset}", ex);
                return 0;
            }
        }

        public async Task<bool> WithdrawCrypto(string address, decimal amount, string asset = "TRX")
        {
            try
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                var parameters = new Dictionary<string, string>
                {
                    {"coin", asset},
                    {"address", address},
                    {"amount", amount.ToString("0.########")},
                    {"timestamp", timestamp}
                };

                var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));
                var signature = CreateSignature(queryString);

                var content = new FormUrlEncodedContent(parameters.Concat(
                    new[] { new KeyValuePair<string, string>("signature", signature) }));

                var response = await _httpClient.PostAsync($"{BaseUrl}/sapi/v1/capital/withdraw/apply", content);
                var result = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    LoggingService.LogTransactionInfo(0, $"Withdrawal successful: {result}");
                    return true;
                }

                LoggingService.LogError($"Withdrawal failed: {result}");
                return false;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error during withdrawal", ex);
                return false;
            }
        }

        private string CreateSignature(string queryString)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
            return BitConverter.ToString(signatureBytes).Replace("-", "").ToLower();
        }

        public async Task<List<(string symbol, decimal price)>> GetAllPrices()
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"{BaseUrl}/api/v3/ticker/price");
                var data = JArray.Parse(response);

                return data.Select(token => (
                    token["symbol"].ToString(),
                    decimal.Parse(token["price"].ToString())
                )).ToList();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error getting all prices", ex);
                return new List<(string, decimal)>();
            }
        }

        public async Task<decimal> GetDailyVolume(string symbol = "TRXUSDT")
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"{BaseUrl}/api/v3/ticker/24hr?symbol={symbol}");
                var data = JObject.Parse(response);
                return decimal.Parse(data["volume"].ToString());
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error getting daily volume for {symbol}", ex);
                return 0;
            }
        }
    }
}