using CSVProssessor.Domain.Entities;
using CSVProssessor.Infrastructure.Interfaces;
using Microsoft.Azure.Cosmos;
using System.Linq.Expressions;

namespace CSVProssessor.Infrastructure.Repositories;

public class CosmosRepository<TEntity> : IGenericRepository<TEntity> where TEntity : BaseEntity
{
    private readonly Container _container;
    private readonly IClaimsService _claimsService;
    private readonly ICurrentTime _timeService;

    public CosmosRepository(Container container, ICurrentTime timeService, IClaimsService claimsService)
    {
        _container = container;
        _timeService = timeService;
        _claimsService = claimsService;
    }

    public async Task<TEntity> AddAsync(TEntity entity)
    {
        var currentUserId = _claimsService.CurrentUserId;

        // Set timestamps
        entity.CreatedAt = _timeService.GetCurrentTime().ToUniversalTime();
        entity.UpdatedAt = _timeService.GetCurrentTime().ToUniversalTime();

        if (entity.CreatedBy == Guid.Empty)
            entity.CreatedBy = currentUserId;

        entity.UpdatedBy = currentUserId;

        // Get partition key value from entity
        var partitionKey = GetPartitionKey(entity);

        await _container.CreateItemAsync(entity, new PartitionKey(partitionKey));
        return entity;
    }

    public async Task AddRangeAsync(List<TEntity> entities)
    {
        var currentUserId = _claimsService.CurrentUserId;
        var currentTime = _timeService.GetCurrentTime().ToUniversalTime();

        foreach (var entity in entities)
        {
            entity.CreatedAt = currentTime;
            entity.UpdatedAt = currentTime;
            entity.CreatedBy = currentUserId;

            var partitionKey = GetPartitionKey(entity);
            await _container.CreateItemAsync(entity, new PartitionKey(partitionKey));
        }
    }

    public async Task<List<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>>? predicate = null,
        params Expression<Func<TEntity, object>>[] includes)
    {
        var query = _container.GetItemQueryIterator<TEntity>(
            new QueryDefinition("SELECT * FROM c WHERE c.IsDeleted = false")
        );

        var results = new List<TEntity>();
        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync();
            results.AddRange(response);
        }

        // Apply predicate if provided
        if (predicate != null)
        {
            results = results.Where(predicate.Compile()).ToList();
        }

        return results;
    }

    public async Task<TEntity?> GetByIdAsync(Guid id, params Expression<Func<TEntity, object>>[] includes)
    {
        try
        {
            var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.id = @id AND c.IsDeleted = false")
                .WithParameter("@id", id.ToString());

            var query = _container.GetItemQueryIterator<TEntity>(queryDefinition);

            if (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                return response.FirstOrDefault();
            }

            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> SoftRemove(TEntity entity)
    {
        entity.IsDeleted = true;
        entity.DeletedAt = _timeService.GetCurrentTime().ToUniversalTime();
        entity.DeletedBy = _claimsService.CurrentUserId;
        entity.UpdatedAt = _timeService.GetCurrentTime().ToUniversalTime();
        entity.UpdatedBy = _claimsService.CurrentUserId;

        var partitionKey = GetPartitionKey(entity);
        await _container.UpsertItemAsync(entity, new PartitionKey(partitionKey));

        return true;
    }

    public async Task<bool> SoftRemoveRange(List<TEntity> entities)
    {
        var currentTime = _timeService.GetCurrentTime().ToUniversalTime();
        var currentUserId = _claimsService.CurrentUserId;

        foreach (var entity in entities)
        {
            entity.IsDeleted = true;
            entity.DeletedAt = currentTime;
            entity.DeletedBy = currentUserId;
            entity.UpdatedAt = currentTime;
            entity.UpdatedBy = currentUserId;

            var partitionKey = GetPartitionKey(entity);
            await _container.UpsertItemAsync(entity, new PartitionKey(partitionKey));
        }

        return true;
    }

    public async Task<bool> SoftRemoveRangeById(List<Guid> entitiesId)
    {
        var queryDefinition = new QueryDefinition(
            "SELECT * FROM c WHERE c.IsDeleted = false AND ARRAY_CONTAINS(@ids, c.id)")
            .WithParameter("@ids", entitiesId.Select(x => x.ToString()).ToList());

        var query = _container.GetItemQueryIterator<TEntity>(queryDefinition);
        var entities = new List<TEntity>();

        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync();
            entities.AddRange(response);
        }

        return await SoftRemoveRange(entities);
    }

    public async Task<bool> Update(TEntity entity)
    {
        entity.UpdatedAt = _timeService.GetCurrentTime().ToUniversalTime();
        entity.UpdatedBy = _claimsService.CurrentUserId;

        var partitionKey = GetPartitionKey(entity);
        await _container.UpsertItemAsync(entity, new PartitionKey(partitionKey));

        return true;
    }

    public async Task<bool> UpdateRange(List<TEntity> entities)
    {
        var currentTime = _timeService.GetCurrentTime().ToUniversalTime();
        var currentUserId = _claimsService.CurrentUserId;

        foreach (var entity in entities)
        {
            entity.UpdatedAt = currentTime;
            entity.UpdatedBy = currentUserId;

            var partitionKey = GetPartitionKey(entity);
            await _container.UpsertItemAsync(entity, new PartitionKey(partitionKey));
        }

        return true;
    }

    public IQueryable<TEntity> GetQueryable()
    {
        // Cosmos DB doesn't support full LINQ translation
        // This returns an in-memory queryable of active entities
        return GetAllAsync().Result.AsQueryable();
    }

    public async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>>? predicate = null,
        params Expression<Func<TEntity, object>>[] includes)
    {
        var queryDefinition = new QueryDefinition("SELECT TOP 1 * FROM c WHERE c.IsDeleted = false");
        var query = _container.GetItemQueryIterator<TEntity>(queryDefinition);

        if (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync();
            var result = response.FirstOrDefault();

            if (result != null && predicate != null && !predicate.Compile().Invoke(result))
            {
                return null;
            }

            return result;
        }

        return null;
    }

    public async Task<bool> HardRemove(Expression<Func<TEntity, bool>> predicate)
    {
        try
        {
            var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.IsDeleted = false");
            var query = _container.GetItemQueryIterator<TEntity>(queryDefinition);
            var entities = new List<TEntity>();

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                entities.AddRange(response);
            }

            var entitiesToDelete = entities.Where(predicate.Compile()).ToList();

            foreach (var entity in entitiesToDelete)
            {
                var partitionKey = GetPartitionKey(entity);
                await _container.DeleteItemAsync<TEntity>(entity.Id.ToString(), new PartitionKey(partitionKey));
            }

            return entitiesToDelete.Any();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error while performing hard remove: {ex.Message}", ex);
        }
    }

    public async Task<bool> HardRemoveRange(List<TEntity> entities)
    {
        try
        {
            foreach (var entity in entities)
            {
                var partitionKey = GetPartitionKey(entity);
                await _container.DeleteItemAsync<TEntity>(entity.Id.ToString(), new PartitionKey(partitionKey));
            }

            return entities.Any();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error while performing hard remove range: {ex.Message}", ex);
        }
    }

    public async Task<bool> HardRemoveAsyn(TEntity entity)
    {
        try
        {
            var partitionKey = GetPartitionKey(entity);
            await _container.DeleteItemAsync<TEntity>(entity.Id.ToString(), new PartitionKey(partitionKey));
            return true;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error while performing hard remove: {ex.Message}", ex);
        }
    }

    public async Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate)
    {
        try
        {
            var queryDefinition = new QueryDefinition("SELECT COUNT(1) as count FROM c WHERE c.IsDeleted = false");
            var query = _container.GetItemQueryIterator<dynamic>(queryDefinition);

            if (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                var result = response.FirstOrDefault();
                return (int?)result?.count ?? 0;
            }

            return 0;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error while performing count: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get partition key value from entity - override in derived classes if needed
    /// </summary>
    private string GetPartitionKey(TEntity entity)
    {
        // Use Id as partition key (most reliable for CosmosDB)
        return entity.Id.ToString();
    }
}
