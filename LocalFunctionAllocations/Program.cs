using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Attributes;
using System.Threading.Tasks;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using MemoryDiagnoser = BenchmarkDotNet.Diagnosers.MemoryDiagnoser;
using BenchmarkDotNet.Validators;
using BenchmarkDotNet.Columns;

namespace TheAwaitingGame
{
    class Program
    {
        static void Main()
        {
            // tell BenchmarkDotNet not to force GC.Collect after benchmark iteration 
            // (single iteration contains of multiple (usually millions) of invocations)
            // it can influence the allocation-heavy Task<T> benchmarks
            var gcMode = new GcMode { Force = false };

            var customConfig = ManualConfig
                .Create(DefaultConfig.Instance) // copies all exporters, loggers and basic stuff
                .With(JitOptimizationsValidator.FailOnError) // Fail if not release mode
                .With(MemoryDiagnoser.Default) // use memory diagnoser
                .With(StatisticColumn.OperationsPerSecond) // add ops/s
                .With(Job.Default.With(gcMode));

            var summary = BenchmarkRunner.Run<Benchmarker>(customConfig);
            Console.WriteLine(summary);
        }
    }

    public class Benchmarker
    {
        static OrderBook _book;
        public Benchmarker()
        {
            // touch the static field to ensure .cctor has run
            GC.KeepAlive(_book);
        }
        static Benchmarker()
        {
            var rand = new Random(12345);

            var book = new OrderBook();
            for (int i = 0; i < 50; i++)
            {
                var order = new Order();
                int lines = rand.Next(1, 10);
                for (int j = 0; j < lines; j++)
                {
                    order.Lines.Add(new OrderLine
                    {
                        Quantity = rand.Next(1, 20)
                    });
                }
                book.Orders.Add(order);
            }
            _book = book;
        }

        [Benchmark(Description = "Sync", Baseline = true)]
        public int Sync() => _book.GetTotalQuantity();

        [Benchmark(Description = "ValueTask Pure Async")]
        public ValueTask<int> ValueTaskPureAsync() => _book.GetTotalQuantityPureAsync();

        [Benchmark(Description = "ValueTask + Local Async via Params")]
        public ValueTask<int> ValueTaskAsync() => _book.GetTotalQuantityAsync();

        [Benchmark(Description = "ValueTask + Local Async via Locals")]
        public ValueTask<int> ValueTaskLocalsAsync() => _book.GetTotalQuantityLocalsAsync();
    }

    public class OrderBook
    {
        public List<Order> Orders { get; } = new List<Order>();

        public int GetTotalQuantity()
        {
            int total = 0;
            for (int i = 0; i < Orders.Count; i++)
            {
                total += Orders[i].GetOrderQuantity();
            }
            return total;
        }

        public async ValueTask<int> GetTotalQuantityPureAsync()
        {
            int total = 0;
            for (int i = 0; i < Orders.Count; i++)
            {
                total += await Orders[i].GetOrderQuantityPureAsync();
            }
            return total;
        }

        public ValueTask<int> GetTotalQuantityAsync()
        {
            {
                int total = 0;
                for (int i = 0; i < Orders.Count; i++)
                {
                    var task = Orders[i].GetOrderQuantityAsync();
                    if (!task.IsCompletedSuccessfully) return Awaited(task, total, i);
                    total += task.Result;
                }
                return new ValueTask<int>(total);
            }
            async ValueTask<int> Awaited(ValueTask<int> task, int total, int i)
            {
                total += await task;
                for (i++; i < Orders.Count; i++)
                {
                    task = Orders[i].GetOrderQuantityAsync();
                    total += (task.IsCompletedSuccessfully) ? task.Result : await task;
                }
                return total;
            }
        }

        public ValueTask<int> GetTotalQuantityLocalsAsync()
        {
            int total = 0;
            int i = 0;
            ValueTask<int> task;
            for (; i < Orders.Count; i++)
            {
                task = Orders[i].GetOrderQuantityLocalsAsync();
                if (!task.IsCompletedSuccessfully) return Awaited();
                total += task.Result;
            }
            return new ValueTask<int>(total);

            async ValueTask<int> Awaited()
            {
                total += await task;
                for (i++; i < Orders.Count; i++)
                {
                    task = Orders[i].GetOrderQuantityLocalsAsync();
                    total += (task.IsCompletedSuccessfully) ? task.Result : await task;
                }
                return total;
            }
        }
    }

    public class Order
    {
        public List<OrderLine> Lines { get; } = new List<OrderLine>();
        public int GetOrderQuantity()
        {
            int total = 0;
            for (var i = 0; i < Lines.Count; i++)
            {
                total += Lines[i].GetLineQuantity();
            }
            return total;
        }
        public async ValueTask<int> GetOrderQuantityPureAsync()
        {
            int total = 0;
            for (var i = 0; i < Lines.Count; i++)
            {
                total += await Lines[i].GetLineQuantityAsync();
            }
            return total;
        }
        public ValueTask<int> GetOrderQuantityAsync()
        {
            {
                int total = 0;
                for (var i = 0; i < Lines.Count; i++)
                {
                    var task = Lines[i].GetLineQuantityAsync();
                    if (!task.IsCompletedSuccessfully) return Awaited(i, task, total);
                    total += task.Result;
                }
                return new ValueTask<int>(total);
            }
            async ValueTask<int> Awaited(int i, ValueTask<int> task, int total)
            {
                total += await task;
                for (i++; i < Lines.Count; i++)
                {
                    task = Lines[i].GetLineQuantityAsync();
                    total += (task.IsCompletedSuccessfully) ? task.Result : await task;
                }
                return total;
            }
        }
        public ValueTask<int> GetOrderQuantityLocalsAsync()
        {
            int total = 0;
            int i = 0;
            ValueTask<int> task;
            for (; i < Lines.Count; i++)
            {
                task = Lines[i].GetLineQuantityAsync();
                if (!task.IsCompletedSuccessfully) return Awaited();
                total += task.Result;
            }
            return new ValueTask<int>(total);

            async ValueTask<int> Awaited()
            {
                total += await task;
                for (i++; i < Lines.Count; i++)
                {
                    task = Lines[i].GetLineQuantityAsync();
                    total += (task.IsCompletedSuccessfully) ? task.Result : await task;
                }
                return total;
            }
        }
    }

    public class OrderLine
    {
        public int Quantity { get; set; }
        public ValueTask<int> GetLineQuantityAsync() => new ValueTask<int>(Quantity);
        public int GetLineQuantity() => Quantity;
    }
}