using System;
using System.IO; // Equivalent to Python's os
using System.Collections.Generic; // For collections like List
using BFBSE;
using BFBSE.tbse_msg_classes;
using System.Linq;
using System.Runtime.InteropServices; // Namespace where TbseSysConsts is defined
namespace BFBSE.tbse_exchange
{
    public class OrderbookHalf
    {
        public string BookType { get; set; }
        public Dictionary<string, Order> Orders { get; set; }
        public Dictionary<int, List<object>> Lob { get; set; }
        public List<List<int>> LobAnon { get; set; }
        public int? BestPrice { get; set; }
        public string BestTid { get; set; }
        public int WorstPrice { get; set; }
        public int NOrders { get; set; }
        public int LobDepth { get; set; }

        public OrderbookHalf(string bookType, int worstPrice)
        {
            BookType = bookType;
            Orders = new Dictionary<string, Order>();
            Lob = new Dictionary<int, List<object>>();
            LobAnon = new List<List<int>>();
            BestPrice = null;
            BestTid = null;
            WorstPrice = worstPrice;
            NOrders = 0;
            LobDepth = 0;
        }

        public void AnonymizeLob()
        {
            LobAnon.Clear();
            foreach (var price in Lob.Keys.OrderBy(p => p))
            {
                int qty = (int)Lob[price][0];
                LobAnon.Add(new List<int> { price, qty });
            }
        }

        public void BuildLob()
        {
            Lob.Clear();
            foreach (var tid in Orders.Keys)
            {
                var order = Orders[tid];
                int price = (int)order.Price;
                if (Lob.ContainsKey(price))
                {
                    int qty = (int)Lob[price][0];
                    var orderList = (List<object>)Lob[price][1];
                    orderList.Add(new List<object> { order.Time, order.Qty, order.Tid, order.Toid });
                    Lob[price] = new List<object> { qty + order.Qty, orderList };
                }
                else
                {
                    Lob[price] = new List<object> { order.Qty, new List<object> { new List<object> { order.Time, order.Qty, order.Tid, order.Toid } } };
                }
            }
            AnonymizeLob();
            if (Lob.Count > 0)
            {
                if (BookType == "Bid")
                {
                    BestPrice = LobAnon.Last()[0];
                }
                else
                {
                    BestPrice = LobAnon.First()[0];
                }
                // Assuming Lob is a Dictionary<int, List<object>>
                var bestPriceKey = (int)BestPrice;
                var bestPriceEntry = (List<object>)Lob[bestPriceKey];
                var nestedList = (List<object>)bestPriceEntry[1];
                var innerList = (List<object>)nestedList[0];
                string bestTid = innerList[2].ToString();
            }
            else
            {
                BestPrice = null;
                BestTid = null;
            }
        }

        public string BookAdd(Order order)
        {
            int initialOrderCount = NOrders;
            Orders[order.Tid.ToString()] = order;
            NOrders = Orders.Count;
            BuildLob();
            return initialOrderCount != NOrders ? "Addition" : "Overwrite";
        }

        public void BookDel(Order order)
        {
            if (Orders.ContainsKey(order.Tid.ToString()))
            {
                Orders.Remove(order.Tid.ToString());
                NOrders = Orders.Count;
                BuildLob();
            }
        }

        public string DeleteBest()
        {
            var bestPriceOrders = (List<object>)Lob[(int)BestPrice];
            int bestPriceQty = (int)bestPriceOrders[0];
            var firstLevelList = (List<object>)bestPriceOrders[1];
            var secondLevelList = (List<object>)firstLevelList[0];
            string bestPriceCounterparty = secondLevelList[2].ToString();
            if (bestPriceQty == 1)
            {
                Lob.Remove((int)BestPrice);
                Orders.Remove(bestPriceCounterparty);
                NOrders--;

                if (NOrders > 0)
                {
                    BestPrice = BookType == "Bid" ? Lob.Keys.Max() : Lob.Keys.Min();
                    LobDepth = Lob.Keys.Count;
                }
                else
                {
                    BestPrice = WorstPrice;
                    LobDepth = 0;
                }
            }
            else
            {
                Lob[(int)BestPrice][0] = bestPriceQty - 1;
                var orderList = (List<object>)bestPriceOrders[1];
                orderList.RemoveAt(0);
                Lob[(int)BestPrice][1] = orderList;
                Orders.Remove(bestPriceCounterparty);
                NOrders--;
            }

            BuildLob();
            return bestPriceCounterparty;
        }
    }

    public class Orderbook
    {
        public OrderbookHalf Bids { get; set; }
        public OrderbookHalf Asks { get; set; }
        public List<Dictionary<string, object>> Tape { get; set; }
        public int QuoteId { get; set; }

        public Orderbook()
        {
            Bids = new OrderbookHalf("Bid", TbseSysConsts.TBSE_SYS_MIN_PRICE);
            Asks = new OrderbookHalf("Ask", TbseSysConsts.TBSE_SYS_MAX_PRICE);
            Tape = new List<Dictionary<string, object>>();
            QuoteId = 0;
        }

        public int GetQuoteId()
        {
            return QuoteId;
        }

        public void IncrementQuoteId()
        {
            QuoteId++;
        }
    }
    public class Exchange : Orderbook
    {
        public Exchange() : base() { }

        public (int, string) AddOrder(Order order, bool verbose)
        {
            order.Toid = base.GetQuoteId();
            base.IncrementQuoteId();

            if (verbose)
            {
                Console.WriteLine($"QUID: order.quid={order.Toid} self.quote.id={base.QuoteId}");
            }

            string response;
            int bestPrice;

            if (order.Otype == "Bid")
            {
                response = base.Bids.BookAdd(order);
                bestPrice = base.Bids.LobAnon.Last()[0];
                base.Bids.BestPrice = bestPrice;
                base.Bids.BestTid = (string)((List<object>)((List<object>)((List<object>)base.Bids.Lob[bestPrice])[1])[0])[2];
            }
            else
            {
                response = base.Asks.BookAdd(order);
                bestPrice = base.Asks.LobAnon.First()[0];
                base.Asks.BestPrice = bestPrice;
                base.Asks.BestTid = (string)((List<object>)((List<object>)((List<object>)base.Asks.Lob[bestPrice])[1])[0])[2];;
            }

            return (order.Toid, response);
        }

        public void DelOrder(double time, Order order)
        {
            if (order.Otype == "Bid")
            {
                base.Bids.BookDel(order);
                if (base.Bids.NOrders > 0)
                {
                    int bestPrice = base.Bids.LobAnon.Last()[0];
                    base.Bids.BestPrice = bestPrice;
                    base.Bids.BestTid = (string)((List<object>)((List<object>)((List<object>)base.Bids.Lob[bestPrice])[1])[0])[2];
                }
                else
                {
                    base.Bids.BestPrice = null;
                    base.Bids.BestTid = null;
                }
                var cancelRecord = new Dictionary<string, object> {
                    { "type", "Cancel" },
                    { "t", time },
                    { "order", order }
                };
                base.Tape.Add(cancelRecord);
            }
            else if (order.Otype == "Ask")
            {
                base.Asks.BookDel(order);
                if (base.Asks.NOrders > 0)
                {
                    int bestPrice = base.Asks.LobAnon.First()[0];
                    base.Asks.BestPrice = bestPrice;
                    base.Asks.BestTid = (string)((List<object>)((List<object>)((List<object>)base.Asks.Lob[bestPrice])[1])[0])[2];
                }
                else
                {
                    base.Asks.BestPrice = null;
                    base.Asks.BestTid = null;
                }
                var cancelRecord = new Dictionary<string, object> {
                    { "type", "Cancel" },
                    { "t", time },
                    { "order", order }
                };
                base.Tape.Add(cancelRecord);
            }
            else
            {
                // Neither bid nor ask?
                throw new Exception("Bad order type in del_quote()");
            }
        }

        public Dictionary<string, object> PublishLob(double time, bool verbose)
        {
            var publicData = new Dictionary<string, object>
            {
                { "t", time },
                { "bids", new Dictionary<string, object>
                    {
                        { "best", base.Bids.BestPrice },
                        { "worst", base.Bids.WorstPrice },
                        { "n", base.Bids.NOrders },
                        { "lob", base.Bids.LobAnon }
                    }
                },
                { "asks", new Dictionary<string, object>
                    {
                        { "best", base.Asks.BestPrice },
                        { "worst", base.Asks.WorstPrice },
                        { "n", base.Asks.NOrders },
                        { "lob", base.Asks.LobAnon }
                    }
                },
                { "QID", base.QuoteId },
                { "tape", base.Tape }
            };

            if (verbose)
            {
                Console.WriteLine($"publish_lob: t={time}");
                Console.WriteLine($"BID_lob={string.Join(",", base.Bids.LobAnon)}");
                Console.WriteLine($"ASK_lob={string.Join(",", base.Asks.LobAnon)}");
            }

            return publicData;
        }

        public (List<dynamic>, Dictionary<string, object>, double, int, List<(double Price, int Qty)>, List<(double Price, int Qty)>) ProcessOrderBatch2(
        double time,List<Order> orders, bool verbose)
        {
            List<tbse_msg_classes.Order> oldAsks = this.Asks.Orders.Values.ToList();
            List<tbse_msg_classes.Order> oldBids = this.Bids.Orders.Values.ToList();
            List<tbse_msg_classes.Order> newBids = new List<tbse_msg_classes.Order>();
            List<tbse_msg_classes.Order> newAsks = new List<tbse_msg_classes.Order>();

            if (orders.Count == 0)
            {
                var lob1 = PublishLob(time, false);
                var demandLOB1 = oldBids.Select(b => (b.Price, b.Qty)).ToList();
                var supplyLOB1 = oldAsks.Select(a => (a.Price, a.Qty)).ToList();

                var (supplyCurve1, demandCurve1) = CreateSupplyDemandCurves(supplyLOB1, demandLOB1);

                return (new List<dynamic>(), lob1, -1, 0, demandCurve1, supplyCurve1);
            }

            foreach (var order in orders)
            {
                if (order.Otype == "Bid")
                    newBids.Add(order);
                else
                    newAsks.Add(order);
            }

            var asks = newAsks.Concat(oldAsks).ToList();
            var bids = newBids.Concat(oldBids).ToList();

            bids.Sort((o1, o2) => o2.Price.CompareTo(o1.Price) != 0 ?
                o2.Price.CompareTo(o1.Price) : Convert.ToInt32(!oldBids.Contains(o1)) - Convert.ToInt32(!oldBids.Contains(o2)));

            asks.Sort((o1, o2) => o1.Price.CompareTo(o2.Price) != 0 ?
                o1.Price.CompareTo(o2.Price) : Convert.ToInt32(oldAsks.Contains(o1)) - Convert.ToInt32(oldAsks.Contains(o2)));

            var demandLOB = bids.Select(b => (b.Price, b.Qty)).ToList();
            var supplyLOB = asks.Select(a => (a.Price, a.Qty)).ToList();

            var (supplyCurve, demandCurve) = CreateSupplyDemandCurves(supplyLOB, demandLOB);
            var auctionPrice = FindEquilibriumPrice(supplyCurve, demandCurve);
           // Console.WriteLine("The auctionprice is: " +auctionPrice);
        //     foreach (var order in asks)
        // {
        //     Console.WriteLine(order);
        // }
        //        Console.WriteLine("now bids");
        //        foreach (var order in bids)
        // {
        //     Console.WriteLine(order);
        // }
            var buyers = bids.Where(b => b.Price >= auctionPrice).ToList();
            var sellers = asks.Where(a => a.Price <= auctionPrice).ToList();
            var tradeQty = Math.Min(buyers.Sum(b => b.Qty), sellers.Sum(s => s.Qty));

            var transactionRecords = new List<dynamic>();
            // Console.WriteLine("The length of buyers is: " + buyers.Count);
            // Console.WriteLine("The length of sellers is: " + sellers.Count);
            // Console.WriteLine("trade quantitiy is: " + tradeQty);
            while (buyers.Any() && sellers.Any() && tradeQty > 0)
            { 
                var buyer = buyers[0];
                var seller = sellers[0];
                tradeQty = Math.Min(tradeQty, Math.Min(buyer.Qty, seller.Qty));
                //Console.WriteLine("inside loop");
                var transactionRecord = new Dictionary<string, object>
        {
            { "type", "Trade" },
            { "t", time },
            { "price", auctionPrice },
            { "party1", seller.Tid },
            { "party2", buyer.Tid },
            { "qty", tradeQty },
            { "coid", buyer.Coid },
            { "counter", seller.Coid }
        };

                // Console.WriteLine("The transaction record is: " + transactionRecord);
                transactionRecords.Add(transactionRecord);
                Tape.Add(transactionRecord);


                if (verbose)
                {
                    Console.WriteLine($">>>>>>>>>>>>>>>>>TRADE t={time:5.2f} ${auctionPrice} {seller.Tid} {buyer.Tid}");
                }

                buyer.Qty -= tradeQty;
                seller.Qty -= tradeQty;

                if (buyer.Qty == 0)
                {
                    bids.Remove(buyer);
                    buyers.Remove(buyer);
                    if (oldBids.Contains(buyer))
                    {
                        DelOrder(time, buyer);
                    }
                }

                if (seller.Qty == 0)
                {
                    asks.Remove(seller);
                    sellers.Remove(seller);
                    if (oldAsks.Contains(seller))
                    {
                        DelOrder(time, seller);
                    }
                }
            }

            foreach (var o in bids.Concat(asks))
            {

                var (toid, response) = AddOrder(o, verbose);
                o.Toid = toid;
                if (verbose)
                {
                    Console.WriteLine($"TOID: order.toid={o.Toid}");
                    Console.WriteLine($"RESPONSE: {response}");
                }
            }

            var lob = PublishLob(time, false);
            //Console.WriteLine("processorderbatch2");
            // Console.WriteLine("The length of transaction records is: " + transactionRecords.Count);

            return (transactionRecords, lob, auctionPrice, transactionRecords.Count, demandCurve, supplyCurve);
        }

        public void TapeDump(string fileName, string fileMode, string tapeMode)
        {
            using (StreamWriter dumpfile = new StreamWriter(fileName, fileMode == "R/W" ? true : false, System.Text.Encoding.UTF8))
            {
                foreach (Dictionary<string, object> tapeItem in Tape)
                {
                   // Console.WriteLine(tapeItem);
                    if (tapeItem["type"].ToString() == "Trade")
                    {
                        dumpfile.WriteLine($"{tapeItem["t"]}, {tapeItem["price"]}");
                    }
                }
            }

            if (tapeMode == "wipe")
            {
                Tape.Clear();
            }
        }

 public double FindEquilibriumPrice(List<(double Price, int Qty)> supply, List<(double Price, int Qty)> demand)
{
    // Console.WriteLine("Supply List:");
    // foreach (var item in supply)
    // {
    //     Console.WriteLine($"supplyPrice: {item.Price}, Quantity: {item.Qty}");
    // }

    // Console.WriteLine("Demand List:");
    // foreach (var item in demand)
    // {
    //     Console.WriteLine($"demandPrice: {item.Price}, Quantity: {item.Qty}");
    // }

    var bestSupplyPrices = new List<double>();
    var bestDemandPrices = new List<double>();
    var smallestNetSurplus = 1000.0;

    foreach (var item in demand)
    {
        double demandPrice = item.Price;
        double demandQty = item.Qty;
        var suppliers = supply.Where(s => s.Price <= demandPrice).ToList();

        //  Console.WriteLine($"Processing demandPrice: {demandPrice}, Quantity: {demandQty}");
        //  Console.WriteLine($"Number of suppliers found: {suppliers.Count}");

        if (!suppliers.Any())
        {
            // Console.WriteLine("No suppliers found for this demand price.");
            continue;  // Skip to the next demand price
        }

        var bestSupplier = suppliers.OrderByDescending(s => s.Price).First();
        double supplyPrice = bestSupplier.Price;
        double supplyQty = bestSupplier.Qty;

        var consumerSurplus = demandQty;
        var producerSurplus = supplyQty;

        var netSurplus = Math.Abs(consumerSurplus - producerSurplus);

        // Console.WriteLine($"Best Supplier: Price: {supplyPrice}, Quantity: {supplyQty}");
        // Console.WriteLine($"Net Surplus: {netSurplus}");

        if (netSurplus < smallestNetSurplus)
        {
            bestSupplyPrices = new List<double> { supplyPrice };
            bestDemandPrices = new List<double> { demandPrice };
            smallestNetSurplus = netSurplus;
           // Console.WriteLine("Updated best prices and smallest net surplus.");
        }
        else if (netSurplus == smallestNetSurplus)
        {
            bestSupplyPrices.Add(supplyPrice);
            bestDemandPrices.Add(demandPrice);
            //Console.WriteLine("Added to best prices (equal net surplus).");
        }
    }

    if (!bestSupplyPrices.Any() || !bestDemandPrices.Any())
    {
        // If no valid prices were found, return a special value (e.g., -1)
        //Console.WriteLine("No valid best prices found. Returning -1.");
        return -1;
    }

    var bestSupplyPrice = bestSupplyPrices.Count == 1 ? bestSupplyPrices.First() : bestSupplyPrices.Average();
    var bestDemandPrice = bestDemandPrices.Count == 1 ? bestDemandPrices.First() : bestDemandPrices.Average();

    var equilibriumPrice = (bestSupplyPrice + bestDemandPrice) / 2.0;

    // Console.WriteLine($"Best Supply Price: {bestSupplyPrice}");
    // Console.WriteLine($"Best Demand Price: {bestDemandPrice}");
    // Console.WriteLine($"Equilibrium Price: {equilibriumPrice}");

    return equilibriumPrice;
}
        public (List<(double Price, int Qty)>, List<(double Price, int Qty)>) CreateSupplyDemandCurves(List<(double Price, int Qty)> supplyLOB, List<( double Price, int Qty)> demandLOB)
        {
            var supplyCurve = new List<(double Price, int Qty)>();
            var demandCurve = new List<(double Price, int Qty)>();
            var demandQty = 0;
            var supplyQty = 0;

            foreach (var (price, qty) in supplyLOB)
            {
                supplyQty += qty;
                supplyCurve.Add((price, supplyQty));
            }

            foreach (var (price, qty) in demandLOB)
            {
                demandQty += qty;
                demandCurve.Add((price, demandQty));
            }

            supplyCurve.Sort((a, b) => b.Item1.CompareTo(a.Item1));
            demandCurve.Sort((a, b) => a.Item1.CompareTo(b.Item1));

            return (supplyCurve, demandCurve);
        }

       
    }

}