namespace ManjuTailoringCentre.Services
{
    public interface IEmailService
    {
        Task<bool> SendAppointmentNotificationAsync(string customerName, string customerPhone, string message);
    }
}
