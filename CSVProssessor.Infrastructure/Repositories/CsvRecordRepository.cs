using CSVProssessor.Domain.Entities;
using Microsoft.Azure.Cosmos;
using CSVProssessor.Infrastructure.Interfaces;

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