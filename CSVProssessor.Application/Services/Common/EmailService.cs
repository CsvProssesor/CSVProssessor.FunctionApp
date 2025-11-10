using CSVProssessor.Application.Interfaces.Common;
using CSVProssessor.Domain.DTOs.EmailDTOs;
using CSVProssessor.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Resend;


public class EmailService : IEmailService
{
    private readonly string _fromEmail;
    private readonly IResend _resend;
    private readonly IUnitOfWork _unitOfWork;

    public EmailService(IResend resend, IConfiguration configuration, IUnitOfWork unitOfWork)
    {
        _resend = resend;
        _fromEmail = configuration["RESEND_FROM"] ?? "noreply@fpt-devteam.fun";
        _unitOfWork = unitOfWork;
    }

    public async Task SendDatabaseChanges(EmailRequestDto request)
    {
        var subject = "Database Change Notification";
        var htmlContent = @"
            <h2>Database Change Detected</h2>
            <p>Hello,</p>
            <p>There has been a change detected in your database.</p>
            <p>Please log in to your account for more details.</p>
            <p>Best regards,<br/>CSV Processor Team</p>
        ";

        await SendEmailAsync(request.To, subject, htmlContent);
    }

    private async Task SendEmailAsync(string to, string subject, string htmlContent)
    {
        var message = new EmailMessage
        {
            From = _fromEmail,
            Subject = subject,
            HtmlBody = htmlContent
        };

        message.To.Add(to);
        await _resend.EmailSendAsync(message);
    }
}