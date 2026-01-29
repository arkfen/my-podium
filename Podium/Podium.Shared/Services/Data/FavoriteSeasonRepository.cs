using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface IFavoriteSeasonRepository
{
    Task<List<FavoriteSeason>> GetUserFavoriteSeasonsAsync(string userId);
    Task<bool> AddFavoriteSeasonAsync(string userId, string seasonId, string seasonName, string seriesName, int year);
    Task<bool> RemoveFavoriteSeasonAsync(string userId, string seasonId);
    Task<int> GetUserFavoriteCountAsync(string userId);
    Task<bool> IsFavoriteAsync(string userId, string seasonId);
}

public class FavoriteSeasonRepository : IFavoriteSeasonRepository
{
    private readonly ITableClientFactory _tableClientFactory;
    private const string TableName = "PodiumFavoriteSeasons";

    public FavoriteSeasonRepository(ITableClientFactory tableClientFactory)
    {
        _tableClientFactory = tableClientFactory;
    }

    public async Task<List<FavoriteSeason>> GetUserFavoriteSeasonsAsync(string userId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var favorites = new List<FavoriteSeason>();

        try
        {
            var filter = $"PartitionKey eq '{userId}'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                favorites.Add(MapToFavoriteSeason(entity));
            }

            // Sort by added date descending (most recent first)
            return favorites.OrderByDescending(f => f.AddedDate).ToList();
        }
        catch (RequestFailedException)
        {
            return favorites;
        }
    }

    public async Task<bool> AddFavoriteSeasonAsync(string userId, string seasonId, string seasonName, string seriesName, int year)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var entity = new TableEntity(userId, seasonId)
            {
                { "UserId", userId },
                { "SeasonId", seasonId },
                { "SeasonName", seasonName },
                { "SeriesName", seriesName },
                { "Year", year },
                { "AddedDate", DateTime.UtcNow }
            };

            await tableClient.UpsertEntityAsync(entity);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<bool> RemoveFavoriteSeasonAsync(string userId, string seasonId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            await tableClient.DeleteEntityAsync(userId, seasonId);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<int> GetUserFavoriteCountAsync(string userId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var count = 0;

        try
        {
            var filter = $"PartitionKey eq '{userId}'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter, select: new[] { "PartitionKey" }))
            {
                count++;
            }

            return count;
        }
        catch (RequestFailedException)
        {
            return 0;
        }
    }

    public async Task<bool> IsFavoriteAsync(string userId, string seasonId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(userId, seasonId);
            return response.Value != null;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    private static FavoriteSeason MapToFavoriteSeason(TableEntity entity)
    {
        return new FavoriteSeason
        {
            UserId = entity.GetString("UserId") ?? string.Empty,
            SeasonId = entity.GetString("SeasonId") ?? string.Empty,
            SeasonName = entity.GetString("SeasonName") ?? string.Empty,
            SeriesName = entity.GetString("SeriesName") ?? string.Empty,
            Year = entity.GetInt32("Year") ?? 0,
            AddedDate = entity.GetDateTime("AddedDate") ?? DateTime.UtcNow
        };
    }
}
