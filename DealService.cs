using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using CryptoWalletBot.Models;
using Newtonsoft.Json;

namespace CryptoWalletBot.Services
{
    public class DealService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly Dictionary<string, Dictionary<string, object>> _sellers;
        private readonly Dictionary<long, UserState> _userStates;
        private readonly Dictionary<long, Deal> _activeDeals;

        public DealService(ITelegramBotClient botClient)
        {
            _botClient = botClient;
            _userStates = new Dictionary<long, UserState>();
            _activeDeals = new Dictionary<long, Deal>();

            // Информация о продавцах для разных валют
            _sellers = new Dictionary<string, Dictionary<string, object>>
            {
                {
                    "Hard", new Dictionary<string, object>
                    {
                        { "id", 3 },
                        { "deals", 431 },
                        { "rating", 100 },
                        { "volume", 23388 },
                        { "min_amount_trx", 33.4729d },
                        { "max_amount_trx", 476.946967d },
                        { "price_trx", 11.95d },
                        { "min_amount_btc", 0.001d },
                        { "max_amount_btc", 0.01d },
                        { "price_btc", 375000d },
                        { "min_amount_eth", 0.01d },
                        { "max_amount_eth", 0.1d },
                        { "price_eth", 150000d }
                    }
                },
                {
                    "AdamSmith", new Dictionary<string, object>
                    {
                        { "id", 1 },
                        { "deals", 294 },
                        { "rating", 100 },
                        { "volume", 11928 },
                        { "min_amount_trx", 27.29d },
                        { "max_amount_trx", 496.15d },
                        { "price_trx", 47.64d },
                        { "min_amount_btc", 0.002d },
                        { "max_amount_btc", 0.02d },
                        { "price_btc", 380000d },
                        { "min_amount_eth", 0.02d },
                        { "max_amount_eth", 0.2d },
                        { "price_eth", 155000d }
                    }
                },
                {
                    "JohnDoe", new Dictionary<string, object>
                    {
                        { "id", 2 },
                        { "deals", 150 },
                        { "rating", 95 },
                        { "volume", 8000 },
                        { "min_amount_trx", 50.0d },
                        { "max_amount_trx", 300.0d },
                        { "price_trx", 47.50d },
                        { "min_amount_btc", 0.003d },
                        { "max_amount_btc", 0.03d },
                        { "price_btc", 385000d },
                        { "min_amount_eth", 0.03d },
                        { "max_amount_eth", 0.3d },
                        { "price_eth", 160000d }
                    }
                }
            };
        }

        // Создание сделки
        public Deal CreateDeal(long chatId, UserState userState)
        {
            var seller = GetSellerInfo(userState.SellerName, userState.Currency);
            if (seller == null) return null;

            double price = Convert.ToDouble(seller[$"price_{userState.Currency.ToLower()}"]);

            var deal = new Deal
            {
                ChatId = chatId,
                Currency = userState.Currency,
                BankName = userState.BankName,
                SellerName = userState.SellerName,
                Amount = userState.Amount,
                CardDetails = userState.CardDetails,
                CreatedAt = DateTime.UtcNow,
                DealType = (userState is BuyUserState) ? "buy" : "sell",
                Rate = price,
                TotalAmount = userState.Amount * price,
                Status = "В процессе"
            };

            _activeDeals[chatId] = deal;
            return deal;
        }

        // Получение информации о продавце
        public Dictionary<string, object> GetSellerInfo(string sellerName, string currency)
        {
            LoggingService.LogTransactionInfo(0, $"Getting info for seller: {sellerName}, currency: {currency}");

            if (_sellers.TryGetValue(sellerName, out var sellerInfo))
            {
                LoggingService.LogTransactionInfo(0, $"Found seller info: {JsonConvert.SerializeObject(sellerInfo)}");
                return sellerInfo;
            }

            LoggingService.LogError($"Seller not found: {sellerName}");
            return null;
        }

        // Обновление цен для продавцов
        public async Task UpdatePrices(decimal trxBuyPrice, decimal trxSellPrice, decimal btcBuyPrice, decimal btcSellPrice, decimal ethBuyPrice, decimal ethSellPrice)
        {
            try
            {
                foreach (var seller in _sellers.Values)
                {
                    seller["price_trx"] = seller["type"].ToString() == "buy" ? Convert.ToDouble(trxBuyPrice) : Convert.ToDouble(trxSellPrice);
                    seller["price_btc"] = seller["type"].ToString() == "buy" ? Convert.ToDouble(btcBuyPrice) : Convert.ToDouble(btcSellPrice);
                    seller["price_eth"] = seller["type"].ToString() == "buy" ? Convert.ToDouble(ethBuyPrice) : Convert.ToDouble(ethSellPrice);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error updating prices in DealService", ex);
            }
        }

        // Отображение деталей сделки
        public string FormatOrderDetails(Dictionary<string, object> seller, string currency, string sellerName, string operation)
        {
            string adText = "";

            switch (sellerName)
            {
                case "Hard":
                    adText = $"💎 🌟 {sellerName} {(operation == "buy" ? "продаёт" : "покупает")} {currency} за UAH.\n" +
                             $"🏆 {seller["deals"]} сделок · {seller["rating"]}% · ${seller["volume"]}\n\n" +
                             $"Цена за 🪙 1 {currency}: 🪙 {seller[$"price_{currency.ToLower()}"]} UAH\n\n" +
                             $"Доступный объём: 🪙 {seller[$"max_amount_{currency.ToLower()}"]} {currency}\n" +
                             $"Лимиты: 🪙 {seller[$"min_amount_{currency.ToLower()}"]} ~ {seller[$"max_amount_{currency.ToLower()}"]} {currency}\n\n" +
                             "Способ оплаты: Monobank\n" +
                             "Срок оплаты: 15 мин\n\n" +
                             "Условия сделки:\n" +
                             "✅ Принимаю оплату только с карт: Mono, ПриватБанк, А-Банк, PUMB, Sense, БВР.\n\n" +
                             "❌ Не принимаю: платежи от третьих лиц, дропов, взломанных счетов, скам и другие незаконные схемы.\n\n" +
                             "⚠️ Могу запросить дополнительную верификацию или указание комментария к платежу.";
                    break;

                case "AdamSmith":
                    adText = $"🏛 Premium Trader {sellerName} {(operation == "buy" ? "продаёт" : "покупает")} {currency}\n" +
                             $"⭐️ {seller["deals"]} успешных сделок · Рейтинг {seller["rating"]}% · Оборот ${seller["volume"]}\n\n" +
                             $"💰 Курс: {seller[$"price_{currency.ToLower()}"]} UAH за 1 {currency}\n\n" +
                             $"📊 Доступно: {seller[$"max_amount_{currency.ToLower()}"]} {currency}\n" +
                             $"📋 Лимиты: от {seller[$"min_amount_{currency.ToLower()}"]} до {seller[$"max_amount_{currency.ToLower()}"]} {currency}\n\n" +
                             "Принимаем оплату:\n" +
                             "• ПриватБанк\n" +
                             "• Monobank\n" +
                             "• Pumb\n\n" +
                             "⚡️ Среднее время сделки: 10-15 минут\n\n" +
                             "Условия работы:\n" +
                             "✅ Работаю с верифицированными пользователями\n" +
                             "✅ Транзакции только с личных карт\n" +
                             "❌ Не работаю с юр. лицами\n" +
                             "⚠️ Возможна дополнительная верификация\n\n" +
                             "Поддержка 24/7 в чате";
                    break;

                case "JohnDoe":
                    adText = $"🔰 Trusted Exchanger {sellerName} {(operation == "buy" ? "продаёт" : "покупает")} {currency}\n" +
                             $"📊 Статистика: {seller["deals"]} trades · {seller["rating"]}% positive · ${seller["volume"]} volume\n\n" +
                             $"Exchange rate: {seller[$"price_{currency.ToLower()}"]} UAH per {currency}\n" +
                             $"Available: {seller[$"max_amount_{currency.ToLower()}"]} {currency}\n" +
                             $"Limits: {seller[$"min_amount_{currency.ToLower()}"]} - {seller[$"max_amount_{currency.ToLower()}"]} {currency}\n\n" +
                             "Payment methods:\n" +
                             "• All Ukrainian banks\n" +
                             "• Popular payment systems\n\n" +
                             "Processing time: 5-20 minutes\n\n" +
                             " TERMS:\n" +
                             "• Only personal accounts\n" +
                             "• Full verification required\n" +
                             "• No third-party payments\n\n" +
                             "SECURITY:\n" +
                             "• 2FA enabled\n" +
                             "• Escrow service\n" +
                             "• Insurance for large trades\n\n" +
                             "24/7 Support available";
                    break;

                default:
                    adText = $"📉 Объявление #{seller["id"]}\n" +
                             $"{(operation == "buy" ? "Продавец" : "Покупатель")}: {sellerName}\n" +
                             $"Сделок: {seller["deals"]}\n" +
                             $"Рейтинг: {seller["rating"]}%\n" +
                             $"Объем: {seller["volume"]} {currency}\n" +
                             $"Цена: {seller[$"price_{currency.ToLower()}"]} UAH/{currency}\n" +
                             $"Минимум: {seller[$"min_amount_{currency.ToLower()}"]} {currency}\n" +
                             $"Максимум: {seller[$"max_amount_{currency.ToLower()}"]} {currency}";
                    break;
            }

            return adText;
        }

        // Удаление активной сделки
        public void RemoveActiveDeal(long chatId)
        {
            _activeDeals.Remove(chatId);
        }

        // Получение активной сделки
        public Deal GetActiveDeal(long chatId)
        {
            return _activeDeals.GetValueOrDefault(chatId);
        }

        // Получение состояния пользователя
        public UserState GetUserState(long chatId)
        {
            return _userStates.GetValueOrDefault(chatId);
        }

        // Установка состояния пользователя
        public void SetUserState(long chatId, UserState state)
        {
            _userStates[chatId] = state;
        }

        // Удаление состояния пользователя
        public void RemoveUserState(long chatId)
        {
            _userStates.Remove(chatId);
        }

        // Проверка наличия активной сделки
        public bool HasActiveDeal(long chatId)
        {
            return _activeDeals.ContainsKey(chatId);
        }
    }
}