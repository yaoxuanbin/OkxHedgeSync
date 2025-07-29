public interface IPositionClient
{
    Task<double> GetSpotPositionAsync(string instId);
    Task<double> GetSwapPositionAsync(string instId);
}