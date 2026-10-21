using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XoSoWeb.Models;
using XoSoWeb.Shares.BaseRepository;

namespace XoSoWeb.Services
{
    public sealed class NewsCollectionOptions
    {
        public int IntervalMinutes { get; set; } = 60;
        public bool AutomaticEnabled { get; set; } = true;
    }

    public sealed class VietlottNewsSourceStatus
    {
        public bool CanCollect { get; set; }
        public string Message { get; set; }
        public string SourceUrl { get; set; }
    }

    public interface IVietlottNewsSourceAdapter
    {
        Task<VietlottNewsSourceStatus> CheckAsync(CancellationToken cancellationToken);
    }

    public sealed class VietlottNewsSourceAdapter : IVietlottNewsSourceAdapter
    {
        public Task<VietlottNewsSourceStatus> CheckAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new VietlottNewsSourceStatus
            {
                CanCollect = false,
                SourceUrl = "https://vietlott.vn/vi/trung-thuong/ket-qua-trung-thuong/chu-nhan-jackpot",
                Message = "Chưa xác minh RSS/API công khai hoặc quyền truy xuất HTML. robots.txt trả trang HTML, không phải chính sách robots; tự động truy cập đang bị khóa."
            });
    }

    public sealed class NewsCollectionService
    {
        private static readonly SemaphoreSlim LocalGate = new SemaphoreSlim(1, 1);
        private readonly XoSoDBContext db;
        private readonly IVietlottNewsSourceAdapter adapter;
        private readonly ILogger<NewsCollectionService> logger;

        public NewsCollectionService(XoSoDBContext db, IVietlottNewsSourceAdapter adapter, ILogger<NewsCollectionService> logger)
        {
            this.db = db;
            this.adapter = adapter;
            this.logger = logger;
        }

        public async Task<NewsCollectionRun> RunAsync(string triggeredBy, CancellationToken cancellationToken)
        {
            if (!await LocalGate.WaitAsync(0, cancellationToken))
                return new NewsCollectionRun { Status = "Skipped", Message = "Một lượt thu thập khác đang chạy trên instance này." };

            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                var connection = db.Database.GetDbConnection();
                await using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction.GetDbTransaction();
                    command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = N'XoSoWeb.NewsCollection.Vietlott', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 0; SELECT @result;";
                    var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
                    if (result < 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return new NewsCollectionRun { Status = "Skipped", Message = "Một instance khác đang thu thập tin." };
                    }
                }

                var started = DateTime.UtcNow;
                var status = await adapter.CheckAsync(cancellationToken);
                var run = new NewsCollectionRun
                {
                    StartedAt = started,
                    CompletedAt = DateTime.UtcNow,
                    Status = status.CanCollect ? "Ready" : "Blocked",
                    TriggeredBy = triggeredBy,
                    FoundCount = 0,
                    NewDraftCount = 0,
                    DuplicateCount = 0,
                    ChangedSourceCount = 0,
                    ErrorCount = 0,
                    Message = status.Message
                };
                db.NewsCollectionRuns.Add(run);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogInformation("Vietlott news collection completed: status={Status}, found={Found}, new={New}, duplicates={Duplicates}, changed={Changed}, errors={Errors}. {Message}",
                    run.Status, run.FoundCount, run.NewDraftCount, run.DuplicateCount, run.ChangedSourceCount, run.ErrorCount, run.Message);
                return run;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Vietlott news collection failed.");
                throw;
            }
            finally
            {
                LocalGate.Release();
            }
        }
    }

    public sealed class NewsCollectionWorker : BackgroundService
    {
        private readonly IServiceScopeFactory scopes;
        private readonly IOptions<NewsCollectionOptions> options;
        private readonly ILogger<NewsCollectionWorker> logger;

        public NewsCollectionWorker(IServiceScopeFactory scopes, IOptions<NewsCollectionOptions> options, ILogger<NewsCollectionWorker> logger)
        {
            this.scopes = scopes;
            this.options = options;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var minutes = Math.Clamp(options.Value.IntervalMinutes, 5, 1440);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                    if (!options.Value.AutomaticEnabled) continue;
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<NewsCollectionService>()
                        .RunAsync("scheduler", stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Scheduled news collection failed; the next interval will retry.");
                }
            }
        }
    }
}
