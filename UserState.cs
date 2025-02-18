// Models/UserState.cs
using System;
using Newtonsoft.Json;

namespace CryptoWalletBot.Models
{
    public class UserState
    {
        public string Currency { get; set; }
        public string BankName { get; set; }
        public string SellerName { get; set; }
        public double Amount { get; set; }
        public string CardDetails { get; set; }
        public string AmountType { get; set; }
        public bool IsWaitingForAmount { get; set; }
        public string DealStatus { get; set; } = "В процессе";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public double CurrentRate { get; set; }
        public double Price { get; set; }
    }

    public class BuyUserState : UserState
    {
        public BuyUserState()
        {
            IsWaitingForAmount = true;
        }
    }

    public class SellUserState : UserState
    {
        public SellUserState()
        {
            IsWaitingForAmount = true;
        }
    }
}