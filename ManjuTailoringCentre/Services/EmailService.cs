using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ManjuTailoringCentre.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;
        private const string AdminEmail = "amahendrareddy123@gmail.com";

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<bool> SendAppointmentNotificationAsync(string customerName, string customerPhone, string message)
        {
            try
            {
                // Method 1: Web3Forms API (Passwordless, zero SMTP setup - recommended for accounts using Google Passkeys)
                var web3Key = _config["Web3Forms:AccessKey"] ?? _config["Web3Forms__AccessKey"];
                if (!string.IsNullOrWhiteSpace(web3Key))
                {
                    using var httpClient = new HttpClient();
                    var payload = new Dictionary<string, string>
                    {
                        { "access_key", web3Key },
                        { "subject", $"✨ New Tailoring Request: {customerName} ({customerPhone})" },
                        { "from_name", "Manju Ladies Tailoring" },
                        { "to_email", AdminEmail },
                        { "Customer Name", customerName },
                        { "Phone Number", customerPhone },
                        { "Direct WhatsApp Chat", $"https://wa.me/91{customerPhone}" },
                        { "Requirement / Message", message },
                        { "Received At", $"{DateTime.UtcNow.AddHours(5.5):dd MMM yyyy, hh:mm tt} IST" }
                    };

                    var response = await httpClient.PostAsync("https://api.web3forms.com/submit", new FormUrlEncodedContent(payload));
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Successfully sent appointment notification via Web3Forms to {AdminEmail} for {Customer}", AdminEmail, customerName);
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Web3Forms response status code: {Status}", response.StatusCode);
                    }
                }

                // Method 2: Gmail / Custom SMTP Client
                var smtpHost = _config["Smtp:Host"] ?? "smtp.gmail.com";
                var smtpPort = int.TryParse(_config["Smtp:Port"], out var port) ? port : 587;
                var smtpUser = _config["Smtp:Username"] ?? _config["Smtp__Username"];
                var smtpPass = _config["Smtp:Password"] ?? _config["Smtp__Password"];

                if (string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
                {
                    _logger.LogWarning("Email credentials not configured. Appointment request logged for {Name} ({Phone}): {Message}", customerName, customerPhone, message);
                    return false;
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                var mail = new MailMessage
                {
                    From = new MailAddress(smtpUser, "Manju Ladies Tailoring Alerts"),
                    Subject = $"✨ New Appointment Request: {customerName} ({customerPhone})",
                    IsBodyHtml = true,
                    Body = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #fce4ec; border-radius: 12px; background: #fff;'>
                            <h2 style='color: #c2185b; margin-top: 0;'>New Appointment / Tailoring Request</h2>
                            <p style='color: #666;'>A new inquiry was submitted through the Manju Ladies Tailoring website:</p>
                            <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                                <tr style='border-bottom: 1px solid #eee;'>
                                    <td style='padding: 10px; font-weight: bold; width: 35%; color: #333;'>Customer Name:</td>
                                    <td style='padding: 10px; color: #111;'>{customerName}</td>
                                </tr>
                                <tr style='border-bottom: 1px solid #eee;'>
                                    <td style='padding: 10px; font-weight: bold; color: #333;'>Phone Number:</td>
                                    <td style='padding: 10px; color: #111;'>
                                        <a href='tel:{customerPhone}' style='color: #0d6efd; text-decoration: none;'>{customerPhone}</a> &nbsp;|&nbsp;
                                        <a href='https://wa.me/91{customerPhone}' style='color: #25d366; text-decoration: none; font-weight: bold;'>Chat on WhatsApp</a>
                                    </td>
                                </tr>
                                <tr style='border-bottom: 1px solid #eee;'>
                                    <td style='padding: 10px; font-weight: bold; color: #333;'>Requirement / Message:</td>
                                    <td style='padding: 10px; color: #111; white-space: pre-wrap;'>{message}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 10px; font-weight: bold; color: #333;'>Received At:</td>
                                    <td style='padding: 10px; color: #666;'>{DateTime.UtcNow.AddHours(5.5):dd MMM yyyy, hh:mm tt} IST</td>
                                </tr>
                            </table>
                            <div style='margin-top: 20px; padding: 12px; background: #fff7f9; border-radius: 8px; color: #c2185b; font-size: 13px;'>
                                📍 Manju Ladies Tailoring Centre, Gandhi Chowk, Gadwal.
                            </div>
                        </div>"
                };

                mail.To.Add(AdminEmail);
                await client.SendMailAsync(mail);
                _logger.LogInformation("Successfully sent appointment notification to {AdminEmail} for {Customer}", AdminEmail, customerName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send appointment notification email to {AdminEmail}", AdminEmail);
                return false;
            }
        }
    }
}
