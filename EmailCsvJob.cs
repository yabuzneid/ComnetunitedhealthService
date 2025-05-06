using CsvHelper;
using MimeKit;
using Quartz;
using System.Globalization;
using System.Data.SqlClient;
using System.Net;
using System.Net.Mail;
using System.IO;
using System.Text; 
public class EmailCsvJob : IJob
{
    private readonly IConfiguration _config;

    public EmailCsvJob(IConfiguration config)
    {
        _config = config;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var connectionString = _config.GetConnectionString("DefaultConnection");
        var emailSettings = _config.GetSection("Email");
        var recipients = emailSettings.GetSection("Recipients").Get<string[]>();
        var data = new List<Dictionary<string, object>>();

        try
        { 
            using var conn = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(
                @"SELECT [Username], [FirstName], [LastName]
                  FROM [ComnetUnitedHealth].[dbo].[tUser]
                  WHERE Active = 1
                  AND Username NOT LIKE '%@comnetcomm.com'
                  AND Username <> 'comnetuser'
                  ORDER BY Username", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader[i];
                data.Add(row);
            }

            if (data.Count == 0)
            {
                Console.WriteLine("No active user data found.");
                return;
            }

            await using var stream = new MemoryStream();
            await using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                foreach (var header in data[0].Keys)
                    csv.WriteField(header);
                await csv.NextRecordAsync();

                foreach (var row in data)
                {
                    foreach (var value in row.Values)
                        csv.WriteField(value);
                    await csv.NextRecordAsync();
                }
            }

            stream.Position = 0;

            var mailMessage = new MailMessage
            {
                From = new MailAddress(emailSettings["From"]),
                Subject = "Active Users CSV Report",
                Body = "Please find the attached CSV report for active users.",
                IsBodyHtml = false
            };

            foreach (var to in recipients)
                mailMessage.To.Add(to);

            var attachment = new Attachment(stream, "active_users.csv", "text/csv");
            mailMessage.Attachments.Add(attachment);

            using var smtpClient = new SmtpClient(emailSettings["SmtpServer"], int.Parse(emailSettings["Port"]))
            {
                EnableSsl = bool.Parse(emailSettings["EnableSsl"] ?? "true"),
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(emailSettings["Username"], emailSettings["Password"])
            };

            smtpClient.Send(mailMessage);
            Console.WriteLine("Email with CSV attachment sent successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
        }
    }
}
