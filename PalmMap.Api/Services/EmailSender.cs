using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;

namespace PalmMap.Api.Services;

public class EmailSender : IEmailSenderDev
{
    private readonly IWebHostEnvironment _env;
    private readonly string _from;

    public EmailSender(IWebHostEnvironment env, IConfiguration config)
    {
        _env = env;
        _from = config.GetValue<string>("Email:From") ?? "no-reply@naladoni.local";
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        var folder = Path.Combine(_env.WebRootPath ?? "wwwroot", "emails");
        Directory.CreateDirectory(folder);

        var fileName = Path.Combine(folder, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N") + ".html");

        var sb = new StringBuilder();
        sb.AppendLine($"From: {_from}");
        sb.AppendLine($"To: {to}");
        sb.AppendLine($"Subject: {subject}");
        sb.AppendLine();
        sb.AppendLine(htmlBody);

        await File.WriteAllTextAsync(fileName, sb.ToString(), Encoding.UTF8);
    }
}

public interface IEmailSenderDev
{
    Task SendEmailAsync(string to, string subject, string htmlBody);
}
