using System;
using System.Threading;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Configurations;

namespace FakeDiscountDetector.Core.Interfaces
{
    public interface IMessageQueueService
    {
        Task PublishScrapingTaskAsync(ScraperConfig config);
        Task ConsumeScrapingTasksAsync(Func<ScraperConfig, Task> onMessage, CancellationToken cancellationToken);
    }
}
