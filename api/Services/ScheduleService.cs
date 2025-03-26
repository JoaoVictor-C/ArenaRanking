using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArenaBackend.Services
{
   public class ScheduleService : IScheduleService, IDisposable
   {
      private readonly List<CancellationTokenSource> _cancellationTokenSources = new List<CancellationTokenSource>();
      private readonly object _lock = new object();
      private bool _disposed = false;

      public CancellationTokenSource ScheduleTask(Action action, TimeSpan delay)
      {
         if (action == null) throw new ArgumentNullException(nameof(action));
         
         var cts = new CancellationTokenSource();
         
         Task.Run(async () =>
         {
            try
            {
               await Task.Delay(delay, cts.Token);
               if (!cts.Token.IsCancellationRequested)
               {
                  action();
               }
            }
            catch (TaskCanceledException)
            {
               // Task was cancelled, no action required
            }
            catch (Exception)
            {
               // Log exception if needed
            }
            finally
            {
               RemoveTokenSource(cts);
            }
         }, cts.Token);

         RegisterTokenSource(cts);
         return cts;
      }

      public CancellationTokenSource ScheduleTask(Func<Task> action, TimeSpan delay)
      {
         if (action == null) throw new ArgumentNullException(nameof(action));
         
         var cts = new CancellationTokenSource();
         
         Task.Run(async () =>
         {
            try
            {
               await Task.Delay(delay, cts.Token);
               if (!cts.Token.IsCancellationRequested)
               {
                  await action();
               }
            }
            catch (TaskCanceledException)
            {
               // Task was cancelled, no action required
            }
            catch (Exception)
            {
               // Log exception if needed
            }
            finally
            {
               RemoveTokenSource(cts);
            }
         }, cts.Token);

         RegisterTokenSource(cts);
         return cts;
      }

      public CancellationTokenSource ScheduleRecurringTask(Action action, TimeSpan interval, TimeSpan? initialDelay = null)
      {
         if (action == null) throw new ArgumentNullException(nameof(action));
         
         var cts = new CancellationTokenSource();
         
         Task.Run(async () =>
         {
            try
            {
               if (initialDelay.HasValue)
               {
                  await Task.Delay(initialDelay.Value, cts.Token);
               }

               while (!cts.Token.IsCancellationRequested)
               {
                  try
                  {
                     action();
                  }
                  catch (Exception)
                  {
                     // Log exception if needed
                  }
                  await Task.Delay(interval, cts.Token);
               }
            }
            catch (TaskCanceledException)
            {
               // Task was cancelled, no action required
            }
            finally
            {
               RemoveTokenSource(cts);
            }
         }, cts.Token);

         RegisterTokenSource(cts);
         return cts;
      }

      public CancellationTokenSource ScheduleRecurringTask(Func<Task> action, TimeSpan interval, TimeSpan? initialDelay = null)
      {
         if (action == null) throw new ArgumentNullException(nameof(action));
         
         var cts = new CancellationTokenSource();
         
         Task.Run(async () =>
         {
            try
            {
               if (initialDelay.HasValue)
               {
                  await Task.Delay(initialDelay.Value, cts.Token);
               }

               while (!cts.Token.IsCancellationRequested)
               {
                  try
                  {
                     await action();
                  }
                  catch (Exception)
                  {
                     // Log exception if needed
                  }
                  await Task.Delay(interval, cts.Token);
               }
            }
            catch (TaskCanceledException)
            {
               // Task was cancelled, no action required
            }
            finally
            {
               RemoveTokenSource(cts);
            }
         }, cts.Token);

         RegisterTokenSource(cts);
         return cts;
      }

      public void CancelAllTasks()
      {
         lock (_lock)
         {
            foreach (var cts in _cancellationTokenSources.ToArray())
            {
               if (!cts.IsCancellationRequested)
               {
                  cts.Cancel();
               }
            }
            _cancellationTokenSources.Clear();
         }
      }

      private void RegisterTokenSource(CancellationTokenSource cts)
      {
         lock (_lock)
         {
            if (!_disposed)
            {
               _cancellationTokenSources.Add(cts);
            }
            else
            {
               cts.Cancel();
               cts.Dispose();
            }
         }
      }

      private void RemoveTokenSource(CancellationTokenSource cts)
      {
         lock (_lock)
         {
            _cancellationTokenSources.Remove(cts);
            cts.Dispose();
         }
      }

      public void Dispose()
      {
         Dispose(true);
         GC.SuppressFinalize(this);
      }

      protected virtual void Dispose(bool disposing)
      {
         if (disposing)
         {
            lock (_lock)
            {
               if (!_disposed)
               {
                  CancelAllTasks();
                  _disposed = true;
               }
            }
         }
      }
   }
}