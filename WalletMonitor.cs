using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Data.SQLite;
using Newtonsoft.Json;
using Telegram.Bot;

namespace Cryptobot2._0_v.Services
{
    public class WalletMonitor
    {
        private readonly ILogger _logger;
        private readonly TatumService _tatumService;
        private readonly ITelegramBotClient _botClient;
        private readonly SQLiteConnection _connection;
        private readonly Timer _timer;
        private const int CHECK_INTERVAL = 600000; // 30 секунд

        public WalletMonitor(ILogger logger, TatumService tatumService, ITelegramBotClient botClient, SQLiteConnection connection)
        {
            _logger = logger;
            _tatumService = tatumService;
            _botClient = botClient;
            _connection = connection;

            CreateBalanceTable();

            _timer = new Timer(async _ => await CheckBalances(), null, CHECK_INTERVAL, CHECK_INTERVAL);
        }

        private void CreateBalanceTable()
        {
            string createBalancesTable = @"
                CREATE TABLE IF NOT EXISTS balances (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id INTEGER,
                    currency TEXT,
                    balance DECIMAL(28,8),
                    last_checked DATETIME,
                    UNIQUE(user_id, currency)
                )";

            using var command = new SQLiteCommand(createBalancesTable, _connection);
            command.ExecuteNonQuery();
        }

        private async Task CheckBalances()
        {
            try
            {
                // Получаем всех пользователей и их кошельки
                using var command = new SQLiteCommand(
                    "SELECT user_id, wallet_addresses FROM users",
                    _connection);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var userId = reader.GetInt64(0);
                    var walletsJson = reader.GetString(1);
                    var wallets = JsonConvert.DeserializeObject<Dictionary<string, string>>(walletsJson);

                    foreach (var (currency, addressJson) in wallets)
                    {
                        await CheckWalletBalance(userId, currency, addressJson);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking balances");
            }
        }

        private async Task CheckWalletBalance(long userId, string currency, string addressJson)
        {
            try
            {
                var addressObject = JsonConvert.DeserializeObject<AddressResponse>(addressJson);
                if (addressObject?.Address == null) return;

                var (success, currentBalance, error) = await _tatumService.GetBalanceAsync(addressObject.Address, currency);
                if (!success) return;

                // Получаем предыдущий баланс
                decimal previousBalance = GetPreviousBalance(userId, currency);

                // Если баланс изменился
                if (currentBalance > previousBalance)
                {
                    decimal difference = currentBalance - previousBalance;
                    await NotifyUser(userId, currency, difference);
                }

                // Обновляем баланс в базе
                UpdateBalance(userId, currency, currentBalance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking balance for {currency}");
            }
        }

        private decimal GetPreviousBalance(long userId, string currency)
        {
            using var command = new SQLiteCommand(
                "SELECT balance FROM balances WHERE user_id = @userId AND currency = @currency",
                _connection);

            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@currency", currency);

            var result = command.ExecuteScalar();
            return result != null ? Convert.ToDecimal(result) : 0m;
        }

        private void UpdateBalance(long userId, string currency, decimal balance)
        {
            using var command = new SQLiteCommand(@"
                INSERT OR REPLACE INTO balances (user_id, currency, balance, last_checked)
                VALUES (@userId, @currency, @balance, @lastChecked)",
                _connection);

            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@currency", currency);
            command.Parameters.AddWithValue("@balance", balance);
            command.Parameters.AddWithValue("@lastChecked", DateTime.UtcNow);

            command.ExecuteNonQuery();
        }

        private async Task NotifyUser(long userId, string currency, decimal amount)
        {
            string currencyName = currency.ToUpper() switch
            {
                "ETH" => "Ethereum",
                "BTC" => "Bitcoin",
                "USDT" => "Tether",
                "TRX" => "TRON",
                _ => currency
            };

            var message = $"💰 *Получено пополнение!*\n\n" +
                         $"🪙 Криптовалюта: {currencyName}\n" +
                         $"💵 Сумма: {amount:F8} {currency}\n" +
                         $"🕒 Время: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";

            try
            {
                await _botClient.SendTextMessageAsync(
                    userId,
                    message,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification to user {userId}");
            }
        }

        private class AddressResponse
        {
            [JsonProperty("address")]
            public string Address { get; set; }
        }
    }
}