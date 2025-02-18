using Telegram.Bot.Types.ReplyMarkups;
using System.Collections.Generic;

namespace CryptoWalletBot.Services
{
    public static class KeyboardService
    {
        private const string SUPPORT_LINK = "https://t.me/NikoBabby";
        public static InlineKeyboardMarkup GetMainKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
        new[] { InlineKeyboardButton.WithCallbackData("💼 Кошелек", "show_wallet") },
        new[] { InlineKeyboardButton.WithCallbackData("💱 P2P Обмен", "show_p2p") },
        new[] { InlineKeyboardButton.WithCallbackData("📋 Активные сделки", "show_active_deals") },
        new[] { InlineKeyboardButton.WithUrl("📞 Поддержка", SUPPORT_LINK) }
    });
        }
        public static InlineKeyboardMarkup GetSellerKeyboard(string currency, string bankName, string operation)
        {
            var buttons = new List<InlineKeyboardButton[]>
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData(
                "Hard (431 сделка, 100%)",
                $"{operation}_seller_Hard_{currency}_{bankName}"
            )
        },
        new[]
        {
            InlineKeyboardButton.WithCallbackData(
                "AdamSmith (294 сделки, 100%)",
                $"{operation}_seller_AdamSmith_{currency}_{bankName}"
            )
        },
        new[]
        {
            InlineKeyboardButton.WithCallbackData(
                "JohnDoe (150 сделок, 95%)",
                $"{operation}_seller_JohnDoe_{currency}_{bankName}"
            )
        },
        new[]
        {
            InlineKeyboardButton.WithCallbackData(
                "◀️ Назад",
                $"{operation}_bank_{bankName}_{currency}"
            )
        }
    };

            return new InlineKeyboardMarkup(buttons);
        }

        public static InlineKeyboardMarkup GetWalletKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
            new[] { InlineKeyboardButton.WithCallbackData("🔄 Обновить баланс", "refresh_balance") },
            new[] { InlineKeyboardButton.WithCallbackData("💰 Пополнить", "wallet_receive") },
            new[] { InlineKeyboardButton.WithCallbackData("📤 Отправить", "wallet_send") },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ В главное меню", "back_to_main") }
        });
        }

        public static InlineKeyboardMarkup GetP2PKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Купить", "show_buy"),
                    InlineKeyboardButton.WithCallbackData("Продать", "show_sell")
                },
                new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "back_to_main") }
            });
        }
        public static InlineKeyboardMarkup GetAddressActionsKeyboard(string currency)
        {
            return new InlineKeyboardMarkup(new[]
            {
            new[] { InlineKeyboardButton.WithCallbackData("🔄 Обновить баланс", "refresh_balance") },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад к выбору валюты", $"wallet_receive") } // Изменено
        });
        }
        public static InlineKeyboardMarkup GetSendActionsKeyboard(string currency)
        {
            return new InlineKeyboardMarkup(new[]
            {
            new[] { InlineKeyboardButton.WithCallbackData("🔄 Обновить баланс", "refresh_balance") },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад к выбору валюты", $"wallet_send") } // Изменено
        });
        }

        public static InlineKeyboardMarkup GetCurrencyKeyboard(string operation)
        {
            return new InlineKeyboardMarkup(new[]
            {
        new []
        {
            InlineKeyboardButton.WithCallbackData(
                "TRX",
                $"{operation}_currency_trx"
            ),
        },
        new []
        {
            InlineKeyboardButton.WithCallbackData(
                "BTC",
                $"{operation}_currency_btc"
            ),
        },
        new []
        {
            InlineKeyboardButton.WithCallbackData(
                "ETH",
                $"{operation}_currency_eth"
            ),
        },
        new []
        {
            InlineKeyboardButton.WithCallbackData("◀️ Назад", "show_p2p"),
        }
    });
        }

        public static InlineKeyboardMarkup GetBankKeyboard(string currency, string operation)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Monobank", $"{operation}_bank_monobank_{currency}") },
                new[] { InlineKeyboardButton.WithCallbackData("ПриватБанк", $"{operation}_bank_privatbank_{currency}") },
                new[] { InlineKeyboardButton.WithCallbackData("ОщадБанк", $"{operation}_bank_oschadbank_{currency}") },
                new[] { InlineKeyboardButton.WithCallbackData("A-банк", $"{operation}_bank_abank_{currency}") },
                new[] { InlineKeyboardButton.WithCallbackData("ПУМБ", $"{operation}_bank_pumb_{currency}") },
                new[] { InlineKeyboardButton.WithCallbackData("Sense Bank", $"{operation}_bank_sense_{currency}") },
                new[] { InlineKeyboardButton.WithCallbackData("Райффайзен банк", $"{operation}_bank_raiffeisen_{currency}") },
                new[] { InlineKeyboardButton.WithCallbackData("Власний Рахунок", $"{operation}_bank_vlasniy_{currency}") },
                new[] { InlineKeyboardButton.WithCallbackData("Банковский перевод", $"{operation}_bank_transfer_{currency}") },
                new[] { InlineKeyboardButton.WithCallbackData("Sportbank", $"{operation}_bank_sport_{currency}") },
                new[] { InlineKeyboardButton.WithCallbackData("Укрсиббанк", $"{operation}_bank_ukrsib_{currency}") },
                new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", operation == "buy" ? "show_buy" : "show_sell") }
            });
        }

        public static InlineKeyboardMarkup GetDealKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Написать продавцу", "contact_seller") },
                new[] { InlineKeyboardButton.WithCallbackData("Написать в поддержку", "contact_support") },
                new[] { InlineKeyboardButton.WithCallbackData("✅ Завершить сделку", "complete_deal") },
                new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", "back_to_main") }
            });
        }

        public static InlineKeyboardMarkup GetAmountTypeKeyboard(string operation, string sellerName, string currency, string bankName)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Указать в UAH", $"{operation}_amount_uah_{sellerName}_{currency}_{bankName}") },
                new[] { InlineKeyboardButton.WithCallbackData("Максимум", $"{operation}_amount_max_{sellerName}_{currency}_{bankName}") },
                new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад", $"{operation}_seller_{sellerName}_{currency}_{bankName}") }
            });
        }

        public static InlineKeyboardMarkup GetCryptoSelectionKeyboard(string action)
        {
            return new InlineKeyboardMarkup(new[]
            {
            new[] { InlineKeyboardButton.WithCallbackData("TRX", $"{action}_TRX") },
            new[] { InlineKeyboardButton.WithCallbackData("BTC", $"{action}_BTC") },
            new[] { InlineKeyboardButton.WithCallbackData("ETH", $"{action}_ETH") },
            new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад к кошельку", "show_wallet") } // Изменено
        });
        }
    }
}