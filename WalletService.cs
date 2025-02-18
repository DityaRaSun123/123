using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SQLite;
using Newtonsoft.Json;
using CryptoWalletBot.Models;
using Cryptobot2._0_v.Services;

namespace CryptoWalletBot.Services
{
    public class WalletService
    {
        private readonly SQLiteConnection _connection;
        private readonly TatumService _tatumService;
        private readonly BinanceService _binanceService;

        // Единый конструктор, объединяющий оба варианта
        public WalletService(SQLiteConnection connection, TatumService tatumService, BinanceService binanceService = null)
        {
            _connection = connection;
            _tatumService = tatumService;
            _binanceService = binanceService;
        }

        public async Task<bool> SendCrypto(string address, decimal amount, string currency = "TRX")
        {
            try
            {
                if (_binanceService == null)
                {
                    LoggingService.LogError($"BinanceService is not initialized");
                    return false;
                }

                return await _binanceService.WithdrawCrypto(address, amount, currency);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error sending {currency}", ex);
                return false;
            }
        }

        public async Task<Dictionary<string, string>> CreateNewWallets(bool onlyMissing = true)
        {
            var wallets = new Dictionary<string, string>();
            var currencies = new[] { "TRX", "BTC", "ETH" };

            foreach (var currency in currencies)
            {
                try
                {
                    if (onlyMissing)
                    {
                        // Проверяем существование кошелька
                        var (exists, _) = await CheckWalletExists(currency);
                        if (exists)
                        {
                            continue;
                        }
                    }

                    var (success, addressJson, error) = await _tatumService.CreateWalletAsync(currency);
                    if (success && !string.IsNullOrEmpty(addressJson))
                    {
                        wallets[currency] = addressJson;
                        LoggingService.LogTransactionInfo(0, $"Successfully created {currency} wallet");
                    }
                    else
                    {
                        LoggingService.LogError($"Failed to create {currency} wallet: {error}");
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Exception while creating {currency} wallet", ex);
                }
            }

            return wallets;
        }

        public async Task<bool> IsTronWalletInactive(string address)
        {
            try
            {
                var (success, balance, _) = await _tatumService.GetBalanceAsync(address, "TRX");
                return success && balance == 0; // Используем decimal напрямую
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error checking TRX wallet status for {address}", ex);
                return true;
            }
        }
        public class AddressResponse
        {
            [JsonProperty("address")]
            public string Address { get; set; }
        }
        public async Task<(bool success, string address)> CheckWalletExists(string currency)
        {
            try
            {
                using var command = new SQLiteCommand(
                    "SELECT wallet_addresses FROM users WHERE json_extract(wallet_addresses, @path) IS NOT NULL LIMIT 1",
                    _connection);
                command.Parameters.AddWithValue("@path", $"$.{currency}");

                var walletsJson = await command.ExecuteScalarAsync() as string;
                if (string.IsNullOrEmpty(walletsJson))
                {
                    return (false, null);
                }

                var wallets = JsonConvert.DeserializeObject<Dictionary<string, string>>(walletsJson);
                if (!wallets.TryGetValue(currency, out var addressJson))
                {
                    return (false, null);
                }

                var addressObj = JsonConvert.DeserializeObject<AddressResponse>(addressJson);
                return (addressObj?.Address != null, addressObj?.Address);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error checking wallet existence for {currency}", ex);
                return (false, null);
            }
        }

        public async Task<(bool success, string address, decimal balance)> GetWalletInfo(long userId, string currency)
        {
            try
            {
                using var command = new SQLiteCommand(
                    "SELECT wallet_addresses FROM users WHERE user_id = @userId",
                    _connection);
                command.Parameters.AddWithValue("@userId", userId);

                var walletsJson = await command.ExecuteScalarAsync() as string;
                if (string.IsNullOrEmpty(walletsJson))
                {
                    return (false, null, 0);
                }

                var wallets = JsonConvert.DeserializeObject<Dictionary<string, string>>(walletsJson);
                if (!wallets.TryGetValue(currency, out var addressJson))
                {
                    return (false, null, 0);
                }

                var addressObj = JsonConvert.DeserializeObject<AddressResponse>(addressJson);
                if (addressObj?.Address == null)
                {
                    return (false, null, 0);
                }

                var (success, balance, error) = await _tatumService.GetBalanceAsync(addressObj.Address, currency);
                if (!success)
                {
                    return (true, addressObj.Address, 0); // Возвращаем адрес, но с нулевым балансом в случае ошибки
                }

                return (true, addressObj.Address, balance);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error getting wallet info for {currency}", ex);
                return (false, null, 0);
            }
        }
    }
}