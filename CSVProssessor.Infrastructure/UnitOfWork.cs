using System.Linq.Expressions;
using CSVProssessor.Domain;
using CSVProssessor.Domain.Entities;
using CSVProssessor.Infrastructure.Interfaces;
using CSVProssessor.Infrastructure.Repositories;

namespace CSVProssessor.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly IClaimsService _claimsService;
    private readonly CosmosDbContext _cosmosDbContext;
    private readonly ICurrentTime _timeService;
    private IGenericRepository<CsvJob>? _csvJobRepository;
    private IGenericRepository<CsvRecord>? _csvRecordRepository;

    public UnitOfWork(CosmosDbContext cosmosDbContext, ICurrentTime timeService, IClaimsService claimsService)
    {
        _cosmosDbContext = cosmosDbContext;
        _timeService = timeService;
        _claimsService = claimsService;
    }

    public IGenericRepository<CsvJob> CsvJobs
    {
        get
        {
            if (_csvJobRepository == null)
                _csvJobRepository = new CosmosRepository<CsvJob>(_cosmosDbContext.CsvJobContainer,
                    _timeService, _claimsService);
            return _csvJobRepository;
        }
    }

    public IGenericRepository<CsvRecord> CsvRecords
    {
        get
        {
            if (_csvRecordRepository == null)
                _csvRecordRepository = new CsvRecordRepository(_cosmosDbContext.CsvRecordContainer,
                    _timeService, _claimsService);
            return _csvRecordRepository;
        }
    }

    public void Dispose()
    {
        // DO NOT dispose CosmosDbContext - it's a singleton managed by DI container
        // Disposing it here causes "Cannot access a disposed 'CosmosClient'" errors
        // The DI container will handle its disposal when the application shuts down
    }

    public async Task<int> SaveChangesAsync()
    {
        // Cosmos DB saves automatically when we call CreateItemAsync, UpsertItemAsync, DeleteItemAsync
        // This method is kept for compatibility
        return await Task.FromResult(1);
    }

    // Where
    public IQueryable<T> Where<T>(Expression<Func<T, bool>> predicate) where T : class
    {
        // Cosmos DB doesn't support full LINQ translation
        // This is a limitation - you should use repository methods instead
        throw new NotImplementedException("Use repository methods directly for querying Cosmos DB");
    }

    // Select
    public IQueryable<TResult> Select<T, TResult>(Expression<Func<T, TResult>> selector) where T : class
    {
        // Cosmos DB doesn't support full LINQ translation
        throw new NotImplementedException("Use repository methods directly for querying Cosmos DB");
    }
}