// Validators/CardDetailsValidator.cs
using System.Text.RegularExpressions;

namespace CryptoWalletBot.Validators
{
    public static class CardDetailsValidator
    {
        private static readonly Regex CardDetailsRegex = new Regex(
            @"^[А-ЯЁ][а-яё]+\s[А-ЯЁ][а-яё]+\s[А-ЯЁ][а-яё]+\s\d{16}$",
            RegexOptions.Compiled);

        public static (bool isValid, string errorMessage) ValidateCardDetails(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return (false, "Данные не могут быть пустыми. Пожалуйста, введите ФИО и номер карты.");

            if (!CardDetailsRegex.IsMatch(input))
                return (false, "Неверный формат. Пожалуйста, введите данные в формате:\n" +
                             "Фамилия Имя Отчество XXXXXXXXXXXXXXXX\n\n" +
                             "Пример: Иванов Иван Иванович 4149123412341234");

            return (true, null);
        }
    }
}