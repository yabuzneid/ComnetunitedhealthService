using Quartz;

namespace ComnetUnitedHealthService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddHostedService<Worker>();



            var host = Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddQuartz(q =>
                    {
                        q.UseMicrosoftDependencyInjectionJobFactory();

                        var cronExpr = hostContext.Configuration["CronSchedule"];

                        q.AddJob<EmailCsvJob>(opts => opts.WithIdentity("CsvJob"));
                        q.AddTrigger(opts => opts
                            .ForJob("CsvJob")
                            .WithIdentity("CsvJobTrigger")
                            //.StartNow()
                            .WithCronSchedule(cronExpr)
                            );
                    });

                    services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
                    services.AddTransient<EmailCsvJob>();
                })
                .Build();
            //var host = builder.Build();
            host.Run();
        }
    }
}