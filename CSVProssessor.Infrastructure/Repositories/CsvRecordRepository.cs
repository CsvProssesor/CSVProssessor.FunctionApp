using CSVProssessor.Domain.Entities;
using CSVProssessor.Infrastructure.Interfaces;
using Microsoft.Azure.Cosmos;

namespace CSVProssessor.Infrastructure.Repositories;

public class CsvRecordRepository : CosmosRepository<CsvRecord>
{
    public CsvRecordRepository(Container container, ICurrentTime timeService, IClaimsService claimsService)
        : base(container, timeService, claimsService)
    {
    }

    protected override string GetPartitionKey(CsvRecord entity)
    {
        return entity.JobId.ToString();
    }
}