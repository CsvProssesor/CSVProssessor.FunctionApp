using CSVProssessor.Domain.DTOs.EmailDTOs;

namespace CSVProssessor.Application.Interfaces.Common;

public interface IEmailService
{
    Task SendDatabaseChanges(EmailRequestDto request);
}