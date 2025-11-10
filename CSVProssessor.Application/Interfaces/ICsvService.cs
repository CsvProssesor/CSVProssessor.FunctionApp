using CSVProssessor.Domain.DTOs.CsvJobDTOs;
using Microsoft.AspNetCore.Http;

namespace CSVProssessor.Application.Interfaces;

public interface ICsvService
{
    Task<ImportCsvResponseDto> ImportCsvAsync(IFormFile file);
    Task SaveCsvRecordsAsync(Guid jobId, string fileName);
    Task PublishCsvChangeAsync(string changeType, object changedDocument);
    Task LogCsvChangesAsync();
    Task<(string fileName, byte[] fileBytes)> ParseMultipartFormDataAsync(string contentType, Stream body);
}