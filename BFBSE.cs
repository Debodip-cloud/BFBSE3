using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Threading; 

using System.Threading.Tasks;
using BFBSE;

using BFBSE.tbse_exchange;
using BFBSE.tbse_msg_classes;
//using Python.Runtime

// using BFBSE.Config;
//using BFBSE.tbse_customer_orders;
// using tbse_exchange;
// using tbse_tader_agents;

namespace BFBSE
{
    class BFBSE
    {
       public static bool TradeStats(string expid, Dictionary<string, Trader> traders, StreamWriter dumpfile)
{
    var traderTypes = new Dictionary<string, Dictionary<string, dynamic>>();
    bool allBalancesZero = false;

    foreach (var t in traders.Keys)
    {
        var trader = traders[t];
        var traderType = trader.TType;
        double tTime1 = 0;
        double tTime2 = 0;

        if (!traderTypes.ContainsKey(traderType))
        {
            traderTypes[traderType] = new Dictionary<string, dynamic>
            {
                { "n", 0 },
                { "balance_sum", 0.0 },
                { "trades_sum", 0 },
                { "time1", 0.0 },
                { "time2", 0.0 }
            };
        }

        var stats = traderTypes[traderType];
        stats["balance_sum"] += trader.Balance;
        stats["trades_sum"] += trader.NTrades;
        stats["n"] += 1;

        if (trader.LastQuote != null)
        {
            tTime1 = trader.Times[0] / trader.Times[2];
            tTime2 = trader.Times[1] / trader.Times[3];
            stats["time1"] += tTime1;
            stats["time2"] += tTime2;
        }
    }
     double check_sum=0;
    // Check if all balance sums are zero
     foreach (var traderType in traderTypes.Keys.OrderBy(k => k))
    {
        var stats = traderTypes[traderType];
        check_sum += stats["balance_sum"];
    }
       
     if (check_sum == 0)
        {
            allBalancesZero = true;
            
        }

    if (!allBalancesZero)
    {
        dumpfile.Write($"{expid}");
        foreach (var traderType in traderTypes.Keys.OrderBy(k => k))
        {
            var stats = traderTypes[traderType];
            var n_t = stats["n"];
            var s = stats["balance_sum"];
            var t = stats["trades_sum"];
            var time1 = stats["time1"];
            var time2 = stats["time2"];
            Console.WriteLine("number of trades to be printed: " + t);
            Console.WriteLine("balance_sum: " + s);
            Console.WriteLine("number of traders: " + n_t);
            string formattedString = $", {traderType}, {s}, {n_t}, " +
                                     $"{(s / n_t).ToString("F2", CultureInfo.InvariantCulture)}, " +
                                     $"{(t / n_t).ToString("F2", CultureInfo.InvariantCulture)}, " +
                                     $"{(time1 / n_t).ToString("F8", CultureInfo.InvariantCulture)}, " +
                                     $"{(time2 / n_t).ToString("F8", CultureInfo.InvariantCulture)}";

            dumpfile.Write(formattedString);
        }
        dumpfile.Write('\n');
    }

    return allBalancesZero;
}

        // Adapted from original BSE code
        // From original BSE code
        public static (int n_buyers, int n_sellers) PopulateMarket(Dictionary<string, List<(string, int)>> traderSpec, Dictionary<string, Trader> traders, bool shuffle, bool verbose)
        {
            // Function that creates instances of the different Trader Types
            object CreateTrader(string robotType, string name)
            {
                switch (robotType)
                {
                    case "GVWY":
                        return new TraderGiveaway("GVWY", name, 0.00, 0);
                    case "ZIC":
                        return new TraderZic("ZIC", name, 0.00, 0);
                    case "SHVR":
                        return new TraderShaver("SHVR", name, 0.00, 0);
                    case "SNPR":
                        return new TraderSniper("SNPR", name, 0.00, 0);
                    case "ZIP":
                        return new TraderZip("ZIP", name, 0.00, 0);
                    case "AA":
                        return new TraderAa("AA", name, 0.00, 0);
                    case "GDX":
                        return new TraderGdx("GDX", name, 0.00, 0);
                    case "TraderMomentum":
                        return new TraderMomentum("TraderMomentum", name, 0.00, 0, 0.05);
                    default:
                        throw new Exception($"FATAL: don't know robot type {robotType}\n");
                }
            }

            // Shuffles traders to avoid any biases caused by trader position.
            void ShuffleTraders(char ttypeChar, int n, Dictionary<string, Trader>traderList)
            {
                Random random = new Random();
                for (int swap = 0; swap < n; swap++)
                {
                    int t1 = (n - 1) - swap;
                    int t2 = random.Next(0, t1 + 1);
                    string t1name = $"{ttypeChar}{t1.ToString("D2")}";
                    string t2name = $"{ttypeChar}{t2.ToString("D2")}";
                    traderList[t1name].TID = t2name;
                    traderList[t2name].TID = t1name;
                    var temp = traderList[t1name];
                    traderList[t1name] = traderList[t2name];
                    traderList[t2name] = temp;
                }
            }

            int nBuyers = 0;
            foreach (var bs in traderSpec["buyers"])
            {
                string traderType = bs.Item1;
                for (int i = 0; i < bs.Item2; i++)
                {
                    string traderName = $"B{nBuyers.ToString("D2")}";
                    traders[traderName] = (Trader)CreateTrader(traderType, traderName);
                    nBuyers++;
                }
            }

            if (nBuyers < 1)
            {
                throw new Exception("FATAL: no buyers specified\n");
            }

            if (shuffle)
            {
                ShuffleTraders('B', nBuyers, traders);
            }

            int nSellers = 0;
            foreach (var ss in traderSpec["sellers"])
            {
                string traderType = ss.Item1;
                for (int i = 0; i < ss.Item2; i++)
                {
                    string traderName = $"S{nSellers.ToString("D2")}";
                    traders[traderName] = (Trader)CreateTrader(traderType, traderName);
                    nSellers++;
                }
            }

            if (nSellers < 1)
            {
                throw new Exception("FATAL: no sellers specified\n");
            }

            if (shuffle)
            {
                ShuffleTraders('S', nSellers, traders);
            }

            if (verbose)
            {
                for (int t = 0; t < nBuyers; t++)
                {
                    string bname = $"B{t.ToString("D2")}";
                   // Console.WriteLine(traders[bname]);
                }
                for (int t = 0; t < nSellers; t++)
                {
                    string bname = $"S{t.ToString("D2")}";
                   // Console.WriteLine(traders[bname]);
                }
            }

            return (nBuyers, nSellers);
        }
//     public static int RunExchange2(
//     Exchange exchange,
//     ConcurrentQueue<Order> orderQ,
//     List<ConcurrentQueue<List<object>>> traderQs,
//     ConcurrentQueue<Order> killQ,
//     Stopwatch stopwatch,
//     int sessLength,
//     double virtualEnd,
//     bool processVerbose)
// {
//     var completedCoid = new Dictionary<int, bool>();

//     var ordersToBatch = new List<Order>();
//     var batchPeriod = Config.BatchInterval;
//     var requiredBatchNumber = 1;
//     var lastBatchTime = 0.0;

//     while (stopwatch.Elapsed.TotalSeconds < sessLength)
//     {
//         double virtualTime = stopwatch.Elapsed.TotalSeconds * (virtualEnd / sessLength);

//         while (!killQ.IsEmpty)
//         {
//             if (killQ.TryDequeue(out var order))
//             {
//                 exchange.DelOrder(virtualTime, order);
//             }
//         }

//         if (orderQ.TryDequeue(out var newOrder))
//         {
//             if (completedCoid.ContainsKey(newOrder.Coid))
//             {
//                 if (completedCoid[newOrder.Coid])
//                 {
//                     continue;
//                 }
//             }
//             else
//             {
//                 completedCoid[newOrder.Coid] = false;

//                 ordersToBatch.RemoveAll(o => o.Tid == newOrder.Tid);

//                 lock (exchange)
//                 {
//                     var ordersOnExchange = exchange.Asks.Orders.Values.Concat(exchange.Bids.Orders.Values).ToList();
//                     foreach (var o in ordersOnExchange)
//                     {
//                         if (o.Tid == newOrder.Tid)
//                         {
//                             exchange.DelOrder(virtualTime, o);
//                         }
//                     }
//                 }

//                 ordersToBatch.Add(newOrder);
//             }

//             var elapsedTime = virtualTime - lastBatchTime;
//             if (elapsedTime >= batchPeriod && requiredBatchNumber != 0)
//             {
//                 var (trades, lob, pEq, qEq, demandCurve, supplyCurve) = exchange.ProcessOrderBatch2(virtualTime, ordersToBatch, processVerbose);
//                 foreach (var trade in trades)
//                 {
//                     completedCoid[trade["coid"]] = true;
//                     completedCoid[trade["counter"]] = true;
//                 }

//                 foreach (var q in traderQs)
//                 {
//                     var list = new List<object> { trades, lob, pEq, qEq, demandCurve, supplyCurve };
//                     q.Enqueue(list);
//                 }

//                 ordersToBatch.Clear();
//                 lastBatchTime = virtualTime;
//             }
//         }

//         //Thread.Sleep(10); // Small delay to simulate time passing
//     }

//     return 0;
// }

// public static int RunTrader2(
//     Trader trader,
//     Exchange exchange,
//     ConcurrentQueue<Order> orderQ,
//     ConcurrentQueue<List<object>> traderQ,
//     Stopwatch stopwatch,
//     double sessLength,
//     double virtualEnd,
//     bool respondVerbose,
//     bool bookkeepVerbose)
// {
//     List<object> trades = new List<object>();
//     double pEq = -1;
//     int qEq = 0;
//     List<(double Price, int Qty)> demandCurve = new List<(double Price, int Qty)>();
//     List<(double Price, int Qty)> supplyCurve = new List<(double Price, int Qty)>();

//     while (stopwatch.Elapsed.TotalSeconds < sessLength)
//     {
//         //Thread.Sleep(10);
//         double virtualTime = stopwatch.Elapsed.TotalSeconds * (virtualEnd / sessLength);
//         double timeLeft = (virtualEnd - virtualTime) / virtualEnd;

//         while (!traderQ.IsEmpty)
//         {
//             if (traderQ.TryDequeue(out var traderData))
//             {
//                 trades = (List<object>)traderData[0];
//                 dynamic lob = traderData[1];
//                 pEq = (double)traderData[2];
//                 qEq = (int)traderData[3];
//                 demandCurve = (List<(double Price, int Qty)>)traderData[4];
//                 supplyCurve = (List<(double Price, int Qty)>)traderData[5];

//                 foreach (var trade in trades)
//                 {
//                     Dictionary<string, dynamic> tradeDict = (Dictionary<string, dynamic>)trade;
//                     if (tradeDict["party1"].ToString() == trader.TID)
//                     {
//                         trader.Bookkeep(tradeDict, null, bookkeepVerbose, virtualTime);
//                     }
//                     if (tradeDict["party2"].ToString() == trader.TID)
//                     {
//                         trader.Bookkeep(tradeDict, null, bookkeepVerbose, virtualTime);
//                     }
//                 }

//                 trader.Respond(virtualTime, pEq, qEq, demandCurve, supplyCurve, lob, trades, respondVerbose);
//             }
//         }

//         dynamic lobUpdate = exchange.PublishLob(virtualTime, false);
//         trader.Respond(virtualTime, pEq, qEq, demandCurve, supplyCurve, lobUpdate, trades, respondVerbose);
//         Order order = trader.GetOrder(virtualTime, pEq, qEq, demandCurve, supplyCurve, timeLeft, lobUpdate);

//         if (order != null)
//         {
//             if (order.Otype == "Ask" && (int)order.Price < (int)trader.Orders[order.Coid].Price)
//             {
//                 throw new Exception("Bad ask");
//             }
//             if (order.Otype == "Bid" && (int)order.Price > (int)trader.Orders[order.Coid].Price)
//             {
//                 throw new Exception("Bad bid");
//             }
//             trader.NQuotes = 1;
//             orderQ.Enqueue(order);
//         }
//     }

//     return 0;
// }
    public static int RunExchange(
        Exchange exchange,
        ConcurrentQueue<Order> orderQ,
        List<ConcurrentQueue<List<object>>> traderQs,
        ConcurrentQueue<Order> killQ,
        ManualResetEvent startEvent,
        Stopwatch stopwatch,
        int sessLength,
        double virtualEnd,
        bool processVerbose)
    {
        var completedCoid = new Dictionary<int, bool>();
        startEvent.WaitOne();

        var ordersToBatch = new List<Order>();
        var batchPeriod = Config.BatchInterval;
        var requiredBatchNumber = 1;
        var lastBatchTime = 0.0;

        while (startEvent.WaitOne(0))
        {
            double virtualTime = stopwatch.Elapsed.TotalSeconds * (virtualEnd / sessLength);

            while (!killQ.IsEmpty)
            {
                if (killQ.TryDequeue(out var order))
                {
                    exchange.DelOrder(virtualTime, order);
                }
            }
            // Boolean dequed=false;
            // if (!orderQ.TryDequeue(out var newOrder))
            //     {
            //         var spinWait = new SpinWait();
            //     var start = DateTime.UtcNow;

            //     // Loop until 1 second has passed
            //     while ((DateTime.UtcNow - start).TotalMilliseconds < 1000)
            //     {
            //         spinWait.SpinOnce();
            //     }

            //         continue;
            //     }
            // else{
            //     dequed=true;
            // }


             if (orderQ.TryDequeue(out var newOrder))
            {
                if (completedCoid.ContainsKey(newOrder.Coid))
                {
                    if (completedCoid[newOrder.Coid])
                    {
                        continue;
                    }
                }
                else
                {
                    completedCoid[newOrder.Coid] = false;

                    ordersToBatch.RemoveAll(o => o.Tid == newOrder.Tid);

                   lock (exchange)
                    {
                        var ordersOnExchange = exchange.Asks.Orders.Values.Concat(exchange.Bids.Orders.Values).ToList();
                        foreach (var o in ordersOnExchange)
                        {
                            if (o.Tid == newOrder.Tid)
                            {
                                exchange.DelOrder(virtualTime, o);
                            }
                        }
                    }
                    // Console.WriteLine("order to be added"+ newOrder.ToString());
                    
                    ordersToBatch.Add(newOrder);
                }

                var elapsedTime = virtualTime - lastBatchTime;
                if (elapsedTime >= batchPeriod && requiredBatchNumber != 0)
                {
                    var (trades, lob, pEq, qEq, demandCurve, supplyCurve) = exchange.ProcessOrderBatch2(virtualTime, ordersToBatch, processVerbose);
                    //Console.WriteLine("printing trades in run exchange");
                    //Console.WriteLine(trades[0]);
                    foreach (var trade in trades)
                    {
                        completedCoid[trade["coid"]] = true;
                        completedCoid[trade["counter"]] = true;
                    }

                    foreach (var q in traderQs)
                    {
                        var list = new List<object> { trades, lob, pEq, qEq, demandCurve, supplyCurve };
                        q.Enqueue(list);
                        //Console.WriteLine("enque length "+trades.Count);
                    }

                    ordersToBatch.Clear();
                    lastBatchTime = virtualTime;
                }
            }
        }

        return 0;
    }

    public static int RunTrader(
        Trader trader,
        Exchange exchange,
        ConcurrentQueue<Order> orderQ,
        ConcurrentQueue<List<object>> traderQ,
        ManualResetEvent startEvent,
        Stopwatch stopwatch,
        double sessLength,
        double virtualEnd,
        bool respondVerbose,
        bool bookkeepVerbose)
    {
        startEvent.WaitOne();
        
        List<object> trades = new List<object>();
        double pEq = -1;
        int qEq = 0;
        List<(double Price, int Qty)> demandCurve = new List<(double Price, int Qty)>();
        List<(double Price, int Qty)> supplyCurve = new List<(double Price, int Qty)>();

        while (startEvent.WaitOne(0))
        {
            Thread.Sleep(10);
            double virtualTime = stopwatch.Elapsed.TotalSeconds * (virtualEnd / sessLength);
            double timeLeft = (virtualEnd - virtualTime) / virtualEnd;

            while (!traderQ.IsEmpty)
            {
                if (traderQ.TryDequeue(out var traderData))
                {
                    
                    //  Console.WriteLine("trader data");
                    trades = (List<object>)traderData[0];
                    dynamic lob = traderData[1];
                    pEq = (double)traderData[2];
                    qEq = (int)traderData[3];
                    demandCurve = (List<(double Price, int Qty)>)traderData[4];
                    supplyCurve = (List<(double Price, int Qty)>)traderData[5];
                    // Console.WriteLine("printing trades");
                    // Console.WriteLine(trades.Count);
                    
                   for (int i = 0; i < trades.Count; i++)
{
    // Console.WriteLine(trades[i]);
    Dictionary<string, dynamic> trade = (Dictionary<string, object>)trades[i];
    // Console.WriteLine("operating");
    // Console.WriteLine(trade);
    if (trade["party1"].ToString() == trader.TID)
    {
        // Console.WriteLine("operating");
        trader.Bookkeep(trade, null, bookkeepVerbose, virtualTime);
    }
    if (trade["party2"].ToString() == trader.TID)
    {
        trader.Bookkeep(trade, null, bookkeepVerbose, virtualTime);
    }
}

                    var time1 = DateTime.Now;
                    trader.Respond(virtualTime, pEq, qEq, demandCurve, supplyCurve, lob, trades, respondVerbose);
                    var time2 = DateTime.Now;
                    trader.Times[1] += (time2 - time1).TotalSeconds;
                    trader.Times[3] += 1;
                }
            }

            dynamic lobUpdate = exchange.PublishLob(virtualTime, false);
            var time3 = DateTime.Now;
            trader.Respond(virtualTime, pEq, qEq, demandCurve, supplyCurve, lobUpdate, trades, respondVerbose);
            //Console.WriteLine("here2"); prints sucessfully
            var time4 = DateTime.Now;
            //Console.WriteLine("trader is"+trader.GetType());// prints sucessfully
            Order order = trader.GetOrder(virtualTime, pEq, qEq, demandCurve, supplyCurve, timeLeft, lobUpdate);
            //Console.WriteLine("order generated" + order.ToString());  
            var time5 = DateTime.Now;
            trader.Times[1] += (time4 - time3).TotalSeconds;
            trader.Times[3] += 1;
            if (order != null)
            {
                if (order.Otype == "Ask" && (int)order.Price < (int)trader.Orders[order.Coid].Price)
                {
                    //Console.WriteLine(trader.TType);
                    throw new Exception("Bad ask");
                }
                if (order.Otype == "Bid" && (int)order.Price > (int)trader.Orders[order.Coid].Price)
                {
                    throw new Exception("Bad bid");
                }
                trader.NQuotes = 1;
                orderQ.Enqueue(order);
                trader.Times[0] += (time5 - time4).TotalSeconds;
                trader.Times[2] += 1;
            }
        }

        return 0;
    }
    // public static int MarketSession2(
    //     string sessId,
    //     int sessLength,
    //     int virtualEnd,
    //     Dictionary<string, List<(string, int)>> traderSpec,
    //     Dictionary<string, object> orderSchedule,
    //     StreamWriter tdump,
    //     bool verbose)
    // {
    //     Exchange exchange = new Exchange();
    //     ConcurrentQueue<Order> orderQueue = new ConcurrentQueue<Order>();
    //     ConcurrentQueue<Order> killQueue = new ConcurrentQueue<Order>();

    //     Stopwatch stopwatch = new Stopwatch();
    //     stopwatch.Start();

    //     bool ordersVerbose = false;
    //     bool processVerbose = false;
    //     bool respondVerbose = false;
    //     bool bookkeepVerbose = false;

    //     Dictionary<string, Trader> traders = new Dictionary<string, Trader>();
    //     var traderStats = PopulateMarket(traderSpec, traders, true, verbose);
    //     List<ConcurrentQueue<List<object>>> traderQueues = new List<ConcurrentQueue<List<object>>>();

    //     // Initialize trader queues
    //     for (int i = 0; i < traders.Count; i++)
    //     {
    //         traderQueues.Add(new ConcurrentQueue<List<object>>());
    //     }

    //     // Main loop to simulate the market session
    //     int cuid = 0;
    //     List<Order> pendingCustOrders = new List<Order>();

    //     while (stopwatch.Elapsed.TotalSeconds < sessLength)
    //     {
    //         double virtualTime = stopwatch.Elapsed.TotalSeconds * (virtualEnd / (double)sessLength);

    //         // Process customer orders
    //         var result = tbse_customer_orders.CustomerOrders(virtualTime, cuid, traders, traderStats, orderSchedule, pendingCustOrders, ordersVerbose);
    //         pendingCustOrders = result.Item1;
    //         List<string> kills = result.Item2;
    //         cuid = result.Item3;

    //         foreach (var kill in kills)
    //         {
    //             if (verbose)
    //             {
    //                 Console.WriteLine($"Kills: {kills}");
    //                 Console.WriteLine($"last_quote={traders[kill].LastQuote}");
    //             }
    //             if (traders[kill].LastQuote != null)
    //             {
    //                 killQueue.Enqueue(traders[kill].LastQuote);
    //                 if (verbose)
    //                 {
    //                     Console.WriteLine($"Killing order {traders[kill].LastQuote}");
    //                 }
    //             }
    //         }

    //         // Simulate trader activities
    //         for (int i = 0; i < traders.Count; i++)
    //     {
    //         traderQueues.Add(new ConcurrentQueue<List<object>>());
    //          int currentIndex = i;
    //              string tid = traders.Keys.ElementAt(i);
    //         {
    //             RunTrader2(

    //                 traders[tid],
    //                 exchange,
    //                 orderQueue,
    //                 traderQueues[currentIndex],
    //                 stopwatch,
    //                 sessLength,
    //                 virtualEnd,
    //                 respondVerbose,
    //                 bookkeepVerbose);
    //         }

    //         // Simulate exchange activities
    //         RunExchange2(
    //             exchange,
    //             orderQueue,
    //             traderQueues,
    //             killQueue,
    //             stopwatch,
    //             sessLength,
    //             virtualEnd,
    //             processVerbose);

    //         Thread.Sleep(10); // Small delay to simulate time passing
    //     }

    //     stopwatch.Stop();

    //     // Finalize session
    //     exchange.TapeDump("transactions.csv", "a", "keep");
    //     TradeStats(sessId, traders, tdump);

    //     if (verbose)
    //     {
    //         Console.WriteLine($"Market session {sessId} completed. Total traders: {traders.Count}");
    //     }

      
    // }
    //   return traders.Count; 
    // }
  private static readonly object consoleLock = new object();
  private static readonly object threadCountLock = new object();
  private static int managedThreadCount = 1; // Including the main thread
  private static readonly object fileLock = new object();

    public static int MarketSession(
    string sessId,
    int sessLength,
    int virtualEnd,
    Dictionary<string, List<(string, int)>> traderSpec,
    Dictionary<string, object> orderSchedule,
    ManualResetEvent startEvent, StreamWriter tdump,
    bool verbose)
{
    bool rerunSession = true;

    while (rerunSession)
    {
        Exchange exchange = new Exchange();
        ConcurrentQueue<Order> orderQueue = new ConcurrentQueue<Order>();
        ConcurrentQueue<Order> killQueue = new ConcurrentQueue<Order>();

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        bool ordersVerbose = false;
        bool processVerbose = false;
        bool respondVerbose = false;
        bool bookkeepVerbose = false;

        Dictionary<string, Trader> traders = new Dictionary<string, Trader>();
        List<Thread> traderThreads = new List<Thread>();
        List<ConcurrentQueue<List<object>>> traderQueues = new List<ConcurrentQueue<List<object>>>();
        var traderStats = PopulateMarket(traderSpec, traders, true, verbose);
        Console.WriteLine("traders dictionary " + traders.ToString());
        for (int i = 0; i < traders.Count; i++)
        {
            traderQueues.Add(new ConcurrentQueue<List<object>>());

            if (i >= traders.Keys.Count || i >= traderQueues.Count)
            {
                Console.WriteLine("Index out of range error.");
                continue;
            }

            string tid = traders.Keys.ElementAt(i);
            int currentIndex = i;
            Thread traderThread = new Thread(() =>
            {
                try
                {
                    startEvent.WaitOne();
                    RunTrader(
                        traders[tid],
                        exchange,
                        orderQueue,
                        traderQueues[currentIndex],
                        startEvent,
                        stopwatch,
                        sessLength,
                        virtualEnd,
                        respondVerbose,
                        bookkeepVerbose);
                }
                catch (Exception ex)
                {
                    // Console.WriteLine($"Exception in trader thread: {ex.Message}");
                    // StackTrace stackTrace = new StackTrace(ex, true);
                    // foreach (StackFrame frame in stackTrace.GetFrames())
                    // {
                    //     Console.WriteLine("File: " + frame.GetFileName());
                    //     Console.WriteLine("Method: " + frame.GetMethod().Name);
                    //     Console.WriteLine("Line Number: " + frame.GetFileLineNumber());
                    // }
                }
            });
            traderThreads.Add(traderThread);
        }

        Thread exThread = new Thread(() =>
        {
            try
            {
                startEvent.WaitOne();
                RunExchange(
                    exchange,
                    orderQueue,
                    traderQueues,
                    killQueue,
                    startEvent,
                    stopwatch,
                    sessLength,
                    virtualEnd,
                    processVerbose);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in exchange thread: {ex.Message}");
            }
        });

        StartManagedThread(exThread);

        foreach (var thread in traderThreads)
        {
            StartManagedThread(thread);
        }

        startEvent.Set();
        Console.WriteLine("length of trader threads " + traderThreads.Count);
        List<Order> pendingCustOrders = new List<Order>();

        if (verbose)
        {
            Console.WriteLine($"\n{sessId};  ");
        }

        int cuid = 0;

        while (stopwatch.Elapsed.TotalSeconds < sessLength)
        {
            double virtualTime = stopwatch.Elapsed.TotalSeconds * (virtualEnd / (double)sessLength);

            var result = tbse_customer_orders.CustomerOrders(virtualTime, cuid, traders, traderStats, orderSchedule, pendingCustOrders, ordersVerbose);
            pendingCustOrders = result.Item1;
            List<string> kills = result.Item2;
            cuid = result.Item3;

            if (kills.Count > 0)
            {
                if (verbose)
                {
                    Console.WriteLine($"Kills: {kills}");
                }
                foreach (var kill in kills)
                {
                    if (verbose)
                    {
                        Console.WriteLine($"last_quote={traders[kill].LastQuote}");
                    }
                    if (traders[kill].LastQuote != null)
                    {
                        killQueue.Enqueue(traders[kill].LastQuote);
                        if (verbose)
                        {
                            Console.WriteLine($"Killing order {traders[kill].LastQuote}");
                        }
                    }
                }
            }
            Thread.Sleep(10);
        }

        startEvent.Reset();

        exThread.Join();

        foreach (var thread in traderThreads)
        {
            thread.Join();
        }

        lock (fileLock)
        {
            exchange.TapeDump("transactions.csv", "a", "keep");
        }

        int managedThreadCount = GetManagedThreadCount();
        Console.WriteLine("Number of managed threads: " + managedThreadCount);

        rerunSession = TradeStats(sessId, traders, tdump);
        // if (!rerunSession)
        // {
        //     TradeStats(sessId, traders, tdump);
        // }
    }

    return GetManagedThreadCount();
}
           private static void StartManagedThread(Thread thread)
    {
        lock (threadCountLock)
        {
            managedThreadCount++;
        }
        thread.Start();
    }

    private static void DecrementManagedThreadCount()
    {
        lock (threadCountLock)
        {
            managedThreadCount--;
        }
    }

    private static int GetManagedThreadCount()
    {
        lock (threadCountLock)
        {
            return managedThreadCount;
        }
    }


        public delegate int RScheduleOffsetFunction(double t, List<object> parameters);
         public delegate int ScheduleOffsetDelegate(double t);
        //RScheduleOffsetFunction  offsetFunctionDelegate = RealWorldScheduleOffsetFunction;
        public static Dictionary<string, object> GetOrderSchedule()
        {
            // Produces order schedule as defined in config file.
            // :return: Order schedule representing the supply/demand curve of the market

            Random random = new Random();
            int rangeMax = random.Next(Config.Supply["rangeMax"]["rangeLow"], Config.Supply["rangeMax"]["rangeHigh"]);
            int rangeMin = random.Next(Config.Supply["rangeMin"]["rangeLow"], Config.Supply["rangeMin"]["rangeHigh"]);

            object rangeS;
            if (Config.UseInputFile)
            {
                var offsetFunctionEventList = GetOffsetEventList();
                //var Realworld= RealWorldScheduleOffsetFunction();
                rangeS = new Tuple<int, int, object[]>(rangeMin, rangeMax, new object[] { new RScheduleOffsetFunction(RealWorldScheduleOffsetFunction), new object[] { offsetFunctionEventList } });

            }
            else if (Config.UseOffset)
            {
                rangeS = new Tuple<int, int, ScheduleOffsetDelegate>(rangeMin, rangeMax, ScheduleOffsetFunction);
            }
            else
            {
                rangeS = new Tuple<int, int>(rangeMin, rangeMax);
            }

            var supplySchedule = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object>
            {
                { "from", 0 },
                { "to", Config.VirtualSessionLength },
                { "ranges", new List<object> { rangeS } },
                { "stepmode", Config.StepMode }
            }
        };

            if (!Config.Symmetric)
            {
                rangeMax = random.Next(Config.Demand["rangeMax"]["rangeLow"], Config.Demand["rangeMax"]["rangeHigh"]);
                rangeMin = random.Next(Config.Demand["rangeMin"]["rangeLow"], Config.Demand["rangeMin"]["rangeHigh"]);
            }

            object rangeD;
            if (Config.UseInputFile)
            {
                var offsetFunctionEventList = GetOffsetEventList();
                rangeD = new Tuple<int, int, object[]>(rangeMin, rangeMax, new object[] { new RScheduleOffsetFunction(RealWorldScheduleOffsetFunction), new object[] { offsetFunctionEventList } });
            }
            else if (Config.UseOffset)
            {
                rangeD = new Tuple<int, int, ScheduleOffsetDelegate>(rangeMin, rangeMax, ScheduleOffsetFunction);
            }
            else
            {
                rangeD = new Tuple<int, int>(rangeMin, rangeMax);
            }

            var demandSchedule = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object>
            {
                { "from", 0 },
                { "to", Config.VirtualSessionLength },
                { "ranges", new List<object> { rangeD } },
                { "stepmode", Config.StepMode }
            }
        };

            return new Dictionary<string, object>
        {
            { "sup", supplySchedule },
            { "dem", demandSchedule },
            { "interval", Config.Interval },
            { "timemode", Config.TimeMode }
        };
        }
        public static int ScheduleOffsetFunction(double t)
        {
            Console.WriteLine(t);
            double pi2 = Math.PI * 2;
            double c = Math.PI * 3000;
            double wavelength = t / c;
            double gradient = 100 * t / (c / pi2);
            double amplitude = 100 * t / (c / pi2);
            double offset = gradient + amplitude * Math.Sin(wavelength * t);
            return (int)Math.Round(offset, 0);
        }

        public static int RealWorldScheduleOffsetFunction(double t, List<object> parameters)
        {
            double endTime = Convert.ToDouble(parameters[0]);
            List<List<double>> offsetEvents = (List<List<double>>)parameters[1];
            double percentElapsed = t / endTime;
            int offset = 0;
            foreach (var evt in offsetEvents)
            {
                offset = (int)evt[1];
                if (percentElapsed < evt[0])
                {
                    break;
                }
            }
            return offset;
        }

       public static List<List<double>> GetOffsetEventList()
{
    var offsetFunctionEventList = new List<List<double>>();
    using (var reader = new StreamReader(Config.InputFile))
    {
        // Skip the header line
        string headerLine = reader.ReadLine();

        double scaleFactor = 80;
        double? minPrice = null;
        double? maxPrice = null;
        DateTime? firstTimeObj = null;
        var priceEvents = new List<List<double>>();
        double timeSinceStart = 0;

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            var values = line.Split(',');

            string t = values[1];
            if (firstTimeObj == null)
            {
                firstTimeObj = DateTime.ParseExact(t, "HH:mm:ss", CultureInfo.InvariantCulture);
            }
            DateTime timeObj = DateTime.ParseExact(t, "HH:mm:ss", CultureInfo.InvariantCulture);
            double price = Convert.ToDouble(values[2]);
            if (minPrice == null || price < minPrice)
            {
                minPrice = price;
            }
            if (maxPrice == null || price > maxPrice)
            {
                maxPrice = price;
            }
            timeSinceStart = (timeObj - firstTimeObj.Value).TotalSeconds;
            priceEvents.Add(new List<double> { timeSinceStart, price });
        }

        double priceRange = maxPrice.Value - minPrice.Value;
        double endTime = timeSinceStart;

        foreach (var evt in priceEvents)
        {
            double normldPrice = (evt[1] - minPrice.Value) / priceRange;
            normldPrice = Math.Min(normldPrice, 1.0);
            normldPrice = Math.Max(0.0, normldPrice);
            int price = (int)Math.Round(normldPrice * scaleFactor);
            var normldEvent = new List<double> { evt[0] / endTime, price };
            offsetFunctionEventList.Add(normldEvent);
        }
    }
    return offsetFunctionEventList;
}        static void Main(string[] args)
        {
            // pylint: disable=too-many-boolean-expressions
            bool USE_CONFIG = false;
            bool USE_CSV = false;
            bool USE_COMMAND_LINE = false;

            int NUM_ZIC = Config.NumZIC;
            int NUM_ZIP = Config.NumZIP;
            int NUM_GDX = Config.NumGDX;
            int NUM_AA = Config.NumAA;
            int NUM_GVWY = Config.NumGVWY;
            int NUM_SHVR = Config.NumSHVR;
            int NUM_Momentum=Config.NUM_Momentum;

            int NUM_OF_ARGS = args.Length;
            Console.WriteLine("NUM_OF_ARGS: {0}",NUM_OF_ARGS);
            if (NUM_OF_ARGS == 0)
            {
                USE_CONFIG = true;
            }
            else if (NUM_OF_ARGS == 1)
            {
                USE_CSV = true;
            }
            else if (NUM_OF_ARGS == 7)
            {
                USE_COMMAND_LINE = true;
                try
                {
                    NUM_ZIC = int.Parse(args[0]);
                    NUM_ZIP = int.Parse(args[1]);
                    NUM_GDX = int.Parse(args[2]);
                    NUM_AA = int.Parse(args[3]);
                    NUM_GVWY = int.Parse(args[4]);
                    NUM_SHVR = int.Parse(args[5]);
                    NUM_Momentum=int.Parse(args[6]);
                }
                catch (FormatException)
                {
                    Console.WriteLine("ERROR: Invalid trader schedule. Please enter six integer values.");
                    return;
                }
            }
            else
            {
                Console.WriteLine("Invalid input arguements.");
                Console.WriteLine("Options for running TBSE:");
                Console.WriteLine("	$ dotnet run  ---  Run using trader schedule from config.");
                Console.WriteLine(" $ dotnet run <string>.csv  ---  Enter name of csv file describing a series of trader schedules.");
                Console.WriteLine(" $ dotnet run <int> <int> <int> <int> <int> <int>  ---  Enter 6 integer values representing trader schedule.");
                return;
            }

            if (NUM_ZIC < 0 || NUM_ZIP < 0 || NUM_GDX < 0 || NUM_AA < 0 || NUM_GVWY < 0 || NUM_SHVR < 0 || NUM_Momentum < 0)
            {
                Console.WriteLine("ERROR: Invalid trader schedule. All input integers should be positive.");
                return;
            }

            if (USE_CONFIG || USE_COMMAND_LINE)
            {
                Dictionary<string, object> order_sched = GetOrderSchedule();

                List<(string, int)> buyers_spec = new List<(string, int)>
                {
                    ("ZIC", NUM_ZIC),
                    ("ZIP", NUM_ZIP),
                    ("GDX", NUM_GDX),
                    ("AA", NUM_AA),
                    ("GVWY", NUM_GVWY),
                    ("SHVR", NUM_SHVR),
                    ("TraderMomentum", NUM_Momentum),
                };

                List<(string, int)> sellers_spec = buyers_spec;
                Dictionary<string, List<(string, int)>> traders_spec = new Dictionary<string, List<(string, int)>>
                {
                    { "sellers", sellers_spec },
                    { "buyers", buyers_spec }
                };

                string file_name = $"{NUM_ZIC.ToString().PadLeft(2, '0')}-{NUM_ZIP.ToString().PadLeft(2, '0')}-{NUM_GDX.ToString().PadLeft(2, '0')}-{NUM_AA.ToString().PadLeft(2, '0')}-{NUM_GVWY.ToString().PadLeft(2, '0')}-{NUM_SHVR.ToString().PadLeft(2, '0')}-{NUM_Momentum.ToString().PadLeft(2, '0')}.csv";
                using (StreamWriter tdump = new StreamWriter(file_name, false))
                {
                    int trader_count = 0;
                    foreach ((string, int) ttype in buyers_spec)
                    {
                        trader_count += ttype.Item2;
                        Console.WriteLine("ttype: {0}",ttype);
                        Console.WriteLine("trader_count: {0}",trader_count);
                    }
                    foreach ((string, int) ttype in sellers_spec)
                    {
                        trader_count += ttype.Item2;
                    }

                    if (trader_count > 40)
                    {
                        Console.WriteLine("WARNING: Too many traders can cause unstable behaviour.");
                    }
                    //Console.WriteLine("buyers_spec: {0}",buyers_spec);
                    //Console.WriteLine(trader_count);
                    int trial = 1;
                    //bool dump_all = true;
                    while (trial < (Config.NumTrials + 1))
                    {
                        string trial_id = $"trial{trial.ToString().PadLeft(7, '0')}";
                        ManualResetEvent start_session_event = new ManualResetEvent(false);
                        try
                        {
                            // int NUM_THREADS = MarketSession2(
                            //     trial_id,
                            //     Config.SessionLength,
                            //     Config.VirtualSessionLength,
                            //     traders_spec,
                            //     order_sched,
                            //      tdump,
                            //     false);
                            int NUM_THREADS = MarketSession(
                                trial_id,
                                Config.SessionLength,
                                Config.VirtualSessionLength,
                                traders_spec,
                                order_sched,
                                start_session_event, tdump,
                                false);

                            // if (NUM_THREADS != trader_count + 2) // traders + exchange + market thread
                            // {
                            //     trial = trial - 1;
                            //     start_session_event.Reset();
                            //     Thread.Sleep(500);
                            //     Console.WriteLine("in sleep 3");
                            //     //Console.WriteLine();
                            //     //Console.WriteLine("Hitting this whenever Shaver agents are used");
                            //     //Console.WriteLine();
                            // }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error: Market session failed, trying again.");
                            Console.WriteLine(e);
                            trial = trial - 1;
                            start_session_event.Reset();
                            Thread.Sleep(500);
                            Console.WriteLine("In sleep 4");
                        }
                        tdump.Flush();
                        trial = trial + 1;
                    }
                }
            }
            else if (USE_CSV)
            {
                string server = args[0];
                List<List<string>> ratios = new List<List<string>>();
                try
                {
                    using (StreamReader csv_file = new StreamReader(server))
                    {
                        string line;
                        while ((line = csv_file.ReadLine()) != null)
                        {
                            List<string> ratio = new List<string>(line.Split(','));
                            ratios.Add(ratio);
                        }
                    }
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("ERROR: File " + server + " not found.");
                    return;
                }
                catch (IOException e)
                {
                    Console.WriteLine("ERROR: " + e);
                    return;
                }

                int trial_number = 1;
                foreach (List<string> ratio in ratios)
                {
                    try
                    {
                        NUM_ZIC = int.Parse(ratio[0]);
                        NUM_ZIP = int.Parse(ratio[1]);
                        NUM_GDX = int.Parse(ratio[2]);
                        NUM_AA = int.Parse(ratio[3]);
                        NUM_GVWY = int.Parse(ratio[4]);
                        NUM_SHVR = int.Parse(ratio[5]);
                    }
                    catch (FormatException)
                    {
                        Console.WriteLine("ERROR: Invalid trader schedule. Please enter six, comma-separated, integer values. Skipping this trader schedule.");
                        continue;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("ERROR: Unknown input error. Skipping this trader schedule." + e);
                        continue;
                    }
                    // pylint: disable=too-many-boolean-expressions
                    if (NUM_ZIC < 0 || NUM_ZIP < 0 || NUM_GDX < 0 || NUM_AA < 0 || NUM_GVWY < 0 || NUM_SHVR < 0)
                    {
                        Console.WriteLine("ERROR: Invalid trader schedule. All input integers should be positive. Skipping this trader schedule.");
                        continue;
                    }

                    string results_folder = "test_results";
                    if (!Directory.Exists(results_folder))
                    {
                        Directory.CreateDirectory(results_folder);
                    }
                    
                    string file_name = $"{results_folder}/{NUM_ZIC.ToString().PadLeft(2, '0')}-{NUM_ZIP.ToString().PadLeft(2, '0')}-{NUM_GDX.ToString().PadLeft(2, '0')}-{NUM_AA.ToString().PadLeft(2, '0')}-{NUM_GVWY.ToString().PadLeft(2, '0')}-{NUM_SHVR.ToString().PadLeft(2, '0')}.csv";
                    using (StreamWriter tdump = new StreamWriter(file_name, false))
                    {
                        for (int i = 0; i < Config.NumSchedulesPerRatio; i++)
                        {
                            Dictionary<string, object> order_sched = GetOrderSchedule();

                            List<(string, int)> buyers_spec = new List<(string, int)>
                            {
                                ("ZIC", NUM_ZIC),
                                ("ZIP", NUM_ZIP),
                                ("GDX", NUM_GDX),
                                ("AA", NUM_AA),
                                ("GVWY", NUM_GVWY),
                                ("SHVR", NUM_SHVR)
                            };

                            List<(string, int)> sellers_spec = buyers_spec;
                            Dictionary<string, List<(string, int)>> traders_spec = new Dictionary<string, List<(string, int)>>
                            {
                                { "sellers", sellers_spec },
                                { "buyers", buyers_spec }
                            };

                            int trader_count = 0;
                            foreach ((string, int) ttype in buyers_spec)
                            {
                                trader_count += ttype.Item2;
                            }
                            foreach ((string, int) ttype in sellers_spec)
                            {
                                trader_count += ttype.Item2;
                            }

                            if (trader_count > 40)
                            {
                                Console.WriteLine("WARNING: Too many traders can cause unstable behaviour.");
                            }
                            //Console.WriteLine("buyers_spec: {0}",buyers_spec);
                            int trial = 1;
                            while (trial <= Config.NumTrialsPerSchedule)
                            {
                                string trial_id = $"trial{trial_number.ToString().PadLeft(7, '0')}";
                                ManualResetEvent start_session_event = new ManualResetEvent(false);
                                try
                                {
                                    int NUM_THREADS = MarketSession(
                                        trial_id,
                                        Config.SessionLength,
                                        Config.VirtualSessionLength,
                                        traders_spec,
                                        order_sched,
                                        start_session_event,tdump,
                                        false);

                                    if (NUM_THREADS != trader_count + 2)
                                    {
                                        trial = trial - 1;
                                        trial_number = trial_number - 1;
                                        start_session_event.Reset();
                                        Thread.Sleep(500);
                                        Console.WriteLine("In sleep 5");
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine("Market session failed. Trying again. " + e);
                                    trial = trial - 1;
                                    trial_number = trial_number - 1;
                                    start_session_event.Reset();
                                    Thread.Sleep(500);
                                    Console.WriteLine($"Exception in trader thread: {e.Message}");
                                    Console.WriteLine("In sleep 6");
                                }
                                tdump.Flush();
                                trial = trial + 1;
                                trial_number = trial_number + 1;
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("ERROR: An unknown error has occurred. Something is very wrong.");
                return;
            }
        }

    }
}
