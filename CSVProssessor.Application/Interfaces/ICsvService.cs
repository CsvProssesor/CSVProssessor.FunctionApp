namespace CSVProssessor.Application.Interfaces
{
    using CSVProssessor.Domain.DTOs.CsvJobDTOs;
    using Microsoft.AspNetCore.Http;

    public interface ICsvService
    {
        Task<ImportCsvResponseDto> ImportCsvAsync(IFormFile file);
        Task SaveCsvRecordsAsync(Guid jobId, string fileName);
        Task PublishCsvChangeAsync(string changeType, object changedDocument);
        Task LogCsvChangesAsync();
        Task<(string fileName, byte[] fileBytes)> ParseMultipartFormDataAsync(string contentType, Stream body);
    }
}