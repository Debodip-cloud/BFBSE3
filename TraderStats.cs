 using System;
 namespace BFBSE
{
    public class TraderStats
    {
        // Properties
    public int N { get; set; }
    public double BalanceSum { get; set; }
    public double Time1 { get; set; }
    public double Time2 { get; set; }
    public int TradesSum { get; set; }

    // Constructor
    public TraderStats(int n, int balanceSum, int tradesSum, int time1, int time2)
    {
        N = n;
        BalanceSum = balanceSum;
        TradesSum=tradesSum;
        Time1 = time1;
        Time2 = time2;
    }

    // Default Constructor
    public TraderStats() { }

    // Method to display trader stats
    public void DisplayStats()
    {
        Console.WriteLine($"N: {N}, BalanceSum: {BalanceSum}, Time1: {Time1}, Time2: {Time2}");
    }
    }
}