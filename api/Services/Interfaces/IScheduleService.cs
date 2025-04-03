using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArenaBackend.Services
{
   public interface IScheduleService
   {
      CancellationTokenSource ScheduleTask(Action action, TimeSpan delay);

      CancellationTokenSource ScheduleTask(Func<Task> action, TimeSpan delay);

      CancellationTokenSource ScheduleRecurringTask(Action action, TimeSpan interval, TimeSpan? initialDelay = null);

      void CancelAllTasks();
   }
}