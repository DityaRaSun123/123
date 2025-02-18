using System;
using System.Data.Common;
using System.Data.SQLite;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cryptobot2._0_v.Services
{
    public class TatumService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly SQLiteConnection _connection;
        private const string API_KEY = "t-67ad9b255d7459d8235868e1-15addd67c6c84bc9978e539f"; // Замените на ваш API ключ
        private const string BASE_URL = "https://api-eu1.tatum.io/v3";

        public TatumService(ILogger logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", API_KEY);
            _connection = new SQLiteConnection("Data Source=cryptobot.db;Version=3;");
            _connection.Open();
        }
        public void Dispose()
        {
            _connection?.Dispose();
            _httpClient?.Dispose();
        }
        public async Task<(bool success, string privateKey, string error)> GetWalletPrivateKey(string username)
        {
            try
            {
                // Получаем адрес из базы данных
                using var command = new SQLiteCommand(
                    "SELECT wallet_addresses FROM users WHERE username = @username",
                    _connection);
                command.Parameters.AddWithValue("@username", username);

                var walletsJson = await command.ExecuteScalarAsync() as string;
                if (string.IsNullOrEmpty(walletsJson))
                {
                    return (false, null, "User not found");
                }

                // Парсим JSON
                var wallets = JsonConvert.DeserializeObject<Dictionary<string, string>>(walletsJson);
                if (!wallets.ContainsKey("TRX"))
                {
                    return (false, null, "TRX wallet not found");
                }

                var trxWalletJson = wallets["TRX"];
                var trxWallet = JsonConvert.DeserializeObject<Dictionary<string, string>>(trxWalletJson);
                var address = trxWallet["address"];

                // Запрашиваем приватный ключ через Tatum API
                var response = await _httpClient.GetAsync($"{BASE_URL}/tron/wallet/priv/{address}");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var keyData = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
                    if (keyData.ContainsKey("key"))
                    {
                        return (true, keyData["key"], null);
                    }
                    return (false, null, "Private key not found in response");
                }

                return (false, null, $"API Error: {content}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting private key");
                return (false, null, ex.Message);
            }
        }

        public async Task<(bool success, string privateKey, string error)> GetPrivateKeyFromMnemonicAsync(string address)
        {
            try
            {
                using var command = new SQLiteCommand(
                    "SELECT wallet_addresses FROM users WHERE json_extract(wallet_addresses, '$.TRX.address') = @address",
                    _connection);
                command.Parameters.AddWithValue("@address", address);

                var walletsJson = await command.ExecuteScalarAsync() as string;
                _logger.LogInformation($"Found wallet data: {walletsJson}");

                if (string.IsNullOrEmpty(walletsJson))
                {
                    return (false, null, "Wallet not found");
                }

                // Парсим JSON данные кошелька
                var wallets = JsonConvert.DeserializeObject<Dictionary<string, JObject>>(walletsJson);
                if (!wallets.ContainsKey("TRX"))
                {
                    return (false, null, "TRX wallet not found");
                }

                var tronWallet = wallets["TRX"].ToObject<WalletInfo>();
                if (string.IsNullOrEmpty(tronWallet.Mnemonic))
                {
                    _logger.LogWarning($"Mnemonic not found for address {address}");
                    return (false, null, "Mnemonic not found");
                }

                var requestData = new
                {
                    mnemonic = tronWallet.Mnemonic
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(requestData),
                    System.Text.Encoding.UTF8,
                    "application/json");

                _logger.LogInformation($"Requesting private key for address {address}");
                var response = await _httpClient.PostAsync($"{BASE_URL}/tron/wallet/priv", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Tatum API response: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var keyResponse = JsonConvert.DeserializeObject<KeyResponse>(responseContent);
                    return (true, keyResponse.Key, null);
                }

                return (false, null, $"Failed to get private key: {responseContent}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting private key from mnemonic for address {address}");
                return (false, null, ex.Message);
            }
        }

        private async Task<(bool success, string addressJson, string error)> CreateEthereumWalletAsync(string currency)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BASE_URL}/ethereum/wallet");
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Create wallet API response for {currency}: {content}");

                if (!response.IsSuccessStatusCode)
                {
                    return (false, null, content);
                }

                var walletResponse = JsonConvert.DeserializeObject<EthereumWalletResponse>(content);
                if (string.IsNullOrEmpty(walletResponse?.Xpub))
                {
                    return (false, null, "Invalid wallet response");
                }

                var addressResponse = await _httpClient.GetAsync($"{BASE_URL}/ethereum/address/{walletResponse.Xpub}/0");
                var addressContent = await addressResponse.Content.ReadAsStringAsync();

                if (!addressResponse.IsSuccessStatusCode)
                {
                    return (false, null, addressContent);
                }

                var address = JsonConvert.DeserializeObject<EthereumAddressResponse>(addressContent);
                return (true, JsonConvert.SerializeObject(new { address = address.Address }), null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        private async Task<(bool success, string addressJson, string error)> CreateTronWalletAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BASE_URL}/tron/wallet");
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Create wallet API response for TRX: {content}");

                if (!response.IsSuccessStatusCode)
                {
                    return (false, null, content);
                }

                var walletResponse = JsonConvert.DeserializeObject<TronWalletResponse>(content);
                if (string.IsNullOrEmpty(walletResponse?.Xpub))
                {
                    return (false, null, "Invalid wallet response");
                }

                var addressResponse = await _httpClient.GetAsync($"{BASE_URL}/tron/address/{walletResponse.Xpub}/0");
                var addressContent = await addressResponse.Content.ReadAsStringAsync();

                if (!addressResponse.IsSuccessStatusCode)
                {
                    return (false, null, addressContent);
                }

                var address = JsonConvert.DeserializeObject<TronAddressResponse>(addressContent);
                return (true, JsonConvert.SerializeObject(new { address = address.Address }), null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        private async Task<(bool success, string addressJson, string error)> CreateBitcoinWalletAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BASE_URL}/bitcoin/wallet");
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Create wallet API response for BTC: {content}");

                if (!response.IsSuccessStatusCode)
                {
                    return (false, null, content);
                }

                var walletResponse = JsonConvert.DeserializeObject<BitcoinWalletResponse>(content);
                if (string.IsNullOrEmpty(walletResponse?.Xpub))
                {
                    return (false, null, "Invalid wallet response");
                }

                var addressResponse = await _httpClient.GetAsync($"{BASE_URL}/bitcoin/address/{walletResponse.Xpub}/0");
                var addressContent = await addressResponse.Content.ReadAsStringAsync();

                if (!addressResponse.IsSuccessStatusCode)
                {
                    return (false, null, addressContent);
                }

                var address = JsonConvert.DeserializeObject<BitcoinAddressResponse>(addressContent);
                return (true, JsonConvert.SerializeObject(new { address = address.Address }), null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        public async Task<(bool success, decimal balance, string error)> GetBalanceAsync(string address, string currency)
        {
            try
            {
                if (string.IsNullOrEmpty(address))
                {
                    return (false, 0, "Address is null or empty");
                }

                string endpoint = currency.ToUpper() switch
                {
                    "BTC" => $"{BASE_URL}/bitcoin/address/balance/{address}",
                    "ETH" => $"{BASE_URL}/ethereum/account/balance/{address}",
                    "USDT" => $"{BASE_URL}/ethereum/account/balance/{address}",
                    "TRX" => $"{BASE_URL}/tron/account/{address}",
                    _ => throw new ArgumentException($"Unsupported currency: {currency}")
                };

                _logger.LogInformation($"Getting balance for {currency} at endpoint: {endpoint}");

                var response = await _httpClient.GetAsync(endpoint);
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"Balance API Response for {currency}: {content}");

                if (response.IsSuccessStatusCode)
                {
                    switch (currency.ToUpper())
                    {
                        case "BTC":
                            var btcBalance = JsonConvert.DeserializeObject<BTCBalanceResponse>(content);
                            return (true, (btcBalance.Incoming - btcBalance.Outgoing) / 100000000M, null);

                        case "ETH":
                        case "USDT":
                            var ethBalance = JsonConvert.DeserializeObject<ETHBalanceResponse>(content);
                            var divisor = currency == "ETH" ? 1000000000000000000M : 1000000M;
                            return (true, decimal.Parse(ethBalance.Balance) / divisor, null);

                        case "TRX":
                            var tronBalance = JsonConvert.DeserializeObject<TronBalanceResponse>(content);
                            return (true, tronBalance.Balance / 1000000M, null);

                        default:
                            return (false, 0, $"Unsupported currency: {currency}");
                    }
                }
                else
                {
                    // Специальная обработка для неактивированных TRX адресов
                    if (currency == "TRX" && content.Contains("tron.account.not.found"))
                    {
                        return (true, 0, null); // Возвращаем успех с нулевым балансом для неактивированных адресов
                    }

                    _logger.LogError($"Failed to get balance for {currency}: {content}");
                    return (false, 0, $"Failed to get balance: {content}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get balance for {currency}");
                return (false, 0, ex.Message);
            }
        }
        // Добавьте эти методы в класс TatumService

        public async Task<(bool success, string privateKey, string error)> GetPrivateKeyAsync(string address, string currency)
        {
            try
            {
                if (string.IsNullOrEmpty(address))
                {
                    return (false, null, "Address is null or empty");
                }

                // Сначала получаем xpub и индекс для адреса
                string xpubEndpoint = $"{BASE_URL}/tron/address/{address}";
                var xpubResponse = await _httpClient.GetAsync(xpubEndpoint);
                var xpubContent = await xpubResponse.Content.ReadAsStringAsync();

                if (!xpubResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to get xpub info: {xpubContent}");
                    return (false, null, $"Failed to get xpub info: {xpubContent}");
                }

                var xpubInfo = JsonConvert.DeserializeObject<XpubAddressResponse>(xpubContent);

                // Теперь получаем приватный ключ используя xpub и индекс
                string privKeyEndpoint = $"{BASE_URL}/tron/wallet/priv/{xpubInfo.Xpub}/{xpubInfo.Index}";

                _logger.LogInformation($"Getting private key for {currency} address: {address}");

                var response = await _httpClient.GetAsync(privKeyEndpoint);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var keyResponse = JsonConvert.DeserializeObject<PrivateKeyResponse>(content);
                    return (true, keyResponse.Key, null);
                }

                _logger.LogError($"Failed to get private key for {currency}: {content}");
                return (false, null, $"Failed to get private key: {content}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting private key for {currency}");
                return (false, null, ex.Message);
            }
        }
        public async Task<(bool success, string addressJson, string error)> CreateWalletAsync(string currency)
        {
            try
            {
                _logger.LogInformation($"Creating {currency} wallet");

                switch (currency.ToUpper())
                {
                    case "TRX":
                        return await CreateTronWalletAsync();
                    case "ETH":
                        return await CreateEthereumWalletAsync(currency);
                    case "BTC":
                        return await CreateBitcoinWalletAsync();
                    default:
                        return (false, null, $"Unsupported currency: {currency}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating {currency} wallet");
                return (false, null, ex.Message);
            }
        }

        public async Task<(bool success, string txId, string error)> SendTRXAsync(
            string fromAddress,
            string toAddress,
            decimal amount)
        {
            try
            {
                // Получаем приватный ключ отправителя
                var (keySuccess, privateKey, keyError) = await GetPrivateKeyAsync(fromAddress, "TRX");
                if (!keySuccess)
                {
                    return (false, null, $"Failed to get private key: {keyError}");
                }

                // Подготавливаем данные для отправки
                var requestData = new
                {
                    from = fromAddress,
                    to = toAddress,
                    amount = (amount * 1_000_000).ToString(), // Конвертируем в SUN (1 TRX = 1,000,000 SUN)
                    privateKey = privateKey
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(requestData),
                    System.Text.Encoding.UTF8,
                    "application/json");

                // Отправляем транзакцию
                var response = await _httpClient.PostAsync($"{BASE_URL}/tron/transaction", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var txResponse = JsonConvert.DeserializeObject<TransactionResponse>(responseContent);
                    _logger.LogInformation($"Successfully sent {amount} TRX from {fromAddress} to {toAddress}. TxID: {txResponse.TxId}");
                    return (true, txResponse.TxId, null);
                }

                _logger.LogError($"Failed to send TRX: {responseContent}");
                return (false, null, responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending TRX");
                return (false, null, ex.Message);
            }
        }
        
        private class WalletInfo
        {
            [JsonProperty("address")]
            public string Address { get; set; }

            [JsonProperty("mnemonic")]
            public string Mnemonic { get; set; }

            [JsonProperty("xpub")]
            public string Xpub { get; set; }
        }

        private class XpubAddressResponse
        {
            [JsonProperty("xpub")]
            public string Xpub { get; set; }

            [JsonProperty("index")]
            public int Index { get; set; }
        }

        // Добавьте эти классы для десериализации ответов
        private class PrivateKeyResponse
        {
            [JsonProperty("key")]
            public string Key { get; set; }
        }

        private class TransactionResponse
        {
            [JsonProperty("txId")]
            public string TxId { get; set; }
        }

        private class EthereumWalletResponse
        {
            [JsonProperty("mnemonic")]
            public string Mnemonic { get; set; }

            [JsonProperty("xpub")]
            public string Xpub { get; set; }
        }

        private class TronWalletResponse
        {
            [JsonProperty("mnemonic")]
            public string Mnemonic { get; set; }

            [JsonProperty("xpub")]
            public string Xpub { get; set; }
        }

        private class BitcoinWalletResponse
        {
            [JsonProperty("mnemonic")]
            public string Mnemonic { get; set; }

            [JsonProperty("xpub")]
            public string Xpub { get; set; }
        }

        private class EthereumAddressResponse
        {
            [JsonProperty("address")]
            public string Address { get; set; }
        }

        private class TronAddressResponse
        {
            [JsonProperty("address")]
            public string Address { get; set; }
        }

        private class BitcoinAddressResponse
        {
            [JsonProperty("address")]
            public string Address { get; set; }
        }

        private class BTCBalanceResponse
        {
            [JsonProperty("incoming")]
            public decimal Incoming { get; set; }

            [JsonProperty("outgoing")]
            public decimal Outgoing { get; set; }
        }

        private class ETHBalanceResponse
        {
            [JsonProperty("balance")]
            public string Balance { get; set; }
        }
        private class TronWalletInfo
        {
            [JsonProperty("address")]
            public string Address { get; set; }

            [JsonProperty("mnemonic")]
            public string Mnemonic { get; set; }

            [JsonProperty("xpub")]
            public string Xpub { get; set; }
        }
        private class KeyResponse
        {
            [JsonProperty("key")]
            public string Key { get; set; }
        }
       

        private class TronBalanceResponse
        {
            [JsonProperty("balance")]
            public decimal Balance { get; set; }
        }
    }
}