// Models/Deal.cs
using System;

namespace CryptoWalletBot.Models
{
    public class Deal
    {
        public long ChatId { get; set; }
        public string Currency { get; set; }
        public string BankName { get; set; }
        public string SellerName { get; set; }
        public double Amount { get; set; }
        public string CardDetails { get; set; }
        public DateTime CreatedAt { get; set; }
        public string DealType { get; set; } // "buy" or "sell"
        public string Status { get; set; }
        public double Rate { get; set; }
        public double TotalAmount { get; set; }
        
    }
}