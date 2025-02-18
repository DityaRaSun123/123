using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Cryptobot2._0_v.Services
{
    public class WalletRecoveryService
    {
        private readonly TatumService _tatumService;
        private readonly ILogger _logger;
        private readonly SQLiteConnection _connection;
        private readonly HashSet<long> _adminIds;

        public WalletRecoveryService(TatumService tatumService, ILogger logger)
        {
            _tatumService = tatumService;
            _logger = logger;
            _adminIds = new HashSet<long> { 7586429057 }; // Замените на ваш ID
        }

        public async Task<string> RecoverWalletAccess(long adminId, string username)
        {
            if (!_adminIds.Contains(adminId))
            {
                return "❌ Доступ запрещен";
            }

            try
            {
                var result = await _tatumService.GetWalletPrivateKey(username);

                if (!result.success)
                {
                    return $"❌ Ошибка: {result.error}";
                }

                return $"🔐 Данные кошелька для {username}:\n\n" +
                       $"Private Key: `{result.privateKey}`\n\n" +
                       "⚠️ ВНИМАНИЕ! Это приватный ключ, никому его не передавайте!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recovering wallet for {username}");
                return "❌ Произошла ошибка при восстановлении доступа";
            }
        }
    }
}
