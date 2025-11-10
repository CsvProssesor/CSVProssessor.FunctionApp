using CSVProssessor.Application.Interfaces.Common;
using CSVProssessor.Domain.DTOs.CsvJobDTOs;
using CSVProssessor.Domain.DTOs.EmailDTOs;
using Microsoft.AspNetCore.Http;

namespace CSVProssessor.Application.Interfaces;

public interface ICsvService
{
    Task<ImportCsvResponseDto> ImportCsvAsync(IFormFile file);
    Task SaveCsvRecordsAsync(Guid jobId, string fileName);
    Task PublishCsvChangeAsync(string changeType, object changedDocument);
    Task SubscribeToDatabaseChangesAsync(IEmailService emailService);
    Task LogCsvChangesAsync();
    Task<(string fileName, byte[] fileBytes)> ParseMultipartFormDataAsync(string contentType, Stream body);
}