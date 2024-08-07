using System;
using System.Collections.Concurrent;
using System.Collections.Generic; // For List and other collections
using System.Diagnostics;
using System.Linq;
using System.Text; // For StringBuilder and other text operations
using BFBSE;
using BFBSE.tbse_exchange;
using BFBSE.tbse_msg_classes;
using Microsoft.VisualBasic;
// Assuming Order class and TBSE_SYS constants are defined in separate files
// using tbse_msg_classes;
// using tbse_sys_consts;


namespace BFBSE
{
    public class Trader
    {
        // Trader superclass - mostly unchanged from original BSE code by Dave Cliff
        // All Traders have a trader id, bank balance, blotter, and list of orders to execute

        public string TType { get; private set; }  // what type / strategy this trader is
        public string TID { get;  set; }    // trader unique ID code
        public double Balance { get; private set; }  // money in the bank
        public List<Dictionary<string, dynamic>> Blotter { get; private set; }  // record of trades executed
        public ConcurrentDictionary<int, Order> Orders { get; private set; }  // customer orders currently being worked (fixed at 1)
        public int NQuotes { get;  set; }  // number of quotes live on LOB
        public int Willing { get; private set; }  // used in ZIP etc
        public int Able { get; private set; }  // used in ZIP etc
        public double BirthTime { get; private set; }  // used when calculating age of a trader/strategy
        public double ProfitPerTime { get; private set; }  // profit per unit time
        public int NTrades { get;  set; }  // how many trades has this trader done?
        public Order LastQuote { get;  set; }  // record of what its last quote was
        public List<double> Times { get; private set; }  // values used to calculate timing elements

        public Trader(string ttype, string tid, double balance, double time)
        {
            TType = ttype;
            TID = tid;
            Balance = balance;
            Blotter = new List<Dictionary<string, dynamic>>();
            Orders = new ConcurrentDictionary<int, Order>();
            NQuotes = 0;
            Willing = 1;
            Able = 1;
            BirthTime = time;
            ProfitPerTime = 0;
            NTrades = 0;
            LastQuote = null;
            Times = new List<double> { 0, 0, 0, 0 };
        }

        public override string ToString()
        {
            return $"[TID {TID} type {TType} balance {Balance} blotter {string.Join(", ", Blotter)} " +
                   $"orders {Orders} n_trades {NTrades} profit_per_time {ProfitPerTime}]";
        }

        public string AddOrder(Order order, bool verbose)
        {
            // Adds an order to the traders list of orders
            // in this version, trader has at most one order,
            // if allow more than one, this needs to be self.orders.append(order)
            // :param order: the order to be added
            // :param verbose: should verbose logging be printed to console
            // :return: Response: "Proceed" if no current offer on LOB, "LOB_Cancel" if there is an order on the LOB needing
            //          cancelled.

            string response;
            if (NQuotes > 0)
            {
                // this trader has a live quote on the LOB, from a previous customer order
                // need response to signal cancellation/withdrawal of that quote
                response = "LOB_Cancel";
            }
            else
            {
                response = "Proceed";
            }
            this.Orders[order.Coid] = order;

            if (verbose)
            {
                Console.WriteLine($"add_order < response={response}");
            }
            return response;
        }

        public void DelOrder(int coid)
        {
            // Removes current order from traders list of orders
            // :param coid: Customer order ID of order to be deleted
            Orders.TryRemove(coid, out _);
        }

        public void Bookkeep(Dictionary<string, dynamic> trade, Order order, bool verbose, double time)
        {
            // Updates trader's internal stats with trade and order
            // :param trade: Trade that has been executed
            // :param order: Order trade was in response to
            // :param verbose: Should verbose logging be printed to console
            // :param time: Current time

            string outputString = "";

            int coid;
            double orderPrice;
            if (Orders.ContainsKey(trade["coid"]))
            {
                coid = trade["coid"];
                orderPrice = Orders[coid].Price;
            }
            else if (Orders.ContainsKey(trade["counter"]))
            {
                coid = trade["counter"];
                orderPrice = Orders[coid].Price;
            }
            else
            {
                Console.WriteLine("COID not found");
                Environment.Exit(1);
                return;
            }

            Blotter.Add(trade);  // add trade record to trader's blotter
            double transactionPrice = (double)trade["price"];
            double profit = Orders[coid].Otype == "Bid" ? orderPrice - transactionPrice : transactionPrice - orderPrice;
            this.Balance += profit;
            Console.WriteLine(this.GetType());
            Console.WriteLine(this.Balance);
            this.NTrades++;
            Console.WriteLine("ntrades "+NTrades);
            ProfitPerTime = Balance / (time - BirthTime);

            if (profit < 0)
            {
                Console.WriteLine(profit);
                Console.WriteLine(trade);
                Console.WriteLine(order);
                Console.WriteLine($"{trade["COID"]} {trade["Counter"]} {order.Coid} {Orders[0].Coid}");
                Environment.Exit(1);
            }

            if (verbose)
            {
                Console.WriteLine($"{outputString} profit={profit} balance={Balance} profit/t={ProfitPerTime}");
            }
            DelOrder(coid);  // delete the order
        }

        // Specify how trader responds to events in the market
        // This is a null action, expect it to be overloaded by specific algos
        // :param time: Current time
        // :param lob: Limit order book
        // :param trade: Trade being responded to
        // :param verbose: Should verbose logging be printed to console
        // :return: Unused
        public virtual void Respond(double time, double pEq, double qEq,  List<(double Price, int Qty)> demandCurve, List<(double Price, int Qty)> supplyCurve, dynamic lob, List<object> trades, bool verbose)
        {
            // Do nothing (this method is intended to be overridden by subclasses)
        }

        // Get the trader's order based on the current state of the market
        // :param time: Current time
        // :param countdown: Time to end of session
        // :param lob: Limit order book
        // :return: The order
        public virtual Order GetOrder(double time, double pEq, double qEq,  List<(double Price, int Qty)> demandCurve, List<(double Price, int Qty)> supplyCurve, double countdown, dynamic lob)
        {
            // Do nothing (this method is intended to be overridden by subclasses)
            return null;
        }
    }
    public class TraderGiveaway : Trader
    {
        // Trader subclass Giveaway
        // even dumber than a ZI-U: just give the deal away
        // (but never makes a loss)

        public TraderGiveaway(string ttype, string tid, double balance, double time)
            : base(ttype, tid, balance, time)
        {
        }

        public override Order GetOrder(double time, double pEq, double qEq,  List<(double Price, int Qty)> demandCurve,  List<(double Price, int Qty)> supplyCurve, double countdown,  object lob)
        {
            // Get's giveaway traders order - in this case the price is just the limit price from the customer order
            // :param time: Current time
            // :param countdown: Time until end of session
            // :param lob: Limit order book
            // :return: Order to be sent to the exchange

            if (Orders.Count < 1)
            {
                return null;
            }
            else
            {
                var coid = Orders.Keys.Max();
                var quotePrice = Orders[coid].Price;
                var order = new Order
                (
                    TID,
                    Orders[coid].Otype,
                    quotePrice,
                    Orders[coid].Qty,
                    time,
                    Orders[coid].Coid,
                    Orders[coid].Toid
                );
                LastQuote = order;
                //Console.WriteLine($"Trader {TID} of {Orders[coid].OType} has orders with limit prices {string.Join(", ", Orders.Values.Select(o => o.Price))} at time {time}");
                return order;
            }
        }
    }
    public class TraderZic : Trader
    {
        // Trader subclass ZI-C
        // After Gode & Sunder 1993

        private static readonly Random Random = new Random();

        public TraderZic(string ttype, string tid, double balance, double time)
            : base(ttype, tid, balance, time)
        {
        }

        private static readonly object ordersLock = new object();
        public override Order GetOrder(double time, double pEq, double qEq, List<(double Price, int Qty)> demandCurve, List<(double Price, int Qty)> supplyCurve, double countdown, dynamic lob)
{
    // Gets ZIC trader, limit price is randomly selected
    if (Orders.Count < 1)
    {
        //Console.WriteLine("orders empty-ZIC");
        return null;
    }

    // Create a local snapshot of the keys to avoid collection modification issues
    var orderKeys = Orders.Keys.ToList();
    var coid = orderKeys.Max();

    var minPriceLob = lob["bids"]["worst"];
    var maxPriceLob = lob["asks"]["worst"];
    var limit = Orders[coid].Price;
    var otype = Orders[coid].Otype;

    var minPrice = minPriceLob;
    var maxPrice = maxPriceLob;

    double quotePrice;
    if (otype == "Bid")
    {
        if (minPrice > limit)
        {
            minPrice = limit;
        }
        quotePrice = Random.Next((int)minPrice, (int)limit + 1); // Include limit in range
    }
    else if (otype == "Ask")
    {
        if (maxPrice < limit)
        {
            maxPrice = limit;
        }
        quotePrice = Random.Next((int)limit, (int)maxPrice + 1); // Include limit in range
    }
    else
    {
        throw new InvalidOperationException("Order type must be either 'Bid' or 'Ask'.");
    }

    var order = new Order
    (
        TID,
        Orders[coid].Otype,
        (int)quotePrice,
        Orders[coid].Qty,
        time,
        Orders[coid].Coid,
        Orders[coid].Toid
    );
    LastQuote = order;
    //Console.WriteLine("order_ZIC"+order.ToString());
    return order;

}
    }
    public class TraderShaver : Trader
    {
        // Trader subclass Shaver
        // shaves a penny off the best price
        // if there is no best price, creates "stub quote" at system max/min

        public TraderShaver(string ttype, string tid, double balance, double time)
            : base(ttype, tid, balance, time)
        {
        }

        public override Order GetOrder(double time, double pEq, double qEq,  List<(double Price, int Qty)> demandCurve,  List<(double Price, int Qty)> supplyCurve, double countdown, dynamic lob)
        {
            // Get's Shaver trader order by shaving/adding a penny to current best bid
            // :param time: Current time
            // :param countdown: Countdown to end of market session
            // :param lob: Limit order book
            // :return: The trader order to be sent to the exchange

            if (Orders.Count < 1)
            {
                return null;
            }

            var coid = Orders.Keys.Max();
            var limitPrice = Orders[coid].Price;
            var otype = Orders[coid].Otype;

            double bestBid = 500;
            double bestAsk = 0;


            if (demandCurve.Any())
        {
            bestBid = demandCurve.Max(d => d.Item1) + 1;
        }

        if (supplyCurve.Any())
        {
            bestAsk = supplyCurve.Min(s => s.Item1) - 1;
        }
            double quotePrice;
            if (otype == "Bid")
            {
                quotePrice = bestBid;
                quotePrice = Math.Min(quotePrice, limitPrice);
            }
            else if (otype == "Ask")
            {
                quotePrice = bestAsk;
                quotePrice = Math.Max(quotePrice, limitPrice);
            }
            else
            {
                throw new InvalidOperationException("Order type must be either 'Bid' or 'Ask'.");
            }

             var order = new Order
                (
                    TID,
                    Orders[coid].Otype,
                    (int)quotePrice,
                    Orders[coid].Qty,
                    time,
                    Orders[coid].Coid,
                    Orders[coid].Toid
                );
            LastQuote = order;

            return order;
        }
    }
    
    public class TraderSniper : Trader
    {
        // Trader subclass Sniper
        // Based on Shaver,
        // "lurks" until t remaining < threshold% of the trading session
        // then gets increasingly aggressive, increasing "shave thickness" as t runs out

        public TraderSniper(string ttype, string tid, double balance, double time)
            : base(ttype, tid, balance, time)
        {
        }

        public override Order GetOrder(double time, double pEq, double qEq,   List<(double Price, int Qty)> demandCurve,  List<(double Price, int Qty)> supplyCurve, double countdown, dynamic lob)
        {
            // :param time: Current time
            // :param countdown: Time until end of market session
            // :param lob: Limit order book
            // :return: Trader order to be sent to exchange

            const double lurkThreshold = 0.2;
            const double shaveGrowthRate = 3;
            int shave = (int)(1.0 / (0.01 + countdown / (shaveGrowthRate * lurkThreshold)));

            if (Orders.Count < 1 || countdown > lurkThreshold)
            {
                return null;
            }

            var coid = Orders.Keys.Max();
            var limitPrice = Orders[coid].Price;
            var otype = Orders[coid].Otype;

            double bestBid;
            double bestAsk;

            if (demandCurve != null && supplyCurve != null && demandCurve.Any() && supplyCurve.Any())
            {
                 bestBid = demandCurve.Min(x => x.Item1);
                 bestAsk = supplyCurve.Max(x => x.Item1);
            }
            else
            {
                bestBid = lob["bids"]["worst"] - shave;
                bestAsk = lob["asks"]["worst"] + shave;
            }

            double quotePrice;
            if (otype == "Bid")
            {
                quotePrice = bestBid + shave;
                quotePrice = Math.Min(quotePrice, limitPrice);
            }
            else if (otype == "Ask")
            {
                quotePrice = bestAsk - shave;
                quotePrice = Math.Max(quotePrice, limitPrice);
            }
            else
            {
                throw new InvalidOperationException("Order type must be either 'Bid' or 'Ask'.");
            }

             var order = new Order
                (
                    TID,
                    Orders[coid].Otype,
                    (int)quotePrice,
                    Orders[coid].Qty,
                    time,
                    Orders[coid].Coid,
                    Orders[coid].Toid
                );
            LastQuote = order;

            return order;
        }
    }
    public class TraderZip : Trader
    {
        // ZIP init key param-values are those used in Cliff's 1997 original HP Labs tech report
        // This implementation keeps separate margin values for buying & selling,
        // so a single trader can both buy AND sell -- in the original, traders were either buyers OR sellers

        private static Random random = new Random();

        public double m_fix = 0.05;
        public double m_var = 0.3;
        public string job;
        public bool active;
        public double prev_change;
        public double beta;
        public double momentum;
        public double ca;
        public double cr;
        public double? margin;
        public double margin_buy;
        public double margin_sell;
        public int? price;
        public double? limit;
        public int[] times;
        public double? prev_best_bid_p;
        public int? prev_best_bid_q;
        public double? prev_best_ask_p;
        public int? prev_best_ask_q;
        public object last_batch;

        public TraderZip(string ttype, string tid, double balance, double time)
            : base(ttype, tid, balance, time)
        {
            job = null; // this is 'Bid' or 'Ask' depending on customer order
            active = false; // gets switched to True while actively working an order
            prev_change = 0; // this was called last_d in Cliff'97
            beta = 0.2 + 0.2 * random.NextDouble(); // learning rate
            momentum = 0.3 * random.NextDouble(); // momentum
            ca = 0.10; // self.ca & .cr were hard-coded in '97 but parameterised later
            cr = 0.10;
            margin = null; // this was called profit in Cliff'97
            margin_buy = -1.0 * (m_fix + m_var * random.NextDouble());
            margin_sell = m_fix + m_var * random.NextDouble();
            price = null;
            limit = null;
            times = new int[4];
            // memory of best price & quantity of best bid and ask, on LOB on previous update
            prev_best_bid_p = null;
            prev_best_bid_q = null;
            prev_best_ask_p = null;
            prev_best_ask_q = null;
            last_batch = null;
            //Console.WriteLine("ZIP trader getting initialised");
        }

        public override Order GetOrder(double time, double pEq, double qEq,  List<(double Price, int Qty)> demandCurve, List<(double Price, int Qty)> supplyCurve, double countdown, dynamic lob)
        {
            // Console.WriteLine ("inside function");
            if (Orders.IsEmpty)
            {
                active = false;
                // Console.WriteLine("is empty");
                return null;
            }
            // else
            // {
                // Console.WriteLine("here");
                var coid = Orders.Keys.Max();
                active = true;
                limit = Orders[coid].Price;
                job = Orders[coid].Otype;

                if (job == "Bid")
                {
                    // currently a buyer (working a bid order)
                    margin = margin_buy;
                }
                else
                {
                    // currently a seller (working a sell order)
                    margin = margin_sell;
                }

                int quote_price = (int)(limit * (1 + margin));
                price = quote_price;

                var order = new Order
                (
                    TID,
                    Orders[coid].Otype,
                    quote_price,
                    Orders[coid].Qty,
                    time,
                    Orders[coid].Coid,
                    Orders[coid].Toid
                );
                // Console.WriteLine("inside getorder"+ order);
                LastQuote = order;
                return order;
            // }
        }

        // Update margin on basis of what happened in market
        public override void Respond(double time, double pEq, double qEq, List<(double, int)> demandCurve, List<(double, int)> supplyCurve,  dynamic lob, List<dynamic> trades, bool verbose)
        {
            if (last_batch == Tuple.Create(demandCurve, supplyCurve))
            {
                return;
            }
            else
            {
                last_batch = Tuple.Create(demandCurve, supplyCurve);
            }

            var trade = trades.FirstOrDefault();

            double? bestBid = lob["bids"]["best"];
            double? bestAsk = lob["asks"]["best"];
            if (demandCurve.Count > 0)
            {
                bestBid = demandCurve.Max(x => x.Item1);
            }

            if (supplyCurve.Count > 0)
            {
                bestAsk = supplyCurve.Min(x => x.Item1);
            }
            

            double TargetUp(double price)
            {
                double ptrb_abs = ca * random.NextDouble(); // absolute shift
                double ptrb_rel = price * (1.0 + (cr * random.NextDouble())); // relative shift
                return Math.Round(ptrb_rel + ptrb_abs);
            }

            double TargetDown(double price)
            {
                double ptrb_abs = ca * random.NextDouble(); // absolute shift
                double ptrb_rel = price * (1.0 - (cr * random.NextDouble())); // relative shift
                return Math.Round(ptrb_rel - ptrb_abs);
            }

            bool WillingToTrade(double price)
            {
                bool willing = false;
                if (job == "Bid" && active && this.price >= price)
                {
                    willing = true;
                }
                if (job == "Ask" && active && this.price <= price)
                {
                    willing = true;
                }
                return willing;
            }

            void ProfitAlter(double price)
            {
                double old_price = (double)this.price;
                double diff = price - old_price;
                double change = ((1.0 - momentum) * (beta * diff)) + (momentum * prev_change);
                prev_change = change;
                double new_margin = (double)(((this.price + change) / limit.Value) - 1.0);

                if (job == "Bid")
                {
                    if (new_margin < 0.0)
                    {
                        margin_buy = new_margin;
                        margin = new_margin;
                    }
                }
                else
                {
                    if (new_margin > 0.0)
                    {
                        margin_sell = new_margin;
                        margin = new_margin;
                    }
                }

                // set the price from limit and profit-margin
                this.price = (int)Math.Round(limit.Value * (1.0 + margin.Value));
            }

            bool bid_improved = false;
            bool bid_hit = false;

            double? lob_best_bid_p = bestBid;
            int? lob_best_bid_q = null;

            if (lob_best_bid_p != null)
            {
                lob_best_bid_q = 1;
                if (prev_best_bid_p == null)
                {
                    prev_best_bid_p = lob_best_bid_p;
                }
                else if (prev_best_bid_p < lob_best_bid_p)
                {
                    bid_improved = true;
                }
                else if (trade != null && ((prev_best_bid_p > lob_best_bid_p) || ((prev_best_bid_p == lob_best_bid_p) && (prev_best_bid_q > lob_best_bid_q))))
                {
                    bid_hit = true;
                }
            }
            else if (prev_best_bid_p != null)
            {
                var tapeList = (List<Dictionary<string, object>>)lob["tape"];
                dynamic last_tape_item = tapeList.Count > 0 ? tapeList[tapeList.Count - 1] : null;
                if (last_tape_item != null && last_tape_item["type"] == "Cancel")
                {
                    bid_hit = false;
                }
                else
                {
                    bid_hit = true;
                }
            }

            bool ask_improved = false;
            bool ask_lifted = false;

            double? lob_best_ask_p = bestAsk;
            int? lob_best_ask_q = null;

            if (lob_best_ask_p != null)
            {
                lob_best_ask_q = 1;
                if (prev_best_ask_p == null)
                {
                    prev_best_ask_p = lob_best_ask_p;
                }
                else if (prev_best_ask_p > lob_best_ask_p)
                {
                    ask_improved = true;
                }
                else if (trade != null && ((prev_best_ask_p < lob_best_ask_p) || ((prev_best_ask_p == lob_best_ask_p) && (prev_best_ask_q > lob_best_ask_q))))
                {
                    ask_lifted = true;
                }
            }
            else if (prev_best_ask_p != null)
            {
                //Console.WriteLine("tape"+lob["tape"]);
                var tapeList = (List<Dictionary<string, object>>)lob["tape"];
                dynamic last_tape_item = tapeList.Count > 0 ? tapeList[tapeList.Count - 1] : null;
                if (last_tape_item != null && last_tape_item["type"] == "Cancel")
                {
                    ask_lifted = false;
                }
                else
                {
                    ask_lifted = true;
                }
            }

            if (verbose && (bid_improved || bid_hit || ask_improved || ask_lifted))
            {
                Console.WriteLine($"B_improved: {bid_improved}, B_hit: {bid_hit}, A_improved: {ask_improved}, A_lifted: {ask_lifted}");
            }

            bool deal = bid_hit || ask_lifted;

            if (trade == null)
            {
                deal = false;
            }

            if (job == "Ask")
            {
                // seller
                if (deal)
                {
                    double trade_price = (double)trade["price"];
                    if (this.price <= trade_price)
                    {
                        double target_price = TargetUp(trade_price);
                        ProfitAlter(target_price);
                    }
                    else if (ask_lifted && active && !WillingToTrade(trade_price))
                    {
                        double target_price = TargetDown(trade_price);
                        ProfitAlter(target_price);
                    }
                }
                else
                {
                    if (ask_improved && this.price > lob_best_ask_p)
                    {
                        double target_price = lob_best_bid_p.HasValue ? TargetUp(lob_best_bid_p.Value) : lob["asks"]["worst"];
                        ProfitAlter(target_price);
                    }
                }
            }

            if (job == "Bid")
            {
                // buyer
                if (deal)
                {
                    double trade_price = (double)trade["price"];
                    if (this.price >= trade_price)
                    {
                        double target_price = TargetDown(trade_price);
                        ProfitAlter(target_price);
                    }
                    else if (bid_hit && active && !WillingToTrade(trade_price))
                    {
                        double target_price = TargetUp(trade_price);
                        ProfitAlter(target_price);
                    }
                }
                else
                {
                    if (bid_improved && this.price < lob_best_bid_p)
                    {
                        double target_price = lob_best_ask_p.HasValue ? TargetDown(lob_best_ask_p.Value) : lob["bids"]["worst"];
                        ProfitAlter(target_price);
                    }
                }
            }

            prev_best_bid_p = lob_best_bid_p;
            prev_best_bid_q = lob_best_bid_q;
            prev_best_ask_p = lob_best_ask_p;
            prev_best_ask_q = lob_best_ask_q;
        }
    }
    public class TraderAa : Trader
    {
        // Learning variables
        private double rShoutChangeRelative = 0.05;
        private double rShoutChangeAbsolute = 0.05;
        private double shortTermLearningRate;
        private double longTermLearningRate;
        private double movingAverageWeightDecay = 0.95;
        private int movingAverageWindowSize = 5;
        private double offerChangeRate = 3.0;
        private double theta = -2.0;
        private double thetaMax = 2.0;
        private double thetaMin = -8.0;
        private double marketMax = TbseSysConsts.TBSE_SYS_MAX_PRICE;
        private Boolean active;

        // Variables to describe the market
        private List<double> previousTransactions = new List<double>();
        private List<double> movingAverageWeights = new List<double>();
        private List<double> estimatedEquilibrium = new List<double>();
        private List<double> smithsAlpha = new List<double>();
        private double? prevBestBidP = null;
        private double? prevBestBidQ = null;
        private double? prevBestAskP = null;
        private double? prevBestAskQ = null;

        // Trading variables
        private double? rShout = null;
        private double? buyTarget = null;
        private double? sellTarget = null;
        private double buyR;
        private double sellR;
        private double? limit = null;
        private string job = null;

        // Define last batch so that internal values are only updated upon new batch matching
        private Tuple<List<(double, int)>, List<(double, int)>> lastBatch = null;

        public TraderAa(string ttype, string tid, double balance, double time)
            : base(ttype, tid, balance, time)
        {
            Random random = new Random();
            this.active = false;
            this.buyR = -1.0 * (0.3 * random.NextDouble());
            this.sellR = -1.0 * (0.3 * random.NextDouble());
            this.shortTermLearningRate = random.NextDouble() * 0.4 + 0.1;
            this.longTermLearningRate = random.NextDouble() * 0.4 + 0.1;

            // Initialize moving average weights
            for (int i = 0; i < this.movingAverageWindowSize; i++)
            {
                this.movingAverageWeights.Add(Math.Pow(this.movingAverageWeightDecay, i));
            }
        }

        public override Order GetOrder(double time, double pEq, double qEq,  List<(double Price, int Qty)> demandCurve, List<(double Price, int Qty)> supplyCurve, double countdown, dynamic lob)
        {
              //Console.WriteLine("here-AA"); it prints
            if (Orders.Count < 1)
            {
                Console.WriteLine("order empty-AA");
                this.active = false;
                return null;
            }

            var coid = Orders.Keys.Max();
            this.active = true;
            this.limit = Orders[coid].Price;
            this.job = Orders[coid].Otype;
            this.CalcTarget(demandCurve, supplyCurve);

            double quotePrice = TbseSysConsts.TBSE_SYS_MIN_PRICE;
            double oBid = this.prevBestBidP ?? 0;
            double oAsk = this.prevBestAskP ?? this.marketMax;

            if (this.job == "Bid") // Buyer
            {
                if (this.limit <= oBid)
                    return null;

                if (this.previousTransactions.Any())
                {
                    double oAskPlus = (1 + this.rShoutChangeRelative) * oAsk + this.rShoutChangeAbsolute;
                    quotePrice = oBid + ((Math.Min((double)this.limit, oAskPlus) - oBid) / this.offerChangeRate);
                }
                else
                {
                    if (oAsk <= this.buyTarget)
                        quotePrice = oAsk;
                    else
                        quotePrice = (double)(oBid + ((this.buyTarget - oBid) / this.offerChangeRate));
                }
            }
            else if (this.job == "Ask") // Seller
            {
                if (this.limit >= oAsk)
                    return null;

                if (this.previousTransactions.Any())
                {
                    double oBidMinus = (1 - this.rShoutChangeRelative) * oBid - this.rShoutChangeAbsolute;
                    quotePrice = oAsk - ((oAsk - Math.Max((double)this.limit, oBidMinus)) / this.offerChangeRate);
                }
                else
                {
                    if (oBid >= this.sellTarget)
                        quotePrice = oBid;
                    else
                        quotePrice = (double)(oAsk - ((oAsk - this.sellTarget) / this.offerChangeRate));
                }
            }

            var order = new Order
                (
                    TID,
                    Orders[coid].Otype,
                    (int)quotePrice,
                    Orders[coid].Qty,
                    time,
                    Orders[coid].Coid,
                    Orders[coid].Toid
                );
             Console.WriteLine("order_AA"+order.ToString());
            this.LastQuote = order;
            return order;
        }

        public override void Respond(double time, double pEq, double qEq, List<(double, int)> demandCurve, List<(double, int)> supplyCurve,  dynamic lob, List<dynamic> trades, bool verbose)
        {
            if (this.lastBatch != null && this.lastBatch.Item1.SequenceEqual(demandCurve) && this.lastBatch.Item2.SequenceEqual(supplyCurve))
                return;

            this.lastBatch = new Tuple<List<(double, int)>, List<(double, int)>>(demandCurve, supplyCurve);

            dynamic trade = trades.FirstOrDefault();

            double? bestBid = lob["bids"]["best"];
            double? bestAsk = lob["asks"]["best"];
            if (demandCurve.Count != 0)
        {
            bestBid = demandCurve.Max(d => d.Item1);
        }

        if (supplyCurve.Count != 0)
        {
            bestAsk = supplyCurve.Min(s => s.Item1);
        }

            bool bidHit = false;
            double? lobBestBidP = bestBid;
            double? lobBestBidQ = null;

            if (lobBestBidP != null)
            {
                lobBestBidQ = 1;
                if (this.prevBestBidP == null)
                {
                    this.prevBestBidP = lobBestBidP;
                }
                else if (trade != null && ((this.prevBestBidP > lobBestBidP) || ((this.prevBestBidP == lobBestBidP) && (this.prevBestBidQ > lobBestBidQ))))
                {
                    bidHit = true;
                }
            }
            else if (this.prevBestBidP != null)
            {
                var tapeList = (List<Dictionary<string, object>>)lob["tape"];
                dynamic lastTapeItem = tapeList.Count > 0 ? tapeList[tapeList.Count - 1] : null;
                if (lastTapeItem != null && lastTapeItem["type"] == "Cancel")
                {
                    bidHit = false;
                }
                else
                {
                    bidHit = true;
                }
            }

            bool askLifted = false;
            double? lobBestAskP = bestAsk;
            double? lobBestAskQ = null;

            if (lobBestAskP != null)
            {
                lobBestAskQ = 1;
                if (this.prevBestAskP == null)
                {
                    this.prevBestAskP = lobBestAskP;
                }
                else if (trade != null && ((this.prevBestAskP < lobBestAskP) || ((this.prevBestAskP == lobBestAskP) && (this.prevBestAskQ > lobBestAskQ))))
                {
                    askLifted = true;
                }
            }
            else if (this.prevBestAskP != null)
            {
                var tapeList = (List<Dictionary<string, object>>)lob["tape"];
                dynamic lastTapeItem = tapeList.Count > 0 ? tapeList[tapeList.Count - 1] : null;

                if (lastTapeItem != null && lastTapeItem["type"] == "Cancel")
                {
                    askLifted = false;
                }
                else
                {
                    askLifted = true;
                }
            }

            this.prevBestBidP = lobBestBidP;
            this.prevBestBidQ = lobBestBidQ;
            this.prevBestAskP = lobBestAskP;
            this.prevBestAskQ = lobBestAskQ;

            bool deal = bidHit || askLifted;
            if (trades.Count == 0)
            {
                deal = false;
            }

            if (deal)
            {
                if (trade != null)
                {
                    this.previousTransactions.Add(trade["price"]);
                    if (this.sellTarget == null)
                    {
                        this.sellTarget = trade["price"];
                    }
                    if (this.buyTarget == null)
                    {
                        this.buyTarget = trade["price"];
                    }
                    this.CalcEq();
                    this.CalcAlpha();
                    this.CalcTheta();
                    this.CalcRShout();
                    this.CalcAgg();
                    this.CalcTarget(demandCurve, supplyCurve);
                }
            }
        }

        private void CalcEq()
        {
            if (this.previousTransactions.Count == 0)
                return;

            if (this.previousTransactions.Count < this.movingAverageWindowSize)
            {
                double sum = this.previousTransactions.Sum();
                this.estimatedEquilibrium.Add(sum / this.previousTransactions.Count);
            }
            else
            {
                List<double> nPreviousTransactions = this.previousTransactions.Skip(this.previousTransactions.Count - this.movingAverageWindowSize).ToList();
                List<double> weightedTransactions = nPreviousTransactions.Select((value, index) => value * this.movingAverageWeights[index]).ToList();
                double eq = weightedTransactions.Sum() / this.movingAverageWeights.Sum();
                this.estimatedEquilibrium.Add(eq);
            }
        }

        private void CalcAlpha()
        {
            double alpha = 0.0;
            foreach (var p in this.estimatedEquilibrium)
            {
                alpha += Math.Pow(p - this.estimatedEquilibrium.Last(), 2);
                alpha = Math.Sqrt(alpha / this.estimatedEquilibrium.Count);
                this.smithsAlpha.Add(alpha / this.estimatedEquilibrium.Last());
            }
        }

        private void CalcTheta()
        {
            double gamma = 2.0;

            if (this.smithsAlpha.Min() == this.smithsAlpha.Max())
            {
                // Alpha range
                double alphaRange = 0.4;
                this.theta = this.thetaMin + (this.thetaMax - this.thetaMin) * (1 - (alphaRange * Math.Exp(gamma * (alphaRange - 1))));
            }
            else
            {
                double alphaRange = (this.smithsAlpha.Last() - this.smithsAlpha.Min()) / (this.smithsAlpha.Max() - this.smithsAlpha.Min());
                double desiredTheta = this.thetaMin + (this.thetaMax - this.thetaMin) * (1 - (alphaRange * Math.Exp(gamma * (alphaRange - 1))));
                this.theta += this.longTermLearningRate * (desiredTheta - this.theta);
            }
        }

        private void CalcRShout()
        {
            double p = this.estimatedEquilibrium.Any() ? this.estimatedEquilibrium.LastOrDefault() : 0.0;
            double lim = this.limit ?? 0.0;
            double theta = this.theta;

            if (this.job == "Bid")
            {
                // Currently a buyer
                if (lim <= p) // Extra-marginal
                {
                    this.rShout = 0.0;
                }
                else // Intra-marginal
                {
                    if (this.buyTarget > this.estimatedEquilibrium.LastOrDefault())
                    {
                        this.rShout = Math.Log((double)(((this.buyTarget - p) * (Math.Exp(theta) - 1) / (lim - p)) + 1)) / theta;
                    }
                    else
                    {
                        this.rShout = Math.Log((double)((1 - (this.buyTarget / p)) * (Math.Exp(theta) - 1) + 1)) / theta;
                    }
                }
            }

            if (this.job == "Ask")
            {
                // Currently a seller
                if (lim >= p) // Extra-marginal
                {
                    this.rShout = 0;
                }
                else // Intra-marginal
                {
                    if (this.sellTarget > this.estimatedEquilibrium.LastOrDefault())
                    {
                        double a = (double)((this.sellTarget - lim) / (p - lim));
                        this.rShout = (Math.Log((1 - a) * (Math.Exp(theta) - 1) + 1)) / theta;
                    }
                    else
                    {
                        this.rShout = Math.Log((double)((this.sellTarget - p) * (Math.Exp(theta) - 1) / (this.marketMax - p) + 1)) / theta;
                    }
                }
            }
        }

        private void CalcAgg()
        {
            if (this.job == "Bid")
            {
                // Buyer
                if (this.buyTarget >= this.previousTransactions.LastOrDefault())
                {
                    // Must be more aggressive
                    double delta = (1 + this.rShoutChangeRelative) * this.rShout.Value + this.rShoutChangeAbsolute;
                    this.buyR = this.buyR + this.shortTermLearningRate * (delta - this.buyR);
                }
                else
                {
                    double delta = (1 - this.rShoutChangeRelative) * this.rShout.Value - this.rShoutChangeAbsolute;
                    this.buyR = this.buyR + this.shortTermLearningRate * (delta - this.buyR);
                }
            }

            if (this.job == "Ask")
            {
                // Seller
                if (this.sellTarget > this.previousTransactions.LastOrDefault())
                {
                    double delta = (1 + this.rShoutChangeRelative) * this.rShout.Value + this.rShoutChangeAbsolute;
                    this.sellR = this.sellR + this.shortTermLearningRate * (delta - this.sellR);
                }
                else
                {
                    double delta = (1 - this.rShoutChangeRelative) * this.rShout.Value - this.rShoutChangeAbsolute;
                    this.sellR = this.sellR + this.shortTermLearningRate * (delta - this.sellR);
                }
            }
        }

        private void CalcTarget( List<(double, int)> demandCurve, List<(double, int)> supplyCurve)
        {
            double p = 1;
            if (this.estimatedEquilibrium.Any())
            {
                p = this.estimatedEquilibrium.Last();
                if (this.limit == p)
                {
                    p = p * 1.000001; // To prevent theta_bar = 0
                }
            }
            else if (this.job == "Bid")
            {
                p = (double)(this.limit - this.limit * 0.2); // Initial guess for equilibrium if no deals yet
            }
            else if (this.job == "Ask")
            {
                p = (double)(this.limit + this.limit * 0.2);
            }

            double lim = this.limit ?? 0.0;
            double theta = this.theta;

            if (this.job == "Bid")
            {
                // Buyer
                double minusThing = (Math.Exp(-this.buyR * theta) - 1) / (Math.Exp(theta) - 1);
                double plusThing = (Math.Exp(this.buyR * theta) - 1) / (Math.Exp(theta) - 1);
                double thetaBar = (theta * lim - theta * p) / p;
                if (thetaBar == 0)
                {
                    thetaBar = 0.0001;
                }
                double barThing = (Math.Exp(-this.buyR * thetaBar) - 1) / (Math.Exp(thetaBar) - 1);
                if (lim <= p) // Extra-marginal
                {
                    if (this.buyR >= 0)
                    {
                        this.buyTarget = lim;
                    }
                    else
                    {
                        this.buyTarget = lim * (1 - minusThing);
                    }
                }
                else // Intra-marginal
                {
                    if (this.buyR >= 0)
                    {
                        this.buyTarget = p + (lim - p) * plusThing;
                    }
                    else
                    {
                        this.buyTarget = p * (1 - barThing);
                    }
                }
                this.buyTarget = Math.Min(this.buyTarget.Value, lim);
            }

            if (this.job == "Ask")
            {
                // Seller
                double minusThing = (Math.Exp(-this.sellR * theta) - 1) / (Math.Exp(theta) - 1);
                double plusThing = (Math.Exp(this.sellR * theta) - 1) / (Math.Exp(theta) - 1);
                double thetaBar = (theta * lim - theta * p) / p;
                if (thetaBar == 0)
                {
                    thetaBar = 0.0001;
                }
                double barThing = (Math.Exp(-this.sellR * thetaBar) - 1) / (Math.Exp(thetaBar) - 1);
                if (lim <= p) // Extra-marginal
                {
                    if (this.buyR >= 0)
                    {
                        this.buyTarget = lim;
                    }
                    else
                    {
                        this.buyTarget = lim + (this.marketMax - lim) * minusThing;
                    }
                }
                else // Intra-marginal
                {
                    if (this.buyR >= 0)
                    {
                        this.buyTarget = lim + (p - lim) * (1 - plusThing);
                    }
                    else
                    {
                        this.buyTarget = p + (this.marketMax - p) * barThing;
                    }
                }
                if (this.sellTarget == null)
                {
                    this.sellTarget = lim;
                }
                else if (this.sellTarget < lim)
                {
                    this.sellTarget = lim;
                }
            }
        }
    }
    public class TraderGdx : Trader
    {
        // Fields
        private List<Order> prevOrders = new List<Order>();
        private string job; // 'Bid' or 'Ask'
        private bool active = false;
        private double? limit = null;
        private List<List<int>> outstandingBids = new List<List<int>>();
        private List<int> outstandingAsks = new List<int>();
        private List<int> acceptedAsks = new List<int>();
        private List<int> acceptedBids = new List<int>();
        private double price = -1;
        private double? prevBestBidP = null;
        private int? prevBestBidQ = null;
        private double? prevBestAskP = null;
        private int? prevBestAskQ = null;
        private bool firstTurn = true;
        private double gamma = 0.9;
        private int holdings = 25;
        private int remainingOfferOps = 25;
        private double[,] values;
        private Tuple<List<(double Price, int Qty)>, List<(double Price, int Qty)>> lastBatch = null;
        // Constructor
        public TraderGdx(string ttype, string tid, double balance, double time) : base(ttype, tid, balance, time)
        {
            values = new double[holdings, remainingOfferOps];
            for (int i = 0; i < holdings; i++)
            {
                for (int j = 0; j < remainingOfferOps; j++)
                {
                    values[i, j] = 0;
                }
            }
        }

        // Methods
        public override Order GetOrder(double time, double pEq, double qEq, List<(double, int)> demandCurve, List<(double, int)> supplyCurve, double countdown, dynamic lob)
        {
            if (Orders.Count < 1)
            {
                active = false;
                return null;
            }
            else
            {
                var coid = Orders.Keys.Max();
                active = true;
                limit = Orders[coid].Price;
                job = Orders[coid].Otype;

                // Calculate price
                if (job == "Bid")
                {
                    price = CalcPBid(holdings - 1, remainingOfferOps - 1);
                }
                else if (job == "Ask")
                {
                    price = CalcPAsk(holdings - 1, remainingOfferOps - 1);
                }

                Order order = new Order(TID, job, Convert.ToInt32(price), Orders[coid].Qty, time, Orders[coid].Coid, Orders[coid].Toid);
                prevOrders.Add(order);
                return order;
            }
        }

        public override void Respond(double time, double pEq, double qEq,  List<(double Price, int Qty)> demandCurve, List<(double Price, int Qty)> supplyCurve, dynamic lob, List<object> trades, bool verbose)
        {   if (this.lastBatch != null && this.lastBatch.Item1.SequenceEqual(demandCurve) && this.lastBatch.Item2.SequenceEqual(supplyCurve))
                return;

            this.lastBatch = new Tuple<List<(double Price, int Qty)>,List<(double Price, int Qty)>>(demandCurve, supplyCurve);


             dynamic trade = trades.FirstOrDefault();

            double? bestBid = lob["bids"]["best"];
            double? bestAsk = lob["asks"]["best"];
            if (demandCurve.Count > 0)
            {
                bestBid = demandCurve.Max(x => x.Item1);
            }

            if (supplyCurve.Count > 0)
            {
                bestAsk = supplyCurve.Min(x => x.Item2);
            }

            // Define a delegate type for the conversion
           // Func<Tuple<string, double>, double> converter = x => Convert.ToDouble(x.Item1);

            // Cast the lambda expression to the delegate type
            outstandingBids = lob["bids"]["lob"];
            prevBestBidP = bestBid;
            prevBestBidQ = lob["bids"]["lob"].Count > 0 ? 1 : (int?)null;

            if (prevBestBidP != null)
            {
                if (prevBestBidQ == null)
                {
                    prevBestBidQ = 1;
                }

                if (prevBestBidP != null && (prevBestBidP > lob["bids"]["best"] || (prevBestBidP == lob["bids"]["best"] && prevBestBidQ > lob["bids"]["lob"].Count)))
                {
                    acceptedBids.AddRange(this.GetBestNBids(demandCurve,qEq));
                }
            }

            // Define a delegate type for the conversion
           //Func<Tuple<string, double>, double> converter2 = x => Convert.ToDouble(x.Item2);


            // Cast the lambda expression to the delegate type
            var outstandingAsks = lob["asks"]["lob"];
            prevBestAskP = bestAsk;
            prevBestAskQ = lob["asks"]["lob"].Count > 0 ? 1 : (int?)null;

            if (prevBestAskP != null)
            {
                if (prevBestAskQ == null)
                {
                    prevBestAskQ = 1;
                }

                if (prevBestAskP != null && (prevBestAskP < lob["asks"]["best"] || (prevBestAskP == lob["asks"]["best"] && prevBestAskQ > lob["asks"]["lob"].Count)))
                {
                    //acceptedAsks.AddRange((IEnumerable<int>)this.GetBestNAsks(supplyCurve,qEq));
                    var bestNAsks = GetBestNAsks(supplyCurve, qEq);

                    // Convert the double values to int and add them to acceptedAsks
                     acceptedAsks.AddRange(bestNAsks.Select(x => (int)x));
                }
            }

            if (firstTurn)
            {
                firstTurn = false;
                for (int n = 1; n < remainingOfferOps; n++)
                {
                    for (int m = 1; m < holdings; m++)
                    {
                        if (job == "Bid")
                        {
                            values[m, n] = CalcPBid(m, n);
                        }
                        else if (job == "Ask")
                        {
                            values[m, n] = CalcPAsk(m, n);
                        }
                    }
                }
            }

            prevBestBidP = bestBid;
            prevBestBidQ = lob["bids"]["lob"].Count > 0 ? 1 : (int?)null;
            prevBestAskP = bestAsk;
            prevBestAskQ = lob["asks"]["lob"].Count > 0 ? 1 : (int?)null;
        }

        private double CalcPBid(int m, int n)
        {
            double bestReturn = 0;
            double bestBid = 0;
            double secondBestBid = 0;

            for (int i = 0; i < Convert.ToInt32(limit / 2); i++)
            {
                double thing = (double)(BeliefBuy(i) * ((limit - i) + gamma * values[m - 1, n - 1]) + (1 - BeliefBuy(i) * gamma * values[m, n - 1]));
                if (thing > bestReturn)
                {
                    secondBestBid = bestBid;
                    bestReturn = thing;
                    bestBid = i;
                }
            }

            if (secondBestBid > bestBid)
            {
                double temp = secondBestBid;
                secondBestBid = bestBid;
                bestBid = temp;
            }

            for (double i = secondBestBid; i < bestBid; i += 0.05)
            {
                double thing = (double)(BeliefBuy(i + secondBestBid) * ((limit - (i + secondBestBid)) + gamma * values[m - 1, n - 1]) + (1 - BeliefBuy(i + secondBestBid) * gamma * values[m, n - 1]));
                if (thing > bestReturn)
                {
                    bestReturn = thing;
                    bestBid = i + secondBestBid;
                }
            }

            return bestBid;
        }

        private double CalcPAsk(int m, int n)
        {
            double bestReturn = 0;
            double bestAsk = (double)limit;
            double secondBestAsk = (double)limit;

            for (int i = 0; i < Convert.ToInt32(limit / 2); i++)
            {
                double j = (double)(i + limit);
                double thing = (double)(BeliefSell(j) * ((j - limit) + gamma * values[m - 1, n - 1]) + (1 - BeliefSell(j) * gamma * values[m, n - 1]));
                if (thing > bestReturn)
                {
                    secondBestAsk = bestAsk;
                    bestReturn = thing;
                    bestAsk = j;
                }
            }

            if (secondBestAsk > bestAsk)
            {
                double temp = secondBestAsk;
                secondBestAsk = bestAsk;
                bestAsk = temp;
            }

            for (double i = secondBestAsk; i < bestAsk; i += 0.05)
            {
                double thing = (double)(BeliefSell(i + secondBestAsk) * ((i + secondBestAsk - limit) + gamma * values[m - 1, n - 1]) + (1 - BeliefSell(i + secondBestAsk) * gamma * values[m, n - 1]));
                if (thing > bestReturn)
                {
                    bestReturn = thing;
                    bestAsk = i + secondBestAsk;
                }
            }

            return bestAsk;
        }

        private double BeliefSell(double price)
        
        {
             var outstandingBidsCopy=outstandingBids;
            try{
            int acceptedAsksGreater = acceptedAsks.Count(p => p >= price);
            
            int bidsGreater = outstandingBidsCopy.Count(bid => bid[0] >= price);
            int unacceptedAsksLower = outstandingAsks.Count(p => p <= price);

            if (acceptedAsksGreater + bidsGreater + unacceptedAsksLower == 0)
            {
                return 0;
            }

            return (double)(acceptedAsksGreater + bidsGreater) / (acceptedAsksGreater + bidsGreater + unacceptedAsksLower);
        }
        catch( Exception ex)
            {
            
            int acceptedAsksGreater = acceptedAsks.Count(p => p >= price);
            
            int bidsGreater = outstandingBidsCopy.Count(bid => bid[0] >= price);
            int unacceptedAsksLower = outstandingAsks.Count(p => p <= price);

            if (acceptedAsksGreater + bidsGreater + unacceptedAsksLower == 0)
            {
                return 0;
            }

            return (double)(acceptedAsksGreater + bidsGreater) / (acceptedAsksGreater + bidsGreater + unacceptedAsksLower);
            }
            
        }

        private double BeliefBuy(double price)
        {
             var outstandingBidsCopy=outstandingBids;
            try{
            int acceptedBidsLower = acceptedBids.Count(p => p <= price);
             
            int asksLower = outstandingAsks.Count(p => p <= price);
            int unacceptedBidsGreater = outstandingBidsCopy.Count(bid => bid[0] >= price);
            if (acceptedBidsLower + asksLower + unacceptedBidsGreater == 0)
            {
                return 0;
            }

            return (double)(acceptedBidsLower + asksLower) / (acceptedBidsLower + asksLower + unacceptedBidsGreater);
            }
            catch( Exception ex)
            {
            int acceptedBidsLower = acceptedBids.Count(p => p <= price);
            
            int asksLower = outstandingAsks.Count(p => p <= price);
            int unacceptedBidsGreater = outstandingBidsCopy.Count(bid => bid[0] >= price);
            if (acceptedBidsLower + asksLower + unacceptedBidsGreater == 0)
            {
                return 0;
            }

            return (double)(acceptedBidsLower + asksLower) / (acceptedBidsLower + asksLower + unacceptedBidsGreater);
            
            }
            
        }

       private List<int> GetBestNBids(List<(double Price, int Qty)> demandCurve, double n)
{
    List<int> bids = new List<int>();
    int lastItemCount = 0;

      foreach (var item in demandCurve)
    {
        int numBids = (int)(item.Qty - lastItemCount); // Cast to int if necessary
        lastItemCount = (int)item.Qty; // Cast to int if necessary
        //bids.AddRange((IEnumerable<int>)Enumerable.Repeat(item.Price, numBids));
        bids.AddRange(Enumerable.Repeat(item.Price, numBids).Select(x => (int)x));

        if (bids.Count >= n)
        {
            return bids.Take((int)n).ToList();
        }
    }

      return bids;
}

        private List<double> GetBestNAsks(List<(double Price, int Qty)> supplyCurve, double n)
{
    List<double> asks = new List<double>();
    int lastItemCount = 0;

    foreach (var item in supplyCurve)
    {
        int numAsks = (int)(item.Qty - lastItemCount); // Cast to int if necessary
        lastItemCount = (int)item.Qty; // Cast to int if necessary
        asks.AddRange(Enumerable.Repeat(item.Price, numAsks));

        if (asks.Count >= n)
        {
            return asks.Take((int)n).ToList();
        }
    }

    return asks;
}

        private void UpdateValues(int m, int n)
        {
            if (job == "Bid")
            {
                values[m, n] = CalcPBid(m, n);
            }
            else if (job == "Ask")
            {
                values[m, n] = CalcPAsk(m, n);
            }
        }
    }
  
public class TraderMomentum : Trader
{
    private double momentumThreshold;
    private List<double> recentPrices;

    public TraderMomentum(string ttype, string tid, double balance, double time, double momentumThreshold = 0.05)
        : base(ttype, tid, balance, time)
    {
        this.momentumThreshold = momentumThreshold;
        this.recentPrices = new List<double>();
    }

    public override Order GetOrder(double time, double pEq, double qEq, List<(double Price, int Qty)> demandCurve, List<(double Price, int Qty)> supplyCurve, double countdown, dynamic lob)
    {
        if (Orders.Count < 1 || recentPrices.Count < 2)
        {
            return null;
        }

        var coid = Orders.Keys.Max();
        var limit = Orders[coid].Price;
        var otype = Orders[coid].Otype;

        double momentum = (recentPrices.Last() - recentPrices.First()) / recentPrices.First();

        double quotePrice = limit;
        if (momentum > momentumThreshold)
        {
            quotePrice = otype == "Bid" ? limit * 1.02 : limit * 0.98; // Adjust price based on positive momentum
        }
        else if (momentum < -momentumThreshold)
        {
            quotePrice = otype == "Bid" ? limit * 0.98 : limit * 1.02; // Adjust price based on negative momentum
        }

        var order = new Order(
            TID,
            otype,
            (int)quotePrice,
            Orders[coid].Qty,
            time,
            Orders[coid].Coid,
            Orders[coid].Toid
        );

        LastQuote = order;
        return order;
    }

    public override void Respond(double time, double pEq, double qEq, List<(double, int)> demandCurve, List<(double, int)> supplyCurve, dynamic lob, List<object> trades, bool verbose)
    {
        dynamic trade = trades.FirstOrDefault();
        if (trade != null)
        {
            recentPrices.Add(trade["price"]);
            if (recentPrices.Count > 5) // Keep only the last 5 prices
            {
                recentPrices.RemoveAt(0);
            }
        }
    }
}


}

    
