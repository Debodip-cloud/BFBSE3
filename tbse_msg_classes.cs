using System;

namespace BFBSE.tbse_msg_classes
{
    public class Order
    {
        public string Tid { get; }
        public string Otype { get; }
        public double Price { get; }
        public int Qty { get; set;}
        public double Time { get; }
        public int Coid { get; }
        public int Toid { get; set;}

        public Order(string tid, string otype, double price, int qty, double time, int coid, int toid)
        {
            Tid = tid;
            Otype = otype;
            Price = price;
            Qty = qty;
            Time = time;
            Coid = coid;
            Toid = toid;
        }

        public override string ToString()
        {
            return $"[{Tid} {Otype} P={Price.ToString().PadLeft(3)} Q={Qty} T={Time:0.00} COID:{Coid} TOID:{Toid}]";
        }
    }
}