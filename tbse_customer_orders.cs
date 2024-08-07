using System;
using System.Collections.Generic;
using BFBSE;
// Assuming that we have equivalents for these classes and constants in C#
using Random = System.Random;  // for the random functionality
using Config = BFBSE.Config;   // for the config settings
using Order = BFBSE.tbse_msg_classes.Order;
using System.Linq;
using System.Security.Cryptography;  // for the Order class from tbse_msg_classes
//using TBSE_SYS_MAX_PRICE = BFBSE.TbseSysConsts.TBSE_SYS_MAX_PRICE;  // for the system constants
//using TBSE_SYS_MIN_PRICE = BFBSE.TbseSysConsts.TBSE_SYS_MIN_PRICE;
namespace BFBSE
{
    public class OrderSchedule
    {
        public string Timemode { get; set; }
        public double Interval { get; set; }
        public List<Schedule> Dem { get; set; }
        public List<Schedule> Sup { get; set; }
    }

    public class Schedule
    {
        public double From { get; set; }
        public double To { get; set; }
        public List<object> Ranges { get; set; }
        public string Stepmode { get; set; }
    }

    public class tbse_customer_orders
    {
        public static (List<Order>, List<string>, int) CustomerOrders(
            double time, int coid, Dictionary<string, Trader> traders, 
             (int n_buyers, int n_sellers) traderStats, Dictionary<string, object> orderSched, 
            List<Order> pending, bool verbose)
        {
            double SysMinCheck(double price)
            {
                if (price < TbseSysConsts.TBSE_SYS_MIN_PRICE)
                {
                    Console.WriteLine("WARNING: price < bse_sys_min -- clipped");
                    price = TbseSysConsts.TBSE_SYS_MIN_PRICE;
                }
                return price;
            }

            double SysMaxCheck(double price)
            {
                if (price > TbseSysConsts.TBSE_SYS_MAX_PRICE)
                {
                    Console.WriteLine("WARNING: price > bse_sys_max -- clipped");
                    price = TbseSysConsts.TBSE_SYS_MAX_PRICE;
                }
                return price;
            }

            double GetOrderPrice(int i, List<object> schedule, double scheduleEnd, int n, string stepmode, double timeOfIssue)
            {
                double offsetMin = 0.0, offsetMax = 0.0;

                if (Config.UseInputFile)
                {
                    // Assuming there's a dynamic offset function setup if the file is used
                    // Implementing as per required
                }

                var firstRange = (Tuple<int, int>)schedule[0];
                double pMin = SysMinCheck(offsetMin + Math.Min(firstRange.Item1, firstRange.Item2));
                double pMax = SysMaxCheck(offsetMax + Math.Max(firstRange.Item1, firstRange.Item2));
                double pRange = pMax - pMin;
                double stepSize = pRange / (n - 1);
                double halfStep = Math.Round(stepSize / 2.0);

                double newOrderPrice;
                if (stepmode == "fixed")
                {
                    newOrderPrice = pMin + i * stepSize;
                }
                else if (stepmode == "jittered")
                {
                    newOrderPrice = pMin + i * stepSize + new Random().Next((int)-halfStep, (int)halfStep);
                }
                else if (stepmode == "random")
                {
                    if (schedule.Count > 1)
                    {
                        int s = new Random().Next(0, schedule.Count);
                        var selectedRange = (Tuple<int, int>)schedule[s];
                        pMin = SysMinCheck(Math.Min(selectedRange.Item1, selectedRange.Item2));
                        pMax = SysMaxCheck(Math.Max(selectedRange.Item1, selectedRange.Item2));
                    }
                    newOrderPrice = new Random().Next((int)pMin, (int)pMax);
                }
                else
                {
                    throw new Exception("ERROR: Unknown stepmode in schedule");
                }
                newOrderPrice = SysMinCheck(SysMaxCheck(newOrderPrice));
                return newOrderPrice;
            }

            List<double> GetIssueTimes(int nTraders, string stepmode, Int32 interval, bool shuffle, bool fitToInterval)
            {
                interval = interval;
                if (nTraders < 1)
                {
                    throw new Exception("FAIL: n_traders < 1 in GetIssueTimes()");
                }

                double tStep;
                if (nTraders == 1)
                {
                    tStep = interval;
                }
                else
                {
                    tStep = interval / (nTraders - 1);
                }

                double arrTime = 0;
                List<double> orderIssueTimes = new List<double>();
                for (int i = 0; i < nTraders; i++)
                {
                    if (stepmode == "periodic")
                    {
                        arrTime = interval;
                    }
                    else if (stepmode == "drip-fixed")
                    {
                        arrTime = i * tStep;
                    }
                    else if (stepmode == "drip-jitter")
                    {
                        arrTime = i * tStep + tStep * new Random().NextDouble();
                    }
                    else if (stepmode == "drip-poisson")
                    {
                        double interArrivalTime = new Random().NextDouble() * (nTraders / interval);
                        arrTime += interArrivalTime;
                    }
                    else
                    {
                        throw new Exception("FAIL: unknown t-stepmode in GetIssueTimes()");
                    }
                    orderIssueTimes.Add(arrTime);
                }

                if (fitToInterval && (arrTime > interval || arrTime < interval))
                {
                    for (int i = 0; i < nTraders; i++)
                    {
                        orderIssueTimes[i] = interval * (orderIssueTimes[i] / arrTime);
                    }
                }

                if (shuffle)
                {
                    Random rng = new Random();
                    orderIssueTimes = orderIssueTimes.OrderBy(a => rng.Next()).ToList();
                }

                return orderIssueTimes;
            }

            (List<object>, string, double) GetSchedMode(double currTime, List<Dictionary<string,object>> orderSchedule)
            {
                List<object> schedRange = null;
                string stepmode = null;
                double schedEndTime = 0;
                bool gotOne = false;
                foreach (var schedule in orderSchedule)
                {
                    if ((int)schedule["from"] <= currTime && currTime < (int)schedule["to"])
                    {
                        schedRange = (List<object>) schedule["ranges"];
                        stepmode = (string)schedule["stepmode"];
                        schedEndTime = (int)schedule["to"];
                        gotOne = true;
                        break;
                    }
                }
                if (!gotOne)
                {
                    throw new Exception($"Fail: t={currTime:5.2f} not within any timezone in order_schedule={orderSchedule}");
                }
                return (schedRange, stepmode, schedEndTime);
            }
            int nBuyers = traderStats.Item1;
            int nSellers = traderStats.Item2;
            bool shuffleTimes = true;
            List<string> cancellations = new List<string>();

            List<Order> newPending;
            if (pending.Count < 1)
            {
                newPending = new List<Order>();
                var issueTimes = GetIssueTimes(nBuyers, (string)orderSched["timemode"], (int)orderSched["interval"], shuffleTimes, true);
                string orderType = "Bid";
                var (sched, mode, schedEnd) = GetSchedMode(time, (List<Dictionary<string, object>>)orderSched["dem"]);
                for (int t = 0; t < nBuyers; t++)
                {
                    double issueTime = time + issueTimes[t];
                    string tName = $"B{t:D2}";
                    double orderPrice = GetOrderPrice(t, sched, schedEnd, nBuyers, mode, issueTime);
                    var order = new Order(tName, orderType, (int)orderPrice, 1, issueTime, coid, -3);
                    newPending.Add(order);
                    coid++;
                }

                issueTimes = GetIssueTimes(nSellers, (string)orderSched["timemode"], (int)orderSched["interval"], shuffleTimes, true);
                orderType = "Ask";
                (sched, mode, schedEnd) = GetSchedMode(time, (List<Dictionary<string, object>>)orderSched["sup"]);
                for (int t = 0; t < nSellers; t++)
                {
                    double issueTime = time + issueTimes[t];
                    string tName = $"S{t:D2}";
                    double orderPrice = GetOrderPrice(t, sched, schedEnd, nSellers, mode, issueTime);
                    var order = new Order(tName, orderType, (int)orderPrice, 1, issueTime, coid, -3);
                    newPending.Add(order);
                    coid++;
                }
            }
            else
            {
                newPending = new List<Order>();
                foreach (var order in pending)
                {
                    if (order.Time < time)
                    {
                        string tName = order.Tid;
                        var response = traders[tName].AddOrder(order, verbose);
                        if (verbose)
                        {
                            Console.WriteLine($"Customer order: {response} {order}");
                        }
                        if (response == "LOB_Cancel")
                        {
                            cancellations.Add(tName);
                            if (verbose)
                            {
                                Console.WriteLine($"Cancellations: {cancellations}");
                            }
                        }
                    }
                    else
                    {
                        newPending.Add(order);
                    }
                }
            }
            return (newPending, cancellations, coid);
        }
    }
}
