using Microsoft.Extensions.Logging;
using System;

namespace CryptoWalletBot.Services
{
    public static class LoggingService
    {
        private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<Program>();

        private static readonly DateTime BaseDate = new DateTime(2025, 2, 15);

        public static void LogCurrentInfo()
        {
            var currentTime = BaseDate.AddMinutes(DateTime.Now.Minute).AddSeconds(DateTime.Now.Second);
            Logger.LogInformation($"Current Date and Time (UTC - YYYY-MM-DD HH:MM:SS formatted): {currentTime:yyyy-MM-dd HH:mm:ss}");
            Logger.LogInformation($"Current User's Login: NikoBabby");
        }

        public static void LogTransactionInfo(long chatId, string operation)
        {
            LogCurrentInfo();
            Logger.LogInformation($"Chat ID {chatId} performing {operation}");
        }

        public static void LogError(string message, Exception ex = null)
        {
            LogCurrentInfo();
            if (ex != null)
                Logger.LogError(ex, message);
            else
                Logger.LogError(message);
        }

        public static void LogBotAction(string action)
        {
            LogCurrentInfo();
            Logger.LogInformation($"Bot action: {action}");
        }

        public static void LogDealAction(long chatId, string dealType, string currency, double amount)
        {
            LogCurrentInfo();
            Logger.LogInformation($"Deal action - Chat ID: {chatId}, Type: {dealType}, Currency: {currency}, Amount: {amount}");
        }

        public static void LogUserState(long chatId, string state)
        {
            LogCurrentInfo();
            Logger.LogInformation($"User state - Chat ID: {chatId}, State: {state}");
        }
    }
}