using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CryptoWalletBot.Services
{
    public class CryptoPriceService
    {
        private readonly HttpClient _httpClient;

        // Константы для API
        private const string CoinGeckoApi = "https://api.coingecko.com/api/v3";
        private const string BinancePublicApi = "https://api.binance.com/api/v3";
        private const string KucoinPublicApi = "https://api.kucoin.com/api/v1";

        public CryptoPriceService()
        {
            _httpClient = new HttpClient();
        }

        // Получение цены TRX/USD
        public async Task<decimal> GetTrxPriceAsync()
        {
            try
            {
                // Пробуем получить цену из CoinGecko
                var response = await _httpClient.GetStringAsync(
                    $"{CoinGeckoApi}/simple/price?ids=tron&vs_currencies=usd");
                var data = JObject.Parse(response);
                return decimal.Parse(data["tron"]["usd"].ToString());
            }
            catch
            {
                try
                {
                    // Если CoinGecko недоступен, пробуем Binance
                    var response = await _httpClient.GetStringAsync(
                        $"{BinancePublicApi}/ticker/price?symbol=TRXUSDT");
                    var data = JObject.Parse(response);
                    return decimal.Parse(data["price"].ToString());
                }
                catch
                {
                    try
                    {
                        // Если Binance недоступен, пробуем KuCoin
                        var response = await _httpClient.GetStringAsync(
                            $"{KucoinPublicApi}/market/orderbook/level1?symbol=TRX-USDT");
                        var data = JObject.Parse(response);
                        return decimal.Parse(data["data"]["price"].ToString());
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError("Failed to get TRX price from all sources", ex);
                        return 0;
                    }
                }
            }
        }

        // Получение курса USD/UAH через Monobank
        public async Task<decimal> GetUsdtUahRate()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(
                    "https://api.monobank.ua/bank/currency");
                var data = JArray.Parse(response);
                var usdRate = data.FirstOrDefault(x =>
                    x["currencyCodeA"].ToString() == "840" && // USD code
                    x["currencyCodeB"].ToString() == "980"    // UAH code
                );
                if (usdRate != null)
                {
                    return decimal.Parse(usdRate["rateBuy"].ToString());
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to get USD/UAH rate from Monobank", ex);
            }
            // Если не удалось получить курс, возвращаем примерное значение
            return 37.5m;
        }

        // Получение цены TRX/UAH
        public async Task<decimal> GetTrxUahPrice()
        {
            var trxUsdPrice = await GetTrxPriceAsync();
            var usdUahRate = await GetUsdtUahRate();
            return trxUsdPrice * usdUahRate;
        }

        // Получение цены BTC/USD
        public async Task<decimal> GetBtcPriceAsync()
        {
            try
            {
                // Пробуем получить цену из CoinGecko
                var response = await _httpClient.GetStringAsync(
                    $"{CoinGeckoApi}/simple/price?ids=bitcoin&vs_currencies=usd");
                var data = JObject.Parse(response);
                return decimal.Parse(data["bitcoin"]["usd"].ToString());
            }
            catch
            {
                try
                {
                    // Если CoinGecko недоступен, пробуем Binance
                    var response = await _httpClient.GetStringAsync(
                        $"{BinancePublicApi}/ticker/price?symbol=BTCUSDT");
                    var data = JObject.Parse(response);
                    return decimal.Parse(data["price"].ToString());
                }
                catch (Exception ex)
                {
                    LoggingService.LogError("Failed to get BTC price from all sources", ex);
                    return 0;
                }
            }
        }

        // Получение цены ETH/USD
        public async Task<decimal> GetEthPriceAsync()
        {
            try
            {
                // Пробуем получить цену из CoinGecko
                var response = await _httpClient.GetStringAsync(
                    $"{CoinGeckoApi}/simple/price?ids=ethereum&vs_currencies=usd");
                var data = JObject.Parse(response);
                return decimal.Parse(data["ethereum"]["usd"].ToString());
            }
            catch
            {
                try
                {
                    // Если CoinGecko недоступен, пробуем Binance
                    var response = await _httpClient.GetStringAsync(
                        $"{BinancePublicApi}/ticker/price?symbol=ETHUSDT");
                    var data = JObject.Parse(response);
                    return decimal.Parse(data["price"].ToString());
                }
                catch (Exception ex)
                {
                    LoggingService.LogError("Failed to get ETH price from all sources", ex);
                    return 0;
                }
            }
        }

        // Получение цены BTC/UAH
        public async Task<decimal> GetBtcUahPrice()
        {
            var btcUsdPrice = await GetBtcPriceAsync();
            var usdUahRate = await GetUsdtUahRate();
            return btcUsdPrice * usdUahRate;
        }

        // Получение цены ETH/UAH
        public async Task<decimal> GetEthUahPrice()
        {
            var ethUsdPrice = await GetEthPriceAsync();
            var usdUahRate = await GetUsdtUahRate();
            return ethUsdPrice * usdUahRate;
        }

        // Обновление цен для DealService
        public async Task UpdateDealServicePrices(DealService dealService)
        {
            try
            {
                // Получаем цены для всех валют
                var trxUahPrice = await GetTrxUahPrice();
                var btcUahPrice = await GetBtcUahPrice();
                var ethUahPrice = await GetEthUahPrice();

                // Добавляем маржу для покупки/продажи
                var trxSellPrice = trxUahPrice * 1.02m; // +2% для продажи
                var trxBuyPrice = trxUahPrice * 0.98m; // -2% для покупки

                var btcSellPrice = btcUahPrice * 1.02m;
                var btcBuyPrice = btcUahPrice * 0.98m;

                var ethSellPrice = ethUahPrice * 1.02m;
                var ethBuyPrice = ethUahPrice * 0.98m;

                // Обновляем цены в DealService
                await dealService.UpdatePrices(trxBuyPrice, trxSellPrice, btcBuyPrice, btcSellPrice, ethBuyPrice, ethSellPrice);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to update prices", ex);
            }
        }
    }
}