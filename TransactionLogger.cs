// Services/TransactionLogger.cs
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using CryptoWalletBot.Models;

namespace CryptoWalletBot.Services
{
    public static class TransactionLogger
    {
        private static readonly string LogPath = "transactions.log";
        private static readonly string ActiveDealsPath = "active_deals.json";

        public static void LogTransaction(long chatId, UserState userState, string cardDetails)
        {
            var logEntry = new
            {
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                ChatId = chatId,
                UserLogin = "NikoBabby",
                TransactionType = userState is BuyUserState ? "Buy" : "Sell",
                Currency = userState.Currency,
                Amount = userState.Amount,
                Bank = userState.BankName,
                Seller = userState.SellerName,
                CardDetails = cardDetails,
                Status = userState.DealStatus,
                CreatedAt = userState.CreatedAt
            };

            var logMessage = JsonConvert.SerializeObject(logEntry, Formatting.Indented);

            try
            {
                System.IO.File.AppendAllText(LogPath, logMessage + Environment.NewLine);

                // Сохраняем активную сделку в отдельный файл
                var activeDeals = LoadActiveDeals();
                activeDeals[chatId] = logEntry;
                SaveActiveDeals(activeDeals);

                LoggingService.LogTransactionInfo(chatId, $"Transaction logged: {logMessage}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error logging transaction", ex);
            }
        }

        private static Dictionary<long, object> LoadActiveDeals()
        {
            try
            {
                if (System.IO.File.Exists(ActiveDealsPath))
                {
                    var json = System.IO.File.ReadAllText(ActiveDealsPath);
                    return JsonConvert.DeserializeObject<Dictionary<long, object>>(json)
                           ?? new Dictionary<long, object>();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error loading active deals", ex);
            }
            return new Dictionary<long, object>();
        }

        private static void SaveActiveDeals(Dictionary<long, object> deals)
        {
            try
            {
                var json = JsonConvert.SerializeObject(deals, Formatting.Indented);
                System.IO.File.WriteAllText(ActiveDealsPath, json);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error saving active deals", ex);
            }
        }

        public static void RemoveActiveDeal(long chatId)
        {
            try
            {
                var activeDeals = LoadActiveDeals();
                if (activeDeals.ContainsKey(chatId))
                {
                    activeDeals.Remove(chatId);
                    SaveActiveDeals(activeDeals);
                    LoggingService.LogTransactionInfo(chatId, "Active deal removed");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error removing active deal", ex);
            }
        }
    }
}