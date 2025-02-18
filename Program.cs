//using System;
//using System.IO;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Collections.Generic;
//using Telegram.Bot;
//using Telegram.Bot.Types;
//using Telegram.Bot.Types.Enums;
//using Telegram.Bot.Types.ReplyMarkups;
//using Telegram.Bot.Exceptions;
//using Microsoft.Extensions.Logging;
//using System.Data.SQLite;
//using Newtonsoft.Json;
//using System.Text;
//using Telegram.Bot.Polling;
//using System.Net.Http;
//using static CryptoWalletBot.Program;
//using System.Text.RegularExpressions;
//using Newtonsoft.Json.Linq;
//using Cryptobot2._0_v.Services;


//namespace CryptoWalletBot
//{
//    class Program
//    {
//        private static readonly string BotToken = "7226180923:AAEhNbaDSvR1t15HJFmwnL7DcPGIQ6_ZDFA";
//        private static readonly ITelegramBotClient BotClient = new TelegramBotClient(BotToken);
//        private static readonly SQLiteConnection Connection = new SQLiteConnection("Data Source=cryptobot.db;Version=3;");
//        private static readonly ILogger<Program> Logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Program>();
//        private static readonly TatumService _tatumService;
//        private static WalletMonitor _walletMonitor;
//        private static Dictionary<long, UserState> _userStates = new Dictionary<long, UserState>();
//        private static List<Deal> _deals = new List<Deal>();
//        static Program()
//        {
//            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
//            Logger = loggerFactory.CreateLogger<Program>();
//            _tatumService = new TatumService(loggerFactory.CreateLogger<TatumService>());
//        }
//        public static class LoggingHelper
//        {
//            private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole())
//                                                                .CreateLogger<Program>();

//            public static void LogCurrentInfo()
//            {
//                // Используем фиксированную дату для тестирования
//                var baseDate = new DateTime(2025, 2, 15);
//                var currentTime = baseDate.AddMinutes(DateTime.Now.Minute).AddSeconds(DateTime.Now.Second);

//                Logger.LogInformation($"Current Date and Time (UTC - YYYY-MM-DD HH:MM:SS formatted): {currentTime:yyyy-MM-dd HH:mm:ss}");
//                Logger.LogInformation($"Current User's Login: NikoBabby");
//            }

//            public static void LogTransactionInfo(long chatId, string operation)
//            {
//                Logger.LogInformation($"Chat ID {chatId} performing {operation}");
//            }

//            public static void LogError(string message, Exception ex = null)
//            {
//                if (ex != null)
//                    Logger.LogError(ex, message);
//                else
//                    Logger.LogError(message);
//            }
//        }

//        static async Task Main(string[] args)
//        {
//            Connection.Open();
//            CreateTables();

//            _walletMonitor = new WalletMonitor(
//                Logger,
//                _tatumService,
//                BotClient,
//                Connection);

//            var cts = new CancellationTokenSource();

//            var receiverOptions = new ReceiverOptions
//            {
//                AllowedUpdates = Array.Empty<UpdateType>()
//            };

//            BotClient.StartReceiving(
//                HandleUpdateAsync,
//                HandlePollingErrorAsync,
//                receiverOptions,
//                cts.Token
//            );

//            var me = await BotClient.GetMeAsync();
//            Logger.LogInformation($"Bot started successfully: @{me.Username}");
//            Logger.LogInformation($"Current Date and Time (UTC): 2025-02-14 14:05:39");
//            Logger.LogInformation($"Current User's Login: NikoBabby");

//            Console.WriteLine("Press any key to exit");
//            Console.ReadKey();

//            cts.Cancel();
//            Connection.Close();
//        }

//        private static void CreateTables()
//        {
//            string createUsersTable = @"
//                CREATE TABLE IF NOT EXISTS users (
//                    id INTEGER PRIMARY KEY AUTOINCREMENT,
//                    user_id INTEGER UNIQUE,
//                    username TEXT,
//                    wallet_addresses TEXT,
//                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
//                )";

//            using var command = new SQLiteCommand(createUsersTable, Connection);
//            command.ExecuteNonQuery();
//        }
//        private static bool HasActiveDeal(long chatId)
//        {
//            try
//            {
//                var activeDeals = LoadActiveDeals();
//                return activeDeals.ContainsKey(chatId);
//            }
//            catch (Exception ex)
//            {
//                Logger.LogError($"Error checking active deals: {ex.Message}");
//                return false;
//            }
//        }

//        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
//        {
//            try
//            {
//                switch (update.Type)
//                {
//                    case UpdateType.Message:
//                        await HandleMessageAsync(update.Message);
//                        break;
//                    case UpdateType.CallbackQuery:
//                        await HandleCallbackQueryAsync(update.CallbackQuery);
//                        break;
//                }
//            }
//            catch (Exception ex)
//            {
//                Logger.LogError(ex, "Error handling update");
//            }
//        }

//        private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
//        {
//            var errorMessage = exception switch
//            {
//                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
//                _ => exception.ToString()
//            };

//            Logger.LogError(errorMessage);
//            return Task.CompletedTask;
//        }

//        private static async Task HandleMessageAsync(Message message)
//        {
//            if (message?.Text == null) return;

//            var chatId = message.Chat.Id;
//            Logger.LogInformation($"Current Date and Time (UTC - YYYY-MM-DD HH:MM:SS formatted): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
//            Logger.LogInformation($"Current User's Login: NikoBabby");
//            Logger.LogInformation($"Received message: {message.Text} from chat {chatId}");

//            if (_userStates.TryGetValue(chatId, out var userState))
//            {
//                Logger.LogInformation($"Found user state for chat {chatId}. IsWaitingForAmount: {userState.IsWaitingForAmount}");

//                if (userState.IsWaitingForAmount)
//                {
//                    var seller = _sellers[userState.SellerName];
//                    double price = (double)seller["price"];
//                    double minAmount = (double)seller["min_amount"];
//                    double maxAmount = (double)seller["max_amount"];

//                    if (double.TryParse(message.Text.Replace(".", ","), out double inputAmount))
//                    {
//                        double amount;
//                        if (userState.AmountType == "uah")
//                        {
//                            // Если ввод в UAH, конвертируем в криптовалюту
//                            amount = inputAmount / price;
//                            if (amount < minAmount || amount > maxAmount)
//                            {
//                                await BotClient.SendTextMessageAsync(chatId,
//                                    $"Сумма должна быть в пределах от {minAmount * price:F2} до {maxAmount * price:F2} UAH. Попробуйте еще раз.");
//                                return;
//                            }
//                        }
//                        else
//                        {
//                            // Если ввод в криптовалюте
//                            amount = inputAmount;
//                            if (amount < minAmount || amount > maxAmount)
//                            {
//                                await BotClient.SendTextMessageAsync(chatId,
//                                    $"Сумма должна быть в пределах от {minAmount} до {maxAmount} {userState.Currency}. Попробуйте еще раз.");
//                                return;
//                            }
//                        }

//                        userState.Amount = amount;
//                        userState.IsWaitingForAmount = false;
//                        _userStates[chatId] = userState;

//                        double totalInUah = amount * price;

//                        string operationType = userState is BuyUserState ? "покупка" : "продажа";
//                        string amountMessage = userState is BuyUserState ? "К оплате" : "Вы получите";

//                        string confirmMessage = $"Подтвердите {operationType}:\n\n" +
//                                             $"Сумма: {amount:F8} {userState.Currency}\n" +
//                                             $"Курс: {price:F2} UAH\n" +
//                                             $"{amountMessage}: {totalInUah:F2} UAH\n\n" +
//                                             "Введите ваши данные в формате:\n" +
//                                             "Фамилия Имя Отчество XXXXXXXXXXXXXXXX\n\n" +
//                                             "Пример: Иванов Иван Иванович 4149123412341234";

//                        await BotClient.SendTextMessageAsync(chatId, confirmMessage);
//                        return;
//                    }
//                    else
//                    {
//                        string errorMessage;
//                        if (userState.AmountType == "uah")
//                        {
//                            errorMessage = $"Неверный формат суммы. Введите сумму в UAH (от {minAmount * price:F2} до {maxAmount * price:F2} UAH):";
//                        }
//                        else
//                        {
//                            errorMessage = $"Неверный формат суммы. Введите сумму в {userState.Currency} (от {minAmount} до {maxAmount} {userState.Currency}):";
//                        }
//                        await BotClient.SendTextMessageAsync(chatId, errorMessage);
//                        return;
//                    }
//                }
//                else
//                {
//                    // Обработка ввода данных карты
//                    var (isValid, errorMessage) = CardDetailsValidator.ValidateCardDetails(message.Text);
//                    if (!isValid)
//                    {
//                        await BotClient.SendTextMessageAsync(chatId, errorMessage);
//                        return;
//                    }

//                    userState.CardDetails = message.Text;
//                    var seller = _sellers[userState.SellerName];
//                    double price = (double)seller["price"];
//                    double totalAmount = userState.Amount * price;

//                    string operationType = userState is BuyUserState ? "Покупка" : "Продажа";
//                    string amountMessage = userState is BuyUserState ? "Итого к оплате" : "Вы получите";
//                    string counterparty = userState is BuyUserState ? "Продавец" : "Покупатель";

//                    string dealMessage = $"✅ Сделка создана!\n\n" +
//                                       $"Тип: {operationType}\n" +
//                                       $"Валюта: {userState.Currency}\n" +
//                                       $"Сумма: {userState.Amount:F8} {userState.Currency}\n" +
//                                       $"Курс: {price:F2} UAH\n" +
//                                       $"{amountMessage}: {totalAmount:F2} UAH\n" +
//                                       $"Банк: {userState.BankName}\n" +
//                                       $"{counterparty}: {userState.SellerName}\n\n" +
//                                       $"Ваши данные успешно сохранены!";

//                    // Логируем транзакцию
//                    TransactionLogger.LogTransaction(chatId, userState, message.Text);

//                    // Удаляем состояние пользователя после завершения сделки
//                    userState.DealStatus = "В процессе";
//                    userState.CreatedAt = DateTime.UtcNow;
//                    _userStates[chatId] = userState;

//                    await BotClient.SendTextMessageAsync(chatId, dealMessage, replyMarkup: GetDealKeyboard());
//                    return;
//                }
//            }
//            else
//            {
//                switch (message.Text)
//                {
//                    case "/start":
//                        await HandleStartCommand(chatId, message.Chat.Username ?? "Unknown");
//                        break;
//                    default:
//                        await BotClient.SendTextMessageAsync(chatId, "Используйте кнопки меню для навигации.");
//                        break;
//                }
//            }
//        }


//        private static async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
//        {
//            if (callbackQuery?.Message == null) return;

//            var chatId = callbackQuery.Message.Chat.Id;
//            var messageId = callbackQuery.Message.MessageId;

//            // Логируем информацию о времени и пользователе
//            Logger.LogInformation($"Current Date and Time (UTC - YYYY-MM-DD HH:MM:SS formatted): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
//            Logger.LogInformation($"Current User's Login: NikoBabby");
//            Logger.LogInformation($"Received callback: {callbackQuery.Data} from chat {chatId}");

//            string currency = null;
//            string bankName = null;
//            string sellerName = null;
//            string amountType = null;

//            try
//            {
//                switch (callbackQuery.Data)
//                {
//                    case "show_wallet":
//                        await DeletePreviousMessage(chatId, messageId);
//                        await ShowWallet(chatId);
//                        break;

//                    case "wallet_receive":
//                        await DeletePreviousMessage(chatId, messageId);
//                        await ShowReceiveOptions(chatId);
//                        break;

//                    case "wallet_send":
//                        if (HasActiveDeal(chatId))
//                        {
//                            await BotClient.SendTextMessageAsync(
//                                chatId,
//                                "⚠️ У вас есть активная сделка!\n" +
//                                "Отправка криптовалюты временно недоступна.\n" +
//                                "Дождитесь завершения текущей сделки.",
//                                replyMarkup: GetWalletKeyboard());
//                            return;
//                        }
//                        await DeletePreviousMessage(chatId, messageId);
//                        await ShowSendOptions(chatId);
//                        break;

//                    case "back_to_main":
//                        await DeletePreviousMessage(chatId, messageId);
//                        await ShowMainMenu(chatId);
//                        break;

//                    case "refresh_balance":
//                        await ShowWallet(chatId);
//                        await BotClient.AnswerCallbackQueryAsync(
//                            callbackQuery.Id,
//                            "Баланс обновлен",
//                            showAlert: false);
//                        break;

//                    case "show_p2p":
//                        await DeletePreviousMessage(chatId, messageId);
//                        await ShowP2PMenu(chatId);
//                        break;

//                    case "show_buy":
//                        await DeletePreviousMessage(chatId, messageId);
//                        await ShowBuyCurrencyMenu(chatId);
//                        break;

//                    case "show_sell":
//                        await DeletePreviousMessage(chatId, messageId);
//                        await ShowSellCurrencyMenu(chatId);
//                        break;
//                    case "show_active_deals":
//                        await DeletePreviousMessage(chatId, messageId);
//                        await ShowActiveDeals(chatId);
//                        break;

//                    case string s when s.StartsWith("buy_currency_"):
//                        currency = s.Split('_')[2];
//                        await DeletePreviousMessage(chatId, messageId);
//                        await ShowBuyBankMenu(chatId, currency);
//                        break;

//                    case string s when s.StartsWith("sell_currency_"):
//                        currency = s.Split('_')[2];
//                        await DeletePreviousMessage(chatId, messageId);
//                        await ShowSellBankMenu(chatId, currency);
//                        break;

//                    case string s when s.StartsWith("buy_bank_"):
//                        {
//                            var parts = s.Split('_');
//                            bankName = parts[2];
//                            currency = parts[3];
//                            await DeletePreviousMessage(chatId, messageId);
//                            await ShowBuySellerMenu(chatId, currency, bankName);
//                        }
//                        break;

//                    case string s when s.StartsWith("sell_bank_"):
//                        {
//                            var parts = s.Split('_');
//                            bankName = parts[2];
//                            currency = parts[3];
//                            await DeletePreviousMessage(chatId, messageId);
//                            await ShowSellSellerMenu(chatId, currency, bankName);
//                        }
//                        break;

//                    case string s when s.StartsWith("buy_seller_"):
//                        {
//                            var parts = s.Split('_');
//                            sellerName = parts[2];
//                            currency = parts[3];
//                            bankName = parts[4];
//                            await DeletePreviousMessage(chatId, messageId);
//                            await ShowBuyOrderDetails(chatId, currency, bankName, sellerName);
//                        }
//                        break;

//                    case string s when s.StartsWith("sell_seller_"):
//                        {
//                            var parts = s.Split('_');
//                            sellerName = parts[2];
//                            currency = parts[3];
//                            bankName = parts[4];
//                            await DeletePreviousMessage(chatId, messageId);
//                            await ShowSellOrderDetails(chatId, currency, bankName, sellerName);
//                        }
//                        break;

//                    case string s when s.StartsWith("buy_confirm_"):
//                        {
//                            var parts = s.Split('_');
//                            sellerName = parts[2];
//                            currency = parts[3];
//                            bankName = parts[4];
//                            await DeletePreviousMessage(chatId, messageId);

//                            var seller = _sellers[sellerName];
//                            double price = (double)seller["price"];
//                            double minAmount = (double)seller["min_amount"];
//                            double maxAmount = (double)seller["max_amount"];

//                            var buyState = new BuyUserState
//                            {
//                                Currency = currency,
//                                BankName = bankName,
//                                SellerName = sellerName,
//                                IsWaitingForAmount = true,
//                                AmountType = "crypto"
//                            };

//                            _userStates[chatId] = buyState;

//                            string message = $"Пришлите сумму {currency}, которую вы хотите купить.\n\n" +
//                                           $"Цена за 🪙 1 {currency}: 🪙 {price:F2} UAH\n\n" +
//                                           $"Минимум: 🪙 {minAmount:F2} {currency}\n" +
//                                           $"Максимум: 🪙 {maxAmount:F2} {currency}";

//                            var keyboard = new InlineKeyboardMarkup(new[]
//                            {
//                        new[] { InlineKeyboardButton.WithCallbackData("Указать в UAH", $"buy_amount_uah_{sellerName}_{currency}_{bankName}") },
//                        new[] { InlineKeyboardButton.WithCallbackData("Максимум", $"buy_amount_max_{sellerName}_{currency}_{bankName}") },
//                        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", $"buy_seller_{sellerName}_{currency}_{bankName}") }
//                    });

//                            await BotClient.SendTextMessageAsync(
//                                chatId,
//                                message,
//                                replyMarkup: keyboard);
//                        }
//                        break;

//                    case string s when s.StartsWith("sell_confirm_"):
//                        {
//                            var parts = s.Split('_');
//                            sellerName = parts[2];
//                            currency = parts[3];
//                            bankName = parts[4];
//                            await DeletePreviousMessage(chatId, messageId);

//                            var seller = _sellers[sellerName];
//                            double price = (double)seller["price"];
//                            double minAmount = (double)seller["min_amount"];
//                            double maxAmount = (double)seller["max_amount"];

//                            var sellState = new SellUserState
//                            {
//                                Currency = currency,
//                                BankName = bankName,
//                                SellerName = sellerName,
//                                IsWaitingForAmount = true,
//                                AmountType = "crypto"
//                            };

//                            _userStates[chatId] = sellState;

//                            await ShowSellAmountMenu(chatId, currency, bankName, sellerName);
//                        }
//                        break;

//                    case string s when s.StartsWith("buy_amount_"):
//                        {
//                            var parts = s.Split('_');
//                            amountType = parts[2];
//                            sellerName = parts[3];
//                            currency = parts[4];
//                            bankName = parts[5];
//                            await DeletePreviousMessage(chatId, messageId);
//                            _userStates[chatId] = new BuyUserState
//                            {
//                                Currency = currency,
//                                BankName = bankName,
//                                SellerName = sellerName,
//                                AmountType = amountType,
//                                IsWaitingForAmount = true
//                            };
//                            await BotClient.SendTextMessageAsync(chatId, $"Введите сумму в {(amountType == "uah" ? "UAH" : currency)}:");
//                        }
//                        break;

//                    case string s when s.StartsWith("sell_amount_"):
//                        {
//                            var parts = s.Split('_');
//                            amountType = parts[2];
//                            sellerName = parts[3];
//                            currency = parts[4];
//                            bankName = parts[5];
//                            await DeletePreviousMessage(chatId, messageId);
//                            _userStates[chatId] = new SellUserState
//                            {
//                                Currency = currency,
//                                BankName = bankName,
//                                SellerName = sellerName,
//                                AmountType = amountType,
//                                IsWaitingForAmount = true
//                            };
//                            await BotClient.SendTextMessageAsync(chatId, $"Введите сумму в {(amountType == "uah" ? "UAH" : currency)}:");
//                        }
//                        break;

//                    default:
//                        Logger.LogWarning($"Unknown callback data: {callbackQuery.Data}");
//                        break;
//                }

//                await BotClient.AnswerCallbackQueryAsync(callbackQuery.Id);
//            }
//            catch (Exception ex)
//            {
//                Logger.LogError(ex, $"Error handling callback {callbackQuery.Data}");
//                await BotClient.SendTextMessageAsync(chatId, "Произошла ошибка. Попробуйте позже.");
//            }
//        }
//        private static async Task DeletePreviousMessage(long chatId, int messageId)
//        {
//            try
//            {
//                await BotClient.DeleteMessageAsync(chatId, messageId);
//            }
//            catch (Exception ex)
//            {
//                Logger.LogError(ex, $"Error deleting message {messageId}");
//            }
//        }
//        private static Dictionary<string, Dictionary<string, object>> _sellers = new Dictionary<string, Dictionary<string, object>>
//{
//    { "AdamSmith", new Dictionary<string, object> { { "id", 1 }, { "deals", 294 }, { "rating", 100 }, { "volume", 11928 }, { "min_amount", 27.29 }, { "max_amount", 496.15 }, { "price", 47.64 } } },
//    { "JohnDoe", new Dictionary<string, object> { { "id", 2 }, { "deals", 150 }, { "rating", 95 }, { "volume", 8000 }, { "min_amount", 50.0 }, { "max_amount", 300.0 }, { "price", 47.50 } } }
//};

//        private static async Task ShowBuySellerMenu(long chatId, string currency, string bankName)
//        {
//            var keyboard = new List<InlineKeyboardButton[]>();

//            foreach (var seller in _sellers)
//            {
//                keyboard.Add(new[] { InlineKeyboardButton.WithCallbackData(seller.Key, $"buy_seller_{seller.Key}_{currency}_{bankName}") });
//            }

//            keyboard.Add(new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "show_buy") });

//            var inlineKeyboard = new InlineKeyboardMarkup(keyboard);

//            await BotClient.SendTextMessageAsync(
//                chatId,
//                "Выберите продавца:",
//                replyMarkup: inlineKeyboard);
//        }
//        private static async Task ShowSellBankMenu(long chatId, string currency)
//        {
//            var keyboard = new InlineKeyboardMarkup(new[]
//            {
//        new[] { InlineKeyboardButton.WithCallbackData("Monobank", $"sell_bank_monobank_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("ПриватБанк", $"sell_bank_privatbank_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("ОщадБанк", $"sell_bank_oschadbank_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("A-банк", $"sell_bank_abank_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("ПУМБ", $"sell_bank_pumb_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("Райффайзен Банк", $"sell_bank_raiffeisen_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("Sense Bank", $"sell_bank_sensebank_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("ОТП Банк", $"sell_bank_otp_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("Укрсиббанк", $"sell_bank_ukrsibbank_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("izibank", $"sell_bank_izibank_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "show_sell") }
//    });

//            await BotClient.SendTextMessageAsync(
//                chatId,
//                "Выберите банк:",
//                replyMarkup: keyboard);
//        }
//        private static async Task ShowSellSellerMenu(long chatId, string currency, string bankName)
//        {
//            var keyboard = new List<InlineKeyboardButton[]>();

//            foreach (var seller in _sellers)
//            {
//                keyboard.Add(new[] { InlineKeyboardButton.WithCallbackData(seller.Key, $"sell_seller_{seller.Key}_{currency}_{bankName}") });
//            }

//            keyboard.Add(new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "show_sell") });

//            var inlineKeyboard = new InlineKeyboardMarkup(keyboard);

//            await BotClient.SendTextMessageAsync(
//                chatId,
//                "Выберите продавца:",
//                replyMarkup: inlineKeyboard);
//        }
//        private static async Task ShowSellOrderDetails(long chatId, string currency, string bankName, string sellerName)
//        {
//            var seller = _sellers[sellerName];
//            int sellerId = (int)seller["id"];
//            int deals = (int)seller["deals"];
//            int rating = (int)seller["rating"];
//            int volume = (int)seller["volume"];
//            double minAmount = (double)seller["min_amount"];
//            double maxAmount = (double)seller["max_amount"];
//            double price = (double)seller["price"];

//            string message = $"📉 Объявление #{sellerId}\n" +
//                            $"Продавец: {sellerName}\n" +
//                            $"Сделок: {deals}\n" +
//                            $"Рейтинг: {rating}%\n" +
//                            $"Объем: {volume} {currency}\n" +
//                            $"Цена: {price} UAH/{currency}\n" +
//                            $"Минимум: {minAmount} {currency}\n" +
//                            $"Максимум: {maxAmount} {currency}";

//            var keyboard = new InlineKeyboardMarkup(new[]
//            {
//        new[] { InlineKeyboardButton.WithCallbackData("Продать валюту", $"sell_confirm_{sellerName}_{currency}_{bankName}") },
//        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "show_sell") }
//    });

//            await BotClient.SendTextMessageAsync(
//                chatId,
//                message,
//                replyMarkup: keyboard);
//        }
//        private static async Task ShowSellAmountMenu(long chatId, string currency, string bankName, string sellerName)
//        {
//            var seller = _sellers[sellerName];
//            double minAmount = (double)seller["min_amount"];
//            double maxAmount = (double)seller["max_amount"];
//            double price = (double)seller["price"];

//            string message = "Пришлите сумму USDT, которую вы хотите продать.\n\n" +
//                            $"Цена за 🪙 1 {currency}: 🪙 {price} UAH\n\n" +
//                            $"Минимум: 🪙 {minAmount} {currency}\n" +
//                            $"Максимум: 🪙 {maxAmount} {currency}";

//            var keyboard = new InlineKeyboardMarkup(new[]
//            {
//        new[] { InlineKeyboardButton.WithCallbackData("Указать в UAH", $"sell_amount_uah_{sellerName}_{currency}_{bankName}") },
//        new[] { InlineKeyboardButton.WithCallbackData("Максимум", $"sell_amount_max_{sellerName}_{currency}_{bankName}") },
//        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", $"sell_seller_{sellerName}_{currency}_{bankName}") }
//    });

//            await BotClient.SendTextMessageAsync(
//                chatId,
//                message,
//                replyMarkup: keyboard);
//        }
//        private static async Task HandleStartCommand(long chatId, string username)
//        {
//            try
//            {
//                using var command = new SQLiteCommand("SELECT wallet_addresses FROM users WHERE user_id = @userId", Connection);
//                command.Parameters.AddWithValue("@userId", chatId);

//                var existingWallets = command.ExecuteScalar()?.ToString();
//                if (string.IsNullOrEmpty(existingWallets))
//                {
//                    // Create new wallets
//                    var wallets = await CreateNewWallets();
//                    var walletsJson = JsonConvert.SerializeObject(wallets);

//                    using var insertCommand = new SQLiteCommand(
//                        "INSERT INTO users (user_id, username, wallet_addresses) VALUES (@userId, @username, @wallets)",
//                        Connection);
//                    insertCommand.Parameters.AddWithValue("@userId", chatId);
//                    insertCommand.Parameters.AddWithValue("@username", username);
//                    insertCommand.Parameters.AddWithValue("@wallets", walletsJson);
//                    insertCommand.ExecuteNonQuery();

//                    await BotClient.SendTextMessageAsync(
//                        chatId,
//                        "Добро пожаловать! Ваши криптокошельки успешно созданы.",
//                        replyMarkup: GetMainKeyboard());
//                }
//                else
//                {
//                    await BotClient.SendTextMessageAsync(
//                        chatId,
//                        "С возвращением!",
//                        replyMarkup: GetMainKeyboard());
//                }
//            }
//            catch (Exception ex)
//            {
//                Logger.LogError(ex, "Error in HandleStartCommand");
//                await BotClient.SendTextMessageAsync(
//                    chatId,
//                    "Произошла ошибка при создании кошельков. Попробуйте позже.");
//            }
//        }
//        private static async Task ShowBuyBankMenu(long chatId, string currency)
//        {
//            var keyboard = new InlineKeyboardMarkup(new[]
//            {
//        new[] { InlineKeyboardButton.WithCallbackData("Monobank", $"buy_bank_monobank_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("ПриватБанк", $"buy_bank_privatbank_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("ОщадБанк", $"buy_bank_oschadbank_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("A-банк", $"buy_bank_abank_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("ПУМБ", $"buy_bank_pumb_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("Райффайзен Банк", $"buy_bank_raiffeisen_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("Sense Bank", $"buy_bank_sensebank_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("ОТП Банк", $"buy_bank_otp_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("Укрсиббанк", $"buy_bank_ukrsibbank_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("izibank", $"buy_bank_izibank_{currency}") },
//        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "show_buy") }
//    });

//            await BotClient.SendTextMessageAsync(
//                chatId,
//                "Выберите банк:",
//                replyMarkup: keyboard);
//        }
//        private static async Task<Dictionary<string, string>> CreateNewWallets()
//        {
//            Logger.LogInformation("Starting wallet creation...");
//            var wallets = new Dictionary<string, string>();
//            var currencies = new[] { "ETH", "BTC", "USDT", "TRX" };

//            foreach (var currency in currencies)
//            {
//                try
//                {
//                    Logger.LogInformation($"Creating {currency} wallet...");
//                    var (success, addressJson, error) = await _tatumService.CreateWalletAsync(currency);

//                    Logger.LogInformation($"Create wallet result for {currency}: success={success}, addressJson={addressJson}, error={error}");

//                    if (success && !string.IsNullOrEmpty(addressJson))
//                    {
//                        try
//                        {
//                            var addressObj = JsonConvert.DeserializeObject<AddressResponse>(addressJson);
//                            if (addressObj?.Address != null)
//                            {
//                                wallets[currency] = addressJson;
//                                Logger.LogInformation($"Successfully added {currency} wallet. Address: {addressObj.Address}");
//                            }
//                            else
//                            {
//                                Logger.LogError($"Invalid address format for {currency}: {addressJson}");
//                            }
//                        }
//                        catch (Exception ex)
//                        {
//                            Logger.LogError(ex, $"Failed to parse address JSON for {currency}: {addressJson}");
//                        }
//                    }
//                    else
//                    {
//                        Logger.LogError($"Failed to create {currency} wallet: {error}");
//                    }
//                }
//                catch (Exception ex)
//                {
//                    Logger.LogError(ex, $"Exception while creating {currency} wallet");
//                }
//            }

//            if (wallets.Count == 0)
//            {
//                Logger.LogError("No wallets were created successfully");
//                throw new Exception("Failed to create any wallets");
//            }

//            Logger.LogInformation($"Final wallets dictionary: {JsonConvert.SerializeObject(wallets)}");
//            return wallets;
//        }
//        private static async Task ShowBuyOrderDetails(long chatId, string currency, string bankName, string sellerName)
//        {
//            var seller = _sellers[sellerName];
//            int sellerId = (int)seller["id"];
//            int deals = (int)seller["deals"];
//            int rating = (int)seller["rating"];
//            int volume = (int)seller["volume"];
//            double minAmount = (double)seller["min_amount"];
//            double maxAmount = (double)seller["max_amount"];
//            double price = (double)seller["price"];

//            string message = $"📉 Объявление #{sellerId}\n" +
//                             $"Продавец: {sellerName}\n" +
//                             $"Сделок: {deals}\n" +
//                             $"Рейтинг: {rating}%\n" +
//                             $"Объем: {volume} {currency}\n" +
//                             $"Цена: {price} UAH/{currency}\n" +
//                             $"Минимум: {minAmount} {currency}\n" +
//                             $"Максимум: {maxAmount} {currency}";

//            var keyboard = new InlineKeyboardMarkup(new[]
//            {
//        new[] { InlineKeyboardButton.WithCallbackData("Купить валюту", $"buy_confirm_{sellerName}_{currency}_{bankName}") },
//        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "show_buy") }
//    });

//            await BotClient.SendTextMessageAsync(
//                chatId,
//                message,
//                replyMarkup: keyboard);
//        }
//        private static async Task ShowWallet(long chatId)
//        {
//            try
//            {
//                using var command = new SQLiteCommand(
//                    "SELECT wallet_addresses FROM users WHERE user_id = @userId",
//                    Connection);
//                command.Parameters.AddWithValue("@userId", chatId);

//                var walletsJson = command.ExecuteScalar()?.ToString();
//                if (string.IsNullOrEmpty(walletsJson))
//                {
//                    await BotClient.SendTextMessageAsync(
//                        chatId,
//                        "Кошельки не найдены. Используйте /start для создания.");
//                    return;
//                }

//                var wallets = JsonConvert.DeserializeObject<Dictionary<string, string>>(walletsJson);
//                var messageBuilder = new StringBuilder();
//                messageBuilder.AppendLine("👛 *Кошелёк*");

//                if (HasActiveDeal(chatId))
//                {
//                    messageBuilder.AppendLine("\n⚠️ _У вас есть активная сделка\\. Отправка криптовалюты временно недоступна\\._\n");
//                }

//                foreach (var (currency, addressJson) in wallets)
//                {
//                    var addressObject = JsonConvert.DeserializeObject<AddressResponse>(addressJson);
//                    if (addressObject?.Address == null)
//                    {
//                        Logger.LogError($"Failed to parse address for {currency}: {addressJson}");
//                        continue;
//                    }

//                    var (success, balance, error) = await _tatumService.GetBalanceAsync(addressObject.Address, currency);

//                    string currencyName = currency.ToUpper() switch
//                    {
//                        "ETH" => "Ethereum",
//                        "BTC" => "Bitcoin",
//                        "USDT" => "Tether",
//                        "TRX" => "TRON",
//                        _ => currency
//                    };

//                    string currencyLink = currency.ToUpper() switch
//                    {
//                        "ETH" => "https://ethereum.org/",
//                        "BTC" => "https://bitcoin.org/",
//                        "USDT" => "https://tether.to/",
//                        "TRX" => "https://tron.network/",
//                        _ => ""
//                    };

//                    string currencyDisplay = string.IsNullOrEmpty(currencyLink)
//                        ? currencyName
//                        : $"[{currencyName}]({currencyLink})";

//                    string formattedBalance = success ? $"{balance:F8} {currency.ToUpper()}" : "Ошибка";
//                    messageBuilder.AppendLine($"🪙 {currencyDisplay}: {formattedBalance}");
//                    messageBuilder.AppendLine($"📋 Адрес: `{addressObject.Address}`");
//                    messageBuilder.AppendLine();
//                }

//                InlineKeyboardMarkup keyboard;
//                if (HasActiveDeal(chatId))
//                {
//                    keyboard = new InlineKeyboardMarkup(new[]
//                    {
//                new[] // Первый ряд
//                {
//                    InlineKeyboardButton.WithCallbackData("📥 Получить", "wallet_receive")
//                },
//                new[] // Второй ряд
//                {
//                    InlineKeyboardButton.WithCallbackData("🔄 Обновить баланс", "refresh_balance")
//                },
//                new[] // Третий ряд
//                {
//                    InlineKeyboardButton.WithCallbackData("◀️ Назад", "back_to_main")
//                }
//            });
//                }
//                else
//                {
//                    keyboard = new InlineKeyboardMarkup(new[]
//                    {
//                new[] // Первый ряд
//                {
//                    InlineKeyboardButton.WithCallbackData("📥 Получить", "wallet_receive"),
//                    InlineKeyboardButton.WithCallbackData("📤 Отправить", "wallet_send")
//                },
//                new[] // Второй ряд
//                {
//                    InlineKeyboardButton.WithCallbackData("🔄 Обновить баланс", "refresh_balance")
//                },
//                new[] // Третий ряд
//                {
//                    InlineKeyboardButton.WithCallbackData("◀️ Назад", "back_to_main")
//                }
//            });
//                }

//                await BotClient.SendTextMessageAsync(
//                    chatId,
//                    messageBuilder.ToString(),
//                    parseMode: ParseMode.MarkdownV2,
//                    replyMarkup: keyboard);
//            }
//            catch (Exception ex)
//            {
//                Logger.LogError(ex, "Error showing wallet");
//                await BotClient.SendTextMessageAsync(
//                    chatId,
//                    "Произошла ошибка при получении данных кошельков.");
//            }
//        }
//        private static async Task ShowBuyAmountMenu(long chatId, string currency, string bankName, string sellerName)
//        {
//            var seller = _sellers[sellerName];
//            double minAmount = (double)seller["min_amount"];
//            double maxAmount = (double)seller["max_amount"];
//            double price = (double)seller["price"];

//            string message = "Пришлите сумму USDT, которую вы хотите купить.\n\n" +
//                             $"Цена за 🪙 1 {currency}: 🪙 {price} UAH\n\n" +
//                             $"Минимум: 🪙 {minAmount} {currency}\n" +
//                             $"Максимум: 🪙 {maxAmount} {currency}";

//            var keyboard = new InlineKeyboardMarkup(new[]
//            {
//        new[] { InlineKeyboardButton.WithCallbackData("Указать в UAH", $"buy_amount_uah_{sellerName}_{currency}_{bankName}") },
//        new[] { InlineKeyboardButton.WithCallbackData("Максимум", $"buy_amount_max_{sellerName}_{currency}_{bankName}") },
//        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", $"buy_seller_{sellerName}_{currency}_{bankName}") }
//    });

//            await BotClient.SendTextMessageAsync(
//                chatId,
//                message,
//                replyMarkup: keyboard);
//        }
//        private static async Task ShowReceiveOptions(long chatId)
//        {
//            var keyboard = new InlineKeyboardMarkup(new[]
//            {
//                new[]
//                {
//                    InlineKeyboardButton.WithCallbackData("Bitcoin (BTC)", "receive_btc"),
//                    InlineKeyboardButton.WithCallbackData("Ethereum (ETH)", "receive_eth")
//                },
//                new[]
//                {
//                    InlineKeyboardButton.WithCallbackData("Tether (USDT)", "receive_usdt"),
//                    InlineKeyboardButton.WithCallbackData("TRON (TRX)", "receive_trx")
//                },
//                new[]
//                {
//                    InlineKeyboardButton.WithCallbackData("◀️ Назад", "show_wallet")
//                }
//            });

//            await BotClient.SendTextMessageAsync(
//                chatId,
//                "Выберите криптовалюту для получения:",
//                replyMarkup: keyboard);
//        }

//        private static async Task ShowSendOptions(long chatId)
//        {
//            await BotClient.SendTextMessageAsync(
//                chatId,
//                "Выберите криптовалюту для отправки:",
//                replyMarkup: GetCryptoSelectionKeyboard("send"));
//        }

//        private static async Task ShowMainMenu(long chatId)
//        {
//            await BotClient.SendTextMessageAsync(
//                chatId,
//                "Главное меню",
//                replyMarkup: GetMainKeyboard());
//        }
//        private static async Task ShowP2PMenu(long chatId)
//        {
//            if (HasActiveDeal(chatId))
//            {
//                await BotClient.SendTextMessageAsync(
//                    chatId,
//                    "⚠️ У вас есть активная сделка!\n" +
//                    "Для создания новой сделки необходимо завершить текущую.\n" +
//                    "Перейдите в раздел «Активные сделки» для просмотра деталей.",
//                    replyMarkup: new InlineKeyboardMarkup(new[]
//                    {
//                new[] { InlineKeyboardButton.WithCallbackData("📋 Активные сделки", "show_active_deals") },
//                new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "back_to_main") }
//                    }));
//                return;
//            }

//            await BotClient.SendTextMessageAsync(
//                chatId,
//                "Выберите действие:",
//                replyMarkup: GetP2PKeyboard());
//        }
//        private static InlineKeyboardMarkup GetP2PKeyboard()
//        {
//            return new InlineKeyboardMarkup(new[]
//            {
//                new[]
//                {
//                    InlineKeyboardButton.WithCallbackData("Купить", "show_buy"),
//                    InlineKeyboardButton.WithCallbackData("Продать", "show_sell")
//                },
//                new[]
//                {
//                    InlineKeyboardButton.WithCallbackData("◀️ Назад", "back_to_main")
//                }
//            });
//        }

//        private static async Task ShowSellCurrencyMenu(long chatId)
//        {
//            var keyboard = new InlineKeyboardMarkup(new[]
//            {
//        new[] { InlineKeyboardButton.WithCallbackData("USDT", "sell_currency_usdt") },
//        new[] { InlineKeyboardButton.WithCallbackData("BTC", "sell_currency_btc") },
//        new[] { InlineKeyboardButton.WithCallbackData("ETH", "sell_currency_eth") },
//        new[] { InlineKeyboardButton.WithCallbackData("TRX", "sell_currency_trx") },
//        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "show_p2p") }
//    });

//            await BotClient.SendTextMessageAsync(
//                chatId,
//                "Выберите криптовалюту для продажи:",
//                replyMarkup: keyboard);
//        }

//        private static InlineKeyboardMarkup GetDealKeyboard()
//        {
//            return new InlineKeyboardMarkup(new[]
//            {
//        new[] { InlineKeyboardButton.WithCallbackData("Написать продавцу", "contact_seller") },
//        new[] { InlineKeyboardButton.WithCallbackData("Написать в поддержку", "contact_support") },
//        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "show_p2p") }
//    });
//        }

//        private static InlineKeyboardMarkup GetMainKeyboard()
//        {
//            return new InlineKeyboardMarkup(new[]
//            {
//                new[] { InlineKeyboardButton.WithCallbackData("💼 Кошелек", "show_wallet") },
//                new[] { InlineKeyboardButton.WithCallbackData("🤝 P2P", "show_p2p") },
//                new[] { InlineKeyboardButton.WithCallbackData("📋 Активные сделки", "show_active_deals") }
//            });
//        }
//        private static async Task ShowActiveDeals(long chatId)
//        {
//            var activeDeals = LoadActiveDeals();
//            if (activeDeals.TryGetValue(chatId, out var userState) && !string.IsNullOrEmpty(userState.CardDetails))
//            {
//                var seller = _sellers[userState.SellerName];
//                double price = (double)seller["price"];
//                double totalAmount = userState.Amount * price;

//                string operationType = userState is BuyUserState ? "Покупка" : "Продажа";
//                string amountMessage = userState is BuyUserState ? "К оплате" : "Вы получите";
//                string counterparty = userState is BuyUserState ? "Продавец" : "Покупатель";

//                string dealMessage = $"✅ Активная сделка\n\n" +
//                                   $"Тип: {operationType}\n" +
//                                   $"Валюта: {userState.Currency}\n" +
//                                   $"Сумма: {userState.Amount:F8} {userState.Currency}\n" +
//                                   $"Курс: {price:F2} UAH\n" +
//                                   $"{amountMessage}: {totalAmount:F2} UAH\n" +
//                                   $"Банк: {userState.BankName}\n" +
//                                   $"{counterparty}: {userState.SellerName}\n\n" +
//                                   $"Статус: {userState.DealStatus}\n" +
//                                   $"Создана: {userState.CreatedAt:dd.MM.yyyy HH:mm:ss}";

//                var keyboard = new InlineKeyboardMarkup(new[]
//                {
//            new[] { InlineKeyboardButton.WithCallbackData("Написать продавцу", "contact_seller") },
//            new[] { InlineKeyboardButton.WithCallbackData("Написать в поддержку", "contact_support") },
//            new[] { InlineKeyboardButton.WithCallbackData("◀️ Главное меню", "back_to_main") }
//        });

//                await BotClient.SendTextMessageAsync(
//                    chatId,
//                    dealMessage,
//                    replyMarkup: keyboard);
//            }
//            else
//            {
//                await BotClient.SendTextMessageAsync(
//                    chatId,
//                    "У вас нет активных сделок.",
//                    replyMarkup: new InlineKeyboardMarkup(new[]
//                    {
//                new[] { InlineKeyboardButton.WithCallbackData("◀️ Главное меню", "back_to_main") }
//                    }));
//            }
//        }

//        private static InlineKeyboardMarkup GetWalletKeyboard()
//        {
//            return new InlineKeyboardMarkup(new[]
//            {
//                new[] {
//                    InlineKeyboardButton.WithCallbackData("📥 Получить", "wallet_receive"),
//                    InlineKeyboardButton.WithCallbackData("📤 Отправить", "wallet_send")
//                },
//                new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "back_to_main") }
//            });
//        }

//        private static InlineKeyboardMarkup GetCryptoSelectionKeyboard(string action)
//        {
//            return new InlineKeyboardMarkup(new[]
//            {
//                new[] {
//                    InlineKeyboardButton.WithCallbackData("Bitcoin (BTC)", $"{action}_btc"),
//                    InlineKeyboardButton.WithCallbackData("Ethereum (ETH)", $"{action}_eth")
//                },
//                new[] {
//                    InlineKeyboardButton.WithCallbackData("Tether (USDT)", $"{action}_usdt"),
//                    InlineKeyboardButton.WithCallbackData("TRON (TRX)", $"{action}_trx")
//                },
//                new[] {
//                    InlineKeyboardButton.WithCallbackData("◀️ Назад", "show_wallet")
//                }
//            });
//        }
//        private static async Task<bool> IsTronWalletInactive(string address)
//        {
//            try
//            {
//                var (success, balance, _) = await _tatumService.GetBalanceAsync(address, "TRX");
//                return success && balance == 0;
//            }
//            catch
//            {
//                return true;
//            }
//        }

//        private static async Task ShowCryptoAddress(long chatId, string currency)
//        {
//            try
//            {
//                using var command = new SQLiteCommand(
//                    "SELECT wallet_addresses FROM users WHERE user_id = @userId",
//                    Connection);
//                command.Parameters.AddWithValue("@userId", chatId);

//                var walletsJson = command.ExecuteScalar()?.ToString();
//                if (string.IsNullOrEmpty(walletsJson))
//                {
//                    await BotClient.SendTextMessageAsync(
//                        chatId,
//                        "Кошельки не найдены. Используйте /start для создания.");
//                    return;
//                }

//                var wallets = JsonConvert.DeserializeObject<Dictionary<string, string>>(walletsJson);
//                if (wallets.TryGetValue(currency, out var addressJson))
//                {
//                    var addressObject = JsonConvert.DeserializeObject<AddressResponse>(addressJson);
//                    if (addressObject?.Address != null)
//                    {
//                        string currencyName = currency.ToUpper() switch
//                        {
//                            "ETH" => "Ethereum",
//                            "BTC" => "Bitcoin",
//                            "USDT" => "Tether",
//                            "TRX" => "TRON",
//                            _ => currency
//                        };

//                        var messageBuilder = new StringBuilder();
//                        messageBuilder.AppendLine($"📋 *Ваш {currencyName} адрес для получения:*"); // Убираем \n
//                        messageBuilder.AppendLine($"`{addressObject.Address}`"); // Убираем \n

//                        if (currency == "TRX" && await IsTronWalletInactive(addressObject.Address))
//                        {
//                            messageBuilder.AppendLine("\nℹ️ *Информация об активации:*");
//                            messageBuilder.AppendLine("1\\. Для активации отправьте минимум 1\\-2 TRX");
//                            messageBuilder.AppendLine("2\\. После получения средств кошелек активируется");
//                            messageBuilder.AppendLine("3\\. Вы сможете отправлять TRX и токены TRC20");
//                        }

//                        var keyboard = new InlineKeyboardMarkup(new[]
//                        {
//                    new[]
//                    {
//                        InlineKeyboardButton.WithCallbackData("🔄 Обновить баланс", "refresh_balance"),
//                        InlineKeyboardButton.WithCallbackData("◀️ Назад", "wallet_receive")
//                    }
//                });

//                        await BotClient.SendTextMessageAsync(
//                            chatId,
//                            messageBuilder.ToString(),
//                            parseMode: ParseMode.MarkdownV2,
//                            replyMarkup: keyboard);
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                Logger.LogError(ex, $"Error showing {currency} address");
//                await BotClient.SendTextMessageAsync(
//                    chatId,
//                    "Произошла ошибка при получении адреса.");
//            }
//        }

//        private static async Task InitiateSendCrypto(long chatId, string currency)
//        {
//            try
//            {
//                using var command = new SQLiteCommand(
//                    "SELECT wallet_addresses FROM users WHERE user_id = @userId",
//                    Connection);
//                command.Parameters.AddWithValue("@userId", chatId);

//                var walletsJson = command.ExecuteScalar()?.ToString();
//                if (string.IsNullOrEmpty(walletsJson))
//                {
//                    await BotClient.SendTextMessageAsync(
//                        chatId,
//                        "Кошельки не найдены. Используйте /start для создания.");
//                    return;
//                }

//                var message = $"Для отправки {currency} введите:\\n" +
//                             $"1. Адрес получателя\\n" +
//                             $"2. Сумму для отправки";

//                await BotClient.SendTextMessageAsync(
//                    chatId,
//                    message,
//                    replyMarkup: GetBackToWalletKeyboard());
//            }
//            catch (Exception ex)
//            {
//                Logger.LogError(ex, $"Error initiating {currency} send");
//                await BotClient.SendTextMessageAsync(
//                    chatId,
//                    "Произошла ошибка при инициации отправки.");
//            }
//        }

//        private static InlineKeyboardMarkup GetBackToWalletKeyboard()
//        {
//            return new InlineKeyboardMarkup(new[]
//            {
//                new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад к кошельку", "show_wallet") }
//            });
//        }
//        private static async Task ShowBuyCurrencyMenu(long chatId)
//        {
//            var keyboard = new InlineKeyboardMarkup(new[]
//            {
//        new[] { InlineKeyboardButton.WithCallbackData("USDT", "buy_currency_usdt") },
//        new[] { InlineKeyboardButton.WithCallbackData("BTC", "buy_currency_btc") },
//        new[] { InlineKeyboardButton.WithCallbackData("ETH", "buy_currency_eth") },
//        new[] { InlineKeyboardButton.WithCallbackData("TRX", "buy_currency_trx") }, // Добавляем кнопку TRX
//        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "show_p2p") }
//    });

//            await BotClient.SendTextMessageAsync(
//                chatId,
//                "Выберите криптовалюту для покупки:",
//                replyMarkup: keyboard);
//        }
//        public class CardDetailsValidator
//        {
//            private static readonly Regex CardDetailsRegex = new Regex(
//                @"^[А-ЯЁ][а-яё]+\s[А-ЯЁ][а-яё]+\s[А-ЯЁ][а-яё]+\s\d{16}$",
//                RegexOptions.Compiled);

//            public static (bool isValid, string errorMessage) ValidateCardDetails(string input)
//            {
//                if (string.IsNullOrWhiteSpace(input))
//                    return (false, "Данные не могут быть пустыми. Пожалуйста, введите ФИО и номер карты.");

//                if (!CardDetailsRegex.IsMatch(input))
//                    return (false, "Неверный формат. Пожалуйста, введите данные в формате:\nФамилия Имя Отчество XXXXXXXXXXXXXXXX\n\nПример: Иванов Иван Иванович 4149123412341234");

//                return (true, null);
//            }
//        }
//        public class TransactionLogger
//        {
//            private static readonly string LogPath = "transactions.log";
//            private static readonly string ActiveDealsPath = "active_deals.json";

//            public static void LogTransaction(long chatId, UserState userState, string cardDetails)
//            {
//                var logEntry = new
//                {
//                    Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
//                    ChatId = chatId,
//                    UserLogin = "NikoBabby",
//                    TransactionType = userState is BuyUserState ? "Buy" : "Sell",
//                    Currency = userState.Currency,
//                    Amount = userState.Amount,
//                    Bank = userState.BankName,
//                    Seller = userState.SellerName,
//                    CardDetails = cardDetails,
//                    Status = userState.DealStatus,
//                    CreatedAt = userState.CreatedAt
//                };

//                var logMessage = JsonConvert.SerializeObject(logEntry, Formatting.Indented);

//                try
//                {
//                    System.IO.File.AppendAllText(LogPath, logMessage + Environment.NewLine);

//                    // Сохраняем активную сделку в отдельный файл
//                    var activeDeals = new Dictionary<long, object>();
//                    if (System.IO.File.Exists(ActiveDealsPath))
//                    {
//                        var existingDeals = System.IO.File.ReadAllText(ActiveDealsPath);
//                        activeDeals = JsonConvert.DeserializeObject<Dictionary<long, object>>(existingDeals);
//                    }

//                    activeDeals[chatId] = logEntry;
//                    System.IO.File.WriteAllText(ActiveDealsPath, JsonConvert.SerializeObject(activeDeals, Formatting.Indented));

//                    Logger.LogInformation($"Transaction logged successfully: {logMessage}");
//                }
//                catch (Exception ex)
//                {
//                    Logger.LogError($"Error logging transaction: {ex.Message}");
//                }
//            }
//        }
//        private static Dictionary<long, UserState> LoadActiveDeals()
//        {
//            try
//            {
//                string activeDealPath = "active_deals.json";
//                if (System.IO.File.Exists(activeDealPath))
//                {
//                    var json = System.IO.File.ReadAllText(activeDealPath);
//                    var deals = JsonConvert.DeserializeObject<Dictionary<long, JObject>>(json);
//                    var result = new Dictionary<long, UserState>();

//                    foreach (var deal in deals)
//                    {
//                        var state = deal.Value["TransactionType"].ToString() == "Buy"
//                            ? new BuyUserState()
//                            : (UserState)new SellUserState();

//                        state.Currency = deal.Value["Currency"].ToString();
//                        state.BankName = deal.Value["Bank"].ToString();
//                        state.SellerName = deal.Value["Seller"].ToString();
//                        state.Amount = deal.Value["Amount"].Value<double>();
//                        state.CardDetails = deal.Value["CardDetails"].ToString();
//                        state.DealStatus = deal.Value["Status"].ToString();
//                        state.CreatedAt = DateTime.Parse(deal.Value["CreatedAt"].ToString());

//                        result[deal.Key] = state;
//                    }
//                    return result;
//                }
//            }
//            catch (Exception ex)
//            {
//                Logger.LogError($"Error loading active deals: {ex.Message}");
//            }
//            return new Dictionary<long, UserState>();
//        }
//        public class Deal
//        {
//            public long ChatId { get; set; }
//            public string Currency { get; set; }
//            public string BankName { get; set; }
//            public string SellerName { get; set; }
//            public double Amount { get; set; }
//            public string CardDetails { get; set; }
//            public DateTime CreatedAt { get; set; }
//            public string DealType { get; set; } // "buy" or "sell"
//        }
//        private static Deal CreateDeal(long chatId, UserState userState)
//        {
//            return new Deal
//            {
//                ChatId = chatId,
//                Currency = userState.Currency,
//                BankName = userState.BankName,
//                SellerName = userState.SellerName,
//                Amount = userState.Amount,
//                CardDetails = userState.CardDetails,
//                CreatedAt = DateTime.UtcNow,
//                DealType = (userState is BuyUserState) ? "buy" : "sell"
//            };
//        }
//        public class BuyUserState : UserState
//        {
//            public BuyUserState()
//            {
//                IsWaitingForAmount = true; // Устанавливаем флаг ожидания суммы
//            }
//        }

//        public class SellUserState : UserState
//        {
//            public SellUserState()
//            {
//                IsWaitingForAmount = true; // Устанавливаем флаг ожидания суммы
//            }
//        }
//        public class UserState
//        {
//            public string Currency { get; set; }
//            public string BankName { get; set; }
//            public string SellerName { get; set; }
//            public double Amount { get; set; }
//            public string CardDetails { get; set; }
//            public string AmountType { get; set; }
//            public bool IsWaitingForAmount { get; set; }
//            public string DealStatus { get; set; } = "В процессе"; // Добавляем статус сделки
//            public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Добавляем время создания сделки
//        }
//        private class AddressResponse
//        {
//            [JsonProperty("address")]
//            public string Address { get; set; }
//        }
//    }
//}



using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Polling;
using System.Data.SQLite;
using CryptoWalletBot.Services;
using CryptoWalletBot.Models;
using CryptoWalletBot.Validators;
using Microsoft.Extensions.Logging;
using Cryptobot2._0_v.Services;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.ReplyMarkups;
using Newtonsoft.Json;

namespace CryptoWalletBot
{
    class Program
    {
        private static readonly string BotToken = "7226180923:AAEhNbaDSvR1t15HJFmwnL7DcPGIQ6_ZDFA";
        private static readonly ITelegramBotClient BotClient = new TelegramBotClient(BotToken);
        private static readonly SQLiteConnection Connection = new SQLiteConnection("Data Source=cryptobot.db;Version=3;");
        private static WalletService _walletService;
        private static DealService _dealService;
        private static TatumService _tatumService;
        private static WalletMonitor _walletMonitor;
        private static CryptoPriceService _cryptoPriceService;
        private static WalletRecoveryService _walletRecoveryService;
        private const string SUPPORT_LINK = "https://t.me/NikoBabby";
        private const string SELLER_LINK = "https://t.me/NikoBabby";
        static Program()
        {
            // Создаем единую фабрику логгеров
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            // Создаем логгеры для разных сервисов
            var tatumServiceLogger = loggerFactory.CreateLogger<TatumService>();
            var walletRecoveryServiceLogger = loggerFactory.CreateLogger<WalletRecoveryService>();

            // Инициализируем сервисы
            _tatumService = new TatumService(tatumServiceLogger);
            _walletService = new WalletService(Connection, _tatumService);
            _dealService = new DealService(BotClient);
            _cryptoPriceService = new CryptoPriceService();
            _walletRecoveryService = new WalletRecoveryService(
                _tatumService,
                walletRecoveryServiceLogger
            );
        }

        static async Task Main(string[] args)
        {
            
             
            try
            {
                Connection.Open();
                CreateTables();

                _walletMonitor = new WalletMonitor(
                    LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<WalletMonitor>(),
                    _tatumService,
                    BotClient,
                    Connection);
                _ = StartPriceUpdateTimer();
                var cts = new CancellationTokenSource();
                var receiverOptions = new ReceiverOptions
                {
                    AllowedUpdates = Array.Empty<UpdateType>()
                };

                BotClient.StartReceiving(
                    HandleUpdateAsync,
                    HandlePollingErrorAsync,
                    receiverOptions,
                    cts.Token
                );

                var me = await BotClient.GetMeAsync();
                LoggingService.LogCurrentInfo();
                LoggingService.LogTransactionInfo(0, $"Bot started successfully: @{me.Username}");

                Console.WriteLine("Press any key to exit");
                Console.ReadKey();

                cts.Cancel();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error in Main", ex);
            }
            finally
            {
                Connection.Close();
            }
        }
        private static async Task StartPriceUpdateTimer()
        {
            while (true)
            {
                try
                {
                    if (_cryptoPriceService != null && _dealService != null)
                    {
                        await _cryptoPriceService.UpdateDealServicePrices(_dealService);
                        LoggingService.LogCurrentInfo();
                        LoggingService.LogTransactionInfo(0, "Prices updated successfully");
                    }
                    else
                    {
                        LoggingService.LogError("CryptoPriceService or DealService is not initialized");
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogError("Error in price update timer", ex);
                }

                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }
        private static void CreateTables()
        {
            string createUsersTable = @"
                CREATE TABLE IF NOT EXISTS users (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id INTEGER UNIQUE,
                    username TEXT,
                    wallet_addresses TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                )";

            using var command = new SQLiteCommand(createUsersTable, Connection);
            command.ExecuteNonQuery();
        }

        // Сначала добавим класс AdminService
        public class AdminService
        {
            private readonly HashSet<long> _adminIds;

            public AdminService()
            {
                // Добавьте сюда ID администраторов
                _adminIds = new HashSet<long>
        {
            7586429057 // Замените на ваш Telegram ID
        };
            }

            public bool IsAdmin(long userId)
            {
                return _adminIds.Contains(userId);
            }
        }

        // Теперь обновим HandleUpdateAsync с правильными параметрами
        private static AdminService _adminService = new AdminService();

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                LoggingService.LogCurrentInfo();

                switch (update.Type)
                {
                    case UpdateType.Message:
                        var message = update.Message;
                        if (message?.Text != null)
                        {
                            var chatId = message.Chat.Id;

                            // Проверка на админские команды
                            if (_adminService.IsAdmin(chatId))
                            {
                                if (message.Text.StartsWith("/recover"))
                                {
                                    var parts = message.Text.Split(' ');
                                    if (parts.Length != 2)
                                    {
                                        await botClient.SendTextMessageAsync(
                                            chatId: chatId,
                                            text: "❌ Используйте формат: /recover username",
                                            cancellationToken: cancellationToken);
                                        return;
                                    }

                                    var username = parts[1];
                                    var result = await _walletRecoveryService.RecoverWalletAccess(chatId, username);

                                    // Исправленный вызов SendTextMessageAsync
                                    await botClient.SendTextMessageAsync(
                                        chatId: chatId,
                                        text: result,
                                        parseMode: ParseMode.Markdown,
                                        disableWebPagePreview: true,
                                        cancellationToken: cancellationToken);

                                    LoggingService.LogTransactionInfo(
                                        chatId,
                                        $"Admin {message.From.Username} requested wallet recovery for user {username}");

                                    return;
                                }

                                if (message.Text.StartsWith("/sendtrx"))
                                {
                                    var parts = message.Text.Split(' ');
                                    if (parts.Length != 4)
                                    {
                                        await botClient.SendTextMessageAsync(
                                            chatId: chatId,
                                            text: "❌ Используйте формат: /sendtrx fromAddress toAddress amount",
                                            cancellationToken: cancellationToken);
                                        return;
                                    }

                                    var fromAddress = parts[1];
                                    var toAddress = parts[2];
                                    if (decimal.TryParse(parts[3], out decimal amount))
                                    {
                                        var (success, txId, error) = await _tatumService.SendTRXAsync(
                                            fromAddress,
                                            toAddress,
                                            amount);

                                        var response = success
                                            ? $"✅ TRX успешно отправлены\nTxID: `{txId}`"
                                            : $"❌ Ошибка: {error}";

                                        // Исправленный вызов SendTextMessageAsync
                                        await botClient.SendTextMessageAsync(
                                            chatId: chatId,
                                            text: response,
                                            parseMode: ParseMode.Markdown,
                                            cancellationToken: cancellationToken);

                                        LoggingService.LogTransactionInfo(
                                            chatId,
                                            $"Admin {message.From.Username} sent {amount} TRX from {fromAddress} to {toAddress}. TxID: {txId}");
                                    }
                                    else
                                    {
                                        await botClient.SendTextMessageAsync(
                                            chatId: chatId,
                                            text: "❌ Неверный формат суммы",
                                            cancellationToken: cancellationToken);
                                    }
                                    return;
                                }
                            }

                            await HandleMessageAsync(message);
                        }
                        break;

                    case UpdateType.CallbackQuery:
                        await HandleCallbackQueryAsync(update.CallbackQuery);
                        break;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error handling update", ex);

                if (update?.Message?.Chat?.Id != null && _adminService.IsAdmin(update.Message.Chat.Id))
                {
                    try
                    {
                        // Исправленный вызов SendTextMessageAsync
                        await botClient.SendTextMessageAsync(
                            chatId: update.Message.Chat.Id,
                            text: $"❌ Произошла ошибка:\n`{ex.Message}`",
                            parseMode: ParseMode.Markdown,
                            cancellationToken: cancellationToken);
                    }
                    catch
                    {
                        // Игнорируем ошибки при отправке сообщения об ошибке
                    }
                }
            }
        }

        private static async Task HandleMessageAsync(Message message)
        {
            if (message?.Text == null) return;

            var chatId = message.Chat.Id;
            LoggingService.LogTransactionInfo(chatId, $"Received message: {message.Text}");

            var userState = _dealService.GetUserState(chatId);
            if (userState != null)
            {
                if (userState.IsWaitingForAmount)
                {
                    await HandleAmountInput(chatId, message.Text, userState);
                }
                else
                {
                    await HandleCardDetailsInput(chatId, message.Text, userState);
                }
                return;
            }

            switch (message.Text)
            {
                case "/start":
                    await HandleStartCommand(chatId, message.Chat.Username ?? "Unknown");
                    break;
                default:
                    await BotClient.SendTextMessageAsync(chatId, "Используйте кнопки меню для навигации.");
                    break;
            }
        }
        private static async Task ShowDealConfirmation(long chatId, UserState userState)
        {
            var seller = _dealService.GetSellerInfo(userState.SellerName);
            double price = (double)seller["price"];
            double totalInUah = userState.Amount * price;

            string operationType = userState is BuyUserState ? "покупка" : "продажа";
            string amountMessage = userState is BuyUserState ? "К оплате" : "Вы получите";

            string confirmMessage = $"Подтвердите {operationType}:\n\n" +
                                  $"Сумма: {userState.Amount:F8} {userState.Currency}\n" +
                                  $"Курс: {price:F2} UAH\n" +
                                  $"{amountMessage}: {totalInUah:F2} UAH\n\n" +
                                  "Введите ваши данные в формате:\n" +
                                  "Фамилия Имя Отчество XXXXXXXXXXXXXXXX\n\n" +
                                  "Пример: Иванов Иван Иванович 4149123412341234";

            await BotClient.SendTextMessageAsync(chatId, confirmMessage);
        }
        private static async Task ShowDealCreated(long chatId, Deal deal)
        {
            string operationType = deal.DealType == "buy" ? "Покупка" : "Продажа";
            string amountMessage = deal.DealType == "buy" ? "К оплате" : "Вы получите";
            string counterparty = deal.DealType == "buy" ? "Продавец" : "Покупатель";

            string dealMessage = $"✅ Сделка создана!\n\n" +
                               $"Тип: {operationType}\n" +
                               $"Валюта: {deal.Currency}\n" +
                               $"Сумма: {deal.Amount:F8} {deal.Currency}\n" +
                               $"Курс: {deal.Rate:F2} UAH\n" +
                               $"{amountMessage}: {deal.TotalAmount:F2} UAH\n" +
                               $"Банк: {deal.BankName}\n" +
                               $"{counterparty}: {deal.SellerName}\n\n" +
                               $"Ваши данные успешно сохранены!";

            await BotClient.SendTextMessageAsync(
                chatId,
                dealMessage,
                replyMarkup: KeyboardService.GetDealKeyboard());
        }
        private static async Task HandleDealCompletion(long chatId, int messageId)
        {
            await DeleteMessage(chatId, messageId);
            _dealService.RemoveActiveDeal(chatId);

            await BotClient.SendTextMessageAsync(
                chatId,
                "✅ Сделка успешно завершена!",
                replyMarkup: KeyboardService.GetMainKeyboard());
        }
        private static async Task ShowReceiveOptions(long chatId)
        {
            await BotClient.SendTextMessageAsync(
                chatId,
                "Выберите криптовалюту для получения:",
                replyMarkup: KeyboardService.GetCryptoSelectionKeyboard("receive"));
        }
        private static async Task ShowMainMenu(long chatId)
        {
            await BotClient.SendTextMessageAsync(
                chatId,
                "Главное меню",
                replyMarkup: KeyboardService.GetMainKeyboard());
        }
        private static async Task ShowCryptoAddress(long chatId, string currency)
        {
            var walletInfo = await _walletService.GetWalletInfo(chatId, currency);
            if (!walletInfo.success)
            {
                await BotClient.SendTextMessageAsync(
                    chatId,
                    "Ошибка получения адреса кошелька.",
                    replyMarkup: KeyboardService.GetCryptoSelectionKeyboard("receive")); // Добавляем возврат
                return;
            }

            var message = $"📋 Ваш {currency} адрес для получения:\n`{walletInfo.address}`";

            await BotClient.SendTextMessageAsync(
                chatId,
                message,
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardService.GetAddressActionsKeyboard(currency));
        }

        private static async Task InitiateSendCrypto(long chatId, string currency)
        {
            var walletInfo = await _walletService.GetWalletInfo(chatId, currency);
            if (!walletInfo.success)
            {
                await BotClient.SendTextMessageAsync(
                    chatId,
                    "Ошибка получения информации о кошельке.",
                    replyMarkup: KeyboardService.GetCryptoSelectionKeyboard("send")); // Добавляем возврат
                return;
            }

            await BotClient.SendTextMessageAsync(
                chatId,
                $"Для отправки {currency} введите адрес получателя и сумму в формате:\n" +
                "АДРЕС СУММА\n\n" +
                $"Доступно: {walletInfo.balance:F8} {currency}",
                replyMarkup: KeyboardService.GetSendActionsKeyboard(currency));
        }
        private static async Task ShowBuyCurrencyMenu(long chatId)
        {
            await BotClient.SendTextMessageAsync(
                chatId,
                "Выберите криптовалюту для покупки:",
                replyMarkup: KeyboardService.GetCurrencyKeyboard("buy"));
        }

        private static async Task ShowSellCurrencyMenu(long chatId)
        {
            await BotClient.SendTextMessageAsync(
                chatId,
                "Выберите криптовалюту для продажи:",
                replyMarkup: KeyboardService.GetCurrencyKeyboard("sell"));
        }
        private static async Task HandleStartCommand(long chatId, string username)
        {
            try
            {
                using var command = new SQLiteCommand(
                    "SELECT wallet_addresses FROM users WHERE user_id = @userId",
                    Connection);
                command.Parameters.AddWithValue("@userId", chatId);

                var existingWallets = command.ExecuteScalar()?.ToString();
                if (string.IsNullOrEmpty(existingWallets))
                {
                    var wallets = await _walletService.CreateNewWallets();
                    var walletsJson = JsonConvert.SerializeObject(wallets);

                    using var insertCommand = new SQLiteCommand(
                        "INSERT INTO users (user_id, username, wallet_addresses) VALUES (@userId, @username, @wallets)",
                        Connection);
                    insertCommand.Parameters.AddWithValue("@userId", chatId);
                    insertCommand.Parameters.AddWithValue("@username", username);
                    insertCommand.Parameters.AddWithValue("@wallets", walletsJson);
                    insertCommand.ExecuteNonQuery();

                    await BotClient.SendTextMessageAsync(
                        chatId,
                        "Добро пожаловать! Ваши криптокошельки успешно созданы.",
                        replyMarkup: KeyboardService.GetMainKeyboard());
                }
                else
                {
                    await BotClient.SendTextMessageAsync(
                        chatId,
                        "С возвращением!",
                        replyMarkup: KeyboardService.GetMainKeyboard());
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error in HandleStartCommand", ex);
                await BotClient.SendTextMessageAsync(
                    chatId,
                    "Произошла ошибка при создании кошельков. Попробуйте позже.");
            }
        }

        private static async Task ShowSendOptions(long chatId)
        {
            await BotClient.SendTextMessageAsync(
                chatId,
                "Выберите криптовалюту для отправки:",
                replyMarkup: KeyboardService.GetCryptoSelectionKeyboard("send"));
        }

        private static async Task HandleAmountInput(long chatId, string input, UserState userState)
        {
            try
            {
                double inputAmount = double.Parse(input.Replace(".", ","));

                var seller = _dealService.GetSellerInfo(userState.SellerName);
                double minAmount = (double)seller["min_amount"];
                double maxAmount = (double)seller["max_amount"];
                double price = (double)seller["price"];

                if (userState.AmountType == "uah")
                {
                    double cryptoAmount = inputAmount / price;
                    if (cryptoAmount < minAmount || cryptoAmount > maxAmount)
                    {
                        await BotClient.SendTextMessageAsync(chatId,
                            $"Сумма должна быть в пределах от {minAmount * price:F2} до {maxAmount * price:F2} UAH. Попробуйте еще раз.");
                        return;
                    }
                    userState.Amount = cryptoAmount;
                }
                else
                {
                    if (inputAmount < minAmount || inputAmount > maxAmount)
                    {
                        await BotClient.SendTextMessageAsync(chatId,
                            $"Сумма должна быть в пределах от {minAmount} до {maxAmount} {userState.Currency}. Попробуйте еще раз.");
                        return;
                    }
                    userState.Amount = inputAmount;
                }

                userState.IsWaitingForAmount = false;
                _dealService.SetUserState(chatId, userState);

                await ShowDealConfirmation(chatId, userState);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error handling amount input: {input}", ex);
                await BotClient.SendTextMessageAsync(chatId, "Неверный формат суммы. Попробуйте еще раз.");
            }
        }

        private static async Task HandleCardDetailsInput(long chatId, string input, UserState userState)
        {
            var (isValid, errorMessage) = CardDetailsValidator.ValidateCardDetails(input);
            if (!isValid)
            {
                await BotClient.SendTextMessageAsync(chatId, errorMessage);
                return;
            }

            userState.CardDetails = input;
            var deal = _dealService.CreateDeal(chatId, userState);
            TransactionLogger.LogTransaction(chatId, userState, input);

            await ShowDealCreated(chatId, deal);
            _dealService.RemoveUserState(chatId);
        }

        private static async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
        {
            if (callbackQuery?.Message == null) return;

            var chatId = callbackQuery.Message.Chat.Id;
            var messageId = callbackQuery.Message.MessageId;

            LoggingService.LogCurrentInfo();
            LoggingService.LogTransactionInfo(chatId, $"Received callback: {callbackQuery.Data}");

            try
            {
                switch (callbackQuery.Data)
                {
                    case "show_wallet":
                        await DeleteMessage(chatId, messageId);
                        await ShowWallet(chatId);
                        break;

                    case "wallet_receive":
                        await DeleteMessage(chatId, messageId);
                        await ShowReceiveOptions(chatId);
                        break;

                    case "create_missing_wallets":
                        await DeleteMessage(chatId, messageId);
                        try
                        {
                            var wallets = await _walletService.CreateNewWallets();
                            await BotClient.SendTextMessageAsync(
                                chatId,
                                "✅ Кошельки успешно созданы!",
                                replyMarkup: KeyboardService.GetMainKeyboard());
                            await ShowWallet(chatId);
                        }
                        catch (Exception ex)
                        {
                            LoggingService.LogError("Error creating wallets", ex);
                            await BotClient.SendTextMessageAsync(
                                chatId,
                                "❌ Произошла ошибка при создании кошельков. Попробуйте позже.");
                        }
                        break;

                    case "wallet_send":
                        if (await _dealService.HasActiveDeal(chatId))
                        {
                            await BotClient.SendTextMessageAsync(
                                chatId,
                                "⚠️ У вас есть активная сделка!\n" +
                                "Отправка криптовалюты временно недоступна.\n" +
                                "Дождитесь завершения текущей сделки.",
                                replyMarkup: KeyboardService.GetWalletKeyboard());
                            return;
                        }
                        await DeleteMessage(chatId, messageId);
                        await ShowSendOptions(chatId);
                        break;

                    case "back_to_main":
                        await DeleteMessage(chatId, messageId);
                        await ShowMainMenu(chatId);
                        break;

                    case "refresh_balance":
                        await ShowWallet(chatId);
                        await BotClient.AnswerCallbackQueryAsync(
                            callbackQuery.Id,
                            "Баланс обновлен",
                            showAlert: false);
                        break;

                    case "show_p2p":
                        await DeleteMessage(chatId, messageId);
                        await ShowP2PMenu(chatId);
                        break;

                    case "show_buy":
                        await DeleteMessage(chatId, messageId);
                        await ShowBuyCurrencyMenu(chatId);
                        break;

                    case "show_sell":
                        await DeleteMessage(chatId, messageId);
                        await ShowSellCurrencyMenu(chatId);
                        break;

                    case "show_active_deals":
                        await DeleteMessage(chatId, messageId);
                        await ShowActiveDeals(chatId);
                        break;

                    case "contact_seller":
                        await BotClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                        await BotClient.SendTextMessageAsync(
                            chatId,
                            "Нажмите на кнопку ниже, чтобы написать продавцу:",
                            replyMarkup: new InlineKeyboardMarkup(
                                InlineKeyboardButton.WithUrl("📝 Написать продавцу", SELLER_LINK)
                            )
                        );
                        break;

                    case "contact_support":
                        await BotClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                        await BotClient.SendTextMessageAsync(
                            chatId,
                            "Нажмите на кнопку ниже, чтобы написать в поддержку:",
                            replyMarkup: new InlineKeyboardMarkup(
                                InlineKeyboardButton.WithUrl("💬 Написать в поддержку", SUPPORT_LINK)
                            )
                        );
                        break;

                    case "complete_deal":
                        await HandleDealCompletion(chatId, messageId);
                        break;

                    default:
                        if (callbackQuery.Data.StartsWith("buy_bank_") || callbackQuery.Data.StartsWith("sell_bank_"))
                        {
                            var parts = callbackQuery.Data.Split('_');
                            var operation = parts[0]; // buy или sell
                            var bankName = parts[2];  // название банка
                            var currency = parts[3];  // валюта

                            await DeleteMessage(chatId, messageId);
                            await ShowSellerSelection(chatId, operation, currency, bankName);

                        }
                        else if (callbackQuery.Data.StartsWith("buy_seller_") || callbackQuery.Data.StartsWith("sell_seller_"))
                        {
                            LoggingService.LogTransactionInfo(chatId, $"Processing seller callback: {callbackQuery.Data}");
                            var parts = callbackQuery.Data.Split('_');
                            LoggingService.LogTransactionInfo(chatId, $"Split parts count: {parts.Length}");

                            if (parts.Length >= 5)
                            {
                                var operation = parts[0];     // buy или sell
                                var sellerName = parts[2];    // имя продавца
                                var currency = parts[3];      // валюта
                                var bankName = parts[4];      // банк

                                LoggingService.LogTransactionInfo(chatId,
                                    $"Parsed data: operation={operation}, seller={sellerName}, " +
                                    $"currency={currency}, bank={bankName}");

                                await DeleteMessage(chatId, messageId);

                                // Получаем информацию о продавце
                                var seller = _dealService.GetSellerInfo(sellerName);
                                if (seller != null)
                                {
                                    // Формируем текст объявления
                                    var adText = _dealService.FormatOrderDetails(seller, currency, sellerName, operation);

                                    // Создаем клавиатуру для выбора типа суммы
                                    var keyboard = new InlineKeyboardMarkup(new[]
                                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                "Указать в UAH",
                                $"{operation}_amount_uah_{sellerName}_{currency}_{bankName}"
                            )
                        },
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                $"Указать в {currency}",
                                $"{operation}_amount_crypto_{sellerName}_{currency}_{bankName}"
                            )
                        },
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                "◀️ Назад",
                                $"{operation}_bank_{bankName}_{currency}"
                            )
                        }
                    });

                                    await BotClient.SendTextMessageAsync(
                                        chatId,
                                        adText,
                                        replyMarkup: keyboard,
                                        parseMode: ParseMode.Html
                                    );

                                    LoggingService.LogTransactionInfo(chatId, "Ad text sent successfully");
                                }
                                else
                                {
                                    LoggingService.LogError($"Seller info not found for {sellerName}");
                                    await BotClient.SendTextMessageAsync(
                                        chatId,
                                        "Произошла ошибка при получении информации о продавце.",
                                        replyMarkup: KeyboardService.GetMainKeyboard()
                                    );
                                }
                            }
                            else
                            {
                                LoggingService.LogError($"Invalid callback data format: {callbackQuery.Data}");
                                await BotClient.SendTextMessageAsync(
                                    chatId,
                                    "Произошла ошибка при обработке запроса.",
                                    replyMarkup: KeyboardService.GetMainKeyboard()
                                );
                            }
                        }
                        else if (callbackQuery.Data.StartsWith("receive_"))
                        {
                            var currency = callbackQuery.Data.Split('_')[1].ToUpper();
                            await DeleteMessage(chatId, messageId);
                            await ShowCryptoAddress(chatId, currency);
                        }
                        else if (callbackQuery.Data.StartsWith("send_"))
                        {
                            var currency = callbackQuery.Data.Split('_')[1].ToUpper();
                            await DeleteMessage(chatId, messageId);
                            await InitiateSendCrypto(chatId, currency);
                        }
                        else
                        {
                            await HandleDynamicCallbacks(chatId, messageId, callbackQuery.Data);
                        }
                        break;
                }

                await BotClient.AnswerCallbackQueryAsync(callbackQuery.Id);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error handling callback {callbackQuery.Data}", ex);
                await BotClient.SendTextMessageAsync(chatId, "Произошла ошибка. Попробуйте позже.");
            }
        }


        private static async Task HandleDynamicCallbacks(long chatId, int messageId, string callbackData)
        {
            try
            {
                if (callbackData.StartsWith("buy_currency_") || callbackData.StartsWith("sell_currency_"))
                {
                    var parts = callbackData.Split('_');
                    string operation = parts[0]; // buy или sell
                    string currency = parts[2]; // trx

                    await DeleteMessage(chatId, messageId);
                    await ShowBankSelection(chatId, operation, currency);
                }
                
                else if (callbackData.StartsWith("buy_seller_") || callbackData.StartsWith("sell_seller_"))
                {
                    var parts = callbackData.Split('_');
                    string operation = parts[0]; // buy или sell
                    string sellerName = parts[2];
                    string currency = parts[3];
                    string bankName = parts[4];

                    await DeleteMessage(chatId, messageId);
                    await ShowOrderDetails(chatId, operation, currency, bankName, sellerName);
                }
                else if (callbackData.StartsWith("buy_confirm_") || callbackData.StartsWith("sell_confirm_"))
                {
                    var parts = callbackData.Split('_');
                    string operation = parts[0]; // buy или sell
                    string sellerName = parts[2];
                    string currency = parts[3];
                    string bankName = parts[4];

                    await DeleteMessage(chatId, messageId);
                    await InitiateDeal(chatId, operation, currency, bankName, sellerName);
                }
                else if (callbackData.StartsWith("buy_amount_") || callbackData.StartsWith("sell_amount_"))
                {
                    var parts = callbackData.Split('_');
                    string operation = parts[0]; // buy или sell
                    string amountType = parts[2]; // uah или max
                    string sellerName = parts[3];
                    string currency = parts[4];
                    string bankName = parts[5];

                    await DeleteMessage(chatId, messageId);
                    await HandleAmountType(chatId, operation, amountType, sellerName, currency, bankName);
                }
                else if (callbackData.StartsWith("receive_") || callbackData.StartsWith("send_"))
                {
                    var parts = callbackData.Split('_');
                    string operation = parts[0]; // receive или send
                    string currency = parts[1].ToUpper(); // TRX

                    await DeleteMessage(chatId, messageId);
                    if (operation == "receive")
                    {
                        await ShowCryptoAddress(chatId, currency);
                    }
                    else
                    {
                        await InitiateSendCrypto(chatId, currency);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error handling dynamic callback: {callbackData}", ex);
                await BotClient.SendTextMessageAsync(chatId, "Произошла ошибка. Попробуйте позже.");
            }
        }
        private static async Task ShowBankSelection(long chatId, string operation, string currency)
        {
            string message = $"Выберите банк для {(operation == "buy" ? "покупки" : "продажи")} {currency.ToUpper()}:";
            await BotClient.SendTextMessageAsync(
                chatId,
                message,
                replyMarkup: KeyboardService.GetBankKeyboard(currency, operation));
        }
        private static async Task ShowSellerSelection(long chatId, string operation, string currency, string bankName)
        {
            string message = $"Выберите {(operation == "buy" ? "продавца" : "покупателя")}:";
            var keyboard = await _dealService.GetSellerKeyboard(operation, currency, bankName);
            await BotClient.SendTextMessageAsync(
                chatId,
                message,
                replyMarkup: keyboard
            );
        }
        private static async Task ShowOrderDetails(long chatId, string operation, string currency, string bankName, string sellerName)
        {
            if (operation == "buy")
            {
                await _dealService.ShowBuyOrderDetails(chatId, currency, bankName, sellerName);
            }
            else
            {
                await _dealService.ShowSellOrderDetails(chatId, currency, bankName, sellerName);
            }
        }
        private static async Task InitiateDeal(long chatId, string operation, string currency, string bankName, string sellerName)
        {
            var userState = operation == "buy"
                ? new BuyUserState()
                : (UserState)new SellUserState();

            userState.Currency = currency;
            userState.BankName = bankName;
            userState.SellerName = sellerName;
            userState.IsWaitingForAmount = true;
            userState.AmountType = "crypto";

            _dealService.SetUserState(chatId, userState);

            var seller = _dealService.GetSellerInfo(sellerName);
            double price = (double)seller["price"];
            double minAmount = (double)seller["min_amount"];
            double maxAmount = (double)seller["max_amount"];

            string message = $"Пришлите сумму {currency}, которую вы хотите {(operation == "buy" ? "купить" : "продать")}.\n\n" +
                            $"Цена за 🪙 1 {currency}: 🪙 {price:F2} UAH\n\n" +
                            $"Минимум: 🪙 {minAmount:F2} {currency}\n" +
                            $"Максимум: 🪙 {maxAmount:F2} {currency}";

            var keyboard = new InlineKeyboardMarkup(new[]
            {
        new[] { InlineKeyboardButton.WithCallbackData("Указать в UAH", $"{operation}_amount_uah_{sellerName}_{currency}_{bankName}") },
        new[] { InlineKeyboardButton.WithCallbackData("Максимум", $"{operation}_amount_max_{sellerName}_{currency}_{bankName}") },
        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", $"{operation}_seller_{sellerName}_{currency}_{bankName}") }
    });

            await BotClient.SendTextMessageAsync(
                chatId,
                message,
                replyMarkup: keyboard);
        }
        private static async Task HandleAmountType(long chatId, string operation, string amountType, string sellerName, string currency, string bankName)
        {
            var seller = _dealService.GetSellerInfo(sellerName);
            if (amountType == "max")
            {
                var userState = operation == "buy"
                    ? new BuyUserState()
                    : (UserState)new SellUserState();

                userState.Currency = currency;
                userState.BankName = bankName;
                userState.SellerName = sellerName;
                userState.Amount = (double)seller["max_amount"];
                userState.IsWaitingForAmount = false;

                _dealService.SetUserState(chatId, userState);
                await ShowDealConfirmation(chatId, userState);
            }
            else // uah
            {
                var userState = operation == "buy"
                    ? new BuyUserState()
                    : (UserState)new SellUserState();

                userState.Currency = currency;
                userState.BankName = bankName;
                userState.SellerName = sellerName;
                userState.AmountType = "uah";
                userState.IsWaitingForAmount = true;

                _dealService.SetUserState(chatId, userState);

                double price = (double)seller["price"];
                double minAmount = (double)seller["min_amount"];
                double maxAmount = (double)seller["max_amount"];

                await BotClient.SendTextMessageAsync(
                    chatId,
                    $"Введите сумму в UAH (от {minAmount * price:F2} до {maxAmount * price:F2} UAH):");
            }
        }

        private static async Task DeleteMessage(long chatId, int messageId)
        {
            try
            {
                await BotClient.DeleteMessageAsync(chatId, messageId);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error deleting message {messageId}", ex);
            }
        }

        private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            LoggingService.LogError(errorMessage);
            return Task.CompletedTask;
        }

        // Вспомогательные методы для отображения различных меню и информации
        private static async Task ShowWallet(long chatId)
        {
            var message = "💼 Ваши криптокошельки:\n\n";
            var currencies = new[] { "TRX", "BTC", "ETH" };
            var hasWallets = false;
            var missingWallets = new List<string>();

            foreach (var currency in currencies)
            {
                try
                {
                    var walletInfo = await _walletService.GetWalletInfo(chatId, currency); // Передаем chatId
                    if (walletInfo.success)
                    {
                        hasWallets = true;
                        message += $"🪙 {currency}:\n" +
                                  $"Адрес: `{walletInfo.address}`\n" +
                                  $"Баланс: {walletInfo.balance:F8} {currency}\n\n";
                    }
                    else
                    {
                        missingWallets.Add(currency);
                        LoggingService.LogTransactionInfo(chatId, $"No {currency} wallet found");
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Error getting {currency} wallet info", ex);
                    missingWallets.Add(currency);
                }
            }

            if (!hasWallets)
            {
                message = "У вас пока нет криптокошельков. Хотите создать?";
                var keyboard = new InlineKeyboardMarkup(new[]
                {
            new[] { InlineKeyboardButton.WithCallbackData("✅ Создать кошельки", "create_wallets") },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "back_to_main") }
        });

                await BotClient.SendTextMessageAsync(
                    chatId,
                    message,
                    replyMarkup: keyboard);
                return;
            }

            if (missingWallets.Any())
            {
                message += "\n⚠️ Отсутствуют кошельки: " + string.Join(", ", missingWallets);
                var keyboard = new InlineKeyboardMarkup(new[]
                {
            new[] { InlineKeyboardButton.WithCallbackData("🔄 Обновить баланс", "refresh_balance") },
            new[] { InlineKeyboardButton.WithCallbackData("💰 Пополнить", "wallet_receive") },
            new[] { InlineKeyboardButton.WithCallbackData("📤 Отправить", "wallet_send") },
            new[] { InlineKeyboardButton.WithCallbackData("✅ Создать недостающие кошельки", "create_missing_wallets") },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "back_to_main") }
        });

                await BotClient.SendTextMessageAsync(
                    chatId,
                    message,
                    parseMode: ParseMode.Markdown,
                    replyMarkup: keyboard);
            }
            else
            {
                await BotClient.SendTextMessageAsync(
                    chatId,
                    message,
                    parseMode: ParseMode.Markdown,
                    replyMarkup: KeyboardService.GetWalletKeyboard());
            }
        }

        private static async Task ShowP2PMenu(long chatId)
        {
            if (await _dealService.HasActiveDeal(chatId))
            {
                await BotClient.SendTextMessageAsync(
                    chatId,
                    "⚠️ У вас есть активная сделка!\n" +
                    "Для создания новой сделки необходимо завершить текущую.",
                    replyMarkup: KeyboardService.GetMainKeyboard());
                return;
            }

            await BotClient.SendTextMessageAsync(
                chatId,
                "Выберите действие:",
                replyMarkup: KeyboardService.GetP2PKeyboard());
        }

        private static async Task ShowActiveDeals(long chatId)
        {
            var deal = _dealService.GetActiveDeal(chatId);
            if (deal == null)
            {
                await BotClient.SendTextMessageAsync(
                    chatId,
                    "У вас нет активных сделок.",
                    replyMarkup: KeyboardService.GetMainKeyboard());
                return;
            }

            await BotClient.SendTextMessageAsync(
                chatId,
                FormatDealMessage(deal),
                replyMarkup: KeyboardService.GetDealKeyboard());
        }


        private static string FormatDealMessage(Deal deal)
        {
            return $"📋 Сделка #{deal.ChatId}\n" +
                   $"Тип: {(deal.DealType == "buy" ? "Покупка" : "Продажа")}\n" +
                   $"Валюта: {deal.Currency}\n" +
                   $"Сумма: {deal.Amount:F8} {deal.Currency}\n" +
                   $"Курс: {deal.Rate:F2} UAH\n" +
                   $"Итого: {deal.TotalAmount:F2} UAH\n" +
                   $"Статус: {deal.Status}\n" +
                   $"Создана: {deal.CreatedAt:dd.MM.yyyy HH:mm:ss}";
        }
    }
}