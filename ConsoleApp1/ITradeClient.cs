public interface ITradeClient
{
    Task BuySpotAsync(string instId, double quantity, double price);
    Task SellSpotAsync(string instId, double quantity, double price);
    Task BuySwapAsync(string instId, double quantity, double price);
    Task SellSwapAsync(string instId, double quantity, double price);
    Task CloseShortSwapAsync(string instId, double quantity, double price); // ÐÂÔö£ºÆ½¿Õ²Ö
}