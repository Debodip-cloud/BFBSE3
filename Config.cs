using System;
using System.Collections.Generic;

namespace BFBSE
{
    class Config
    {
        // Batch interval
        public static int BatchInterval { get; } = 0.2; // Interval between batches in number of seconds.

        // General
        public static int SessionLength { get; } = 1;  // Length of session in seconds.
        public static int VirtualSessionLength { get; } = 600;  // Number of virtual timesteps per sessionLength.
        public static bool Verbose { get; } = true;  // Adds additional output for debugging.

        // BSE ONLY
        public static double StartTime { get; } = 0.0;
        public static double EndTime { get; } = 600.0;

        // Trader Schedule
        public static int NumZIC { get; } = 0;
        public static int NumZIP { get; } = 5;
        public static int NumGDX { get; } = 0;
        public static int NumAA { get; } = 0;
        public static int NumGVWY { get; } = 5;
        public static int NumSHVR { get; } = 0;
        public static int NUM_Momentum { get; }=0;
        // Order Schedule
        public static bool UseOffset { get; } = false;  // Use an offset function to vary equilibrium price, this is disabled if useInputFile = True
        public static bool UseInputFile { get; } = false;  // Use an input file to define order schedule (e.g. Real World Trading data)
        public static string InputFile { get; } = "RWD/IBM-310817.csv"; // Path to real world data input file
        public static string StepMode { get; } = "random";  // Valid values: 'fixed', 'jittered', 'random'
        public static string TimeMode { get; } = "periodic";  // Valid values: 'periodic', 'drip-fixed', 'drip-jitter', 'drip-poisson'
        public static int Interval { get; } = 250;  // Virtual seconds between new set of customer orders being generated.

        public static Dictionary<string, Dictionary<string, int>> Supply = new Dictionary<string, Dictionary<string, int>>
        {
            { "rangeMax", new Dictionary<string, int>
                {
                    { "rangeHigh", 200 }, // Range of values between which the max possible sell order will be randomly placed
                    { "rangeLow", 100 }
                }
            },
            { "rangeMin", new Dictionary<string, int>
                {
                    { "rangeHigh", 100 }, // Range of values between which the min possible sell order will be randomly placed
                    { "rangeLow", 0 }
                }
            }
        };

        // NOTE: If symmetric = True this schedule is ignored and the demand schedule will equal the above supply schedule.
        public static Dictionary<string, Dictionary<string, int>> Demand = new Dictionary<string, Dictionary<string, int>>
        {
            { "rangeMax", new Dictionary<string, int>
                {
                    { "rangeHigh", 200 }, // Range of values between which the max possible buy order will be randomly placed
                    { "rangeLow", 100 }
                }
            },
            { "rangeMin", new Dictionary<string, int>
                {
                    { "rangeHigh", 100 }, // Range of values between which the min possible buy order will be randomly placed
                    { "rangeLow", 0 }
                }
            }
        };

        // For single schedule: using config trader schedule, or command-line trader schedule.
        public static int NumTrials = 10;
 
        // For multiple schedules: using input csv file.
        public static int NumSchedulesPerRatio { get; } = 10;  // Number of schedules per ratio of traders in csv file.
        public static int NumTrialsPerSchedule { get; } = 100;  // Number of trials per schedule.
        public static bool Symmetric { get; } = true;  // Should range of supply = range of demand?
                                                       // Validation method
        public static bool ParseConfig()
        {
            bool valid = true;

            if (!(SessionLength is int))
            {
                Console.WriteLine("CONFIG ERROR: sessionLengths must be integer.");
                valid = false;
            }
            if (!(VirtualSessionLength is int))
            {
                Console.WriteLine("CONFIG ERROR: virtualSessionLengths must be integer.");
                valid = false;
            }
            if (!(Verbose is bool))
            {
                Console.WriteLine("CONFIG ERROR: verbose must be bool.");
                valid = false;
            }
            if (!(StartTime is double))
            {
                Console.WriteLine("CONFIG ERROR: start_time must be a float.");
                valid = false;
            }
            if (!(EndTime is double))
            {
                Console.WriteLine("CONFIG ERROR: end_time must be a float.");
                valid = false;
            }
            if (!(NumZIC is int && NumAA is int && NumGDX is int && NumGVWY is int && NumSHVR is int && NumZIP is int))
            {
                Console.WriteLine("CONFIG ERROR: Trader schedule values must be integer.");
                valid = false;
            }
            if (!(UseOffset is bool))
            {
                Console.WriteLine("CONFIG ERROR: useOffset must be bool.");
                valid = false;
            }
            if (!(StepMode is string))
            {
                Console.WriteLine("CONFIG ERROR: stepmode must be string.");
                valid = false;
            }
            if (!(TimeMode is string))
            {
                Console.WriteLine("CONFIG ERROR: timemode must be string.");
                valid = false;
            }
            if (!(Interval is int))
            {
                Console.WriteLine("CONFIG ERROR: interval must be integer.");
                valid = false;
            }
            if (!(Supply["RangeMax"]["RangeHigh"] is int && Supply["RangeMax"]["RangeLow"] is int &&
                  Supply["RangeMin"]["RangeHigh"] is int && Supply["RangeMin"]["RangeLow"] is int &&
                  Demand["RangeMax"]["RangeHigh"] is int && Demand["RangeMax"]["RangeLow"] is int &&
                  Demand["RangeMin"]["RangeHigh"]is int && Demand["RangeMin"]["RangeLow"] is int))
            {
                Console.WriteLine("CONFIG ERROR: Trader schedule values must be integer.");
                valid = false;
            }
            if (!(NumTrials is int))
            {
                Console.WriteLine("CONFIG ERROR: numTrials must be integer.");
                valid = false;
            }
            if (!(NumSchedulesPerRatio is int))
            {
                Console.WriteLine("CONFIG ERROR: numSchedulesPerRatio must be integer.");
                valid = false;
            }
            if (!(NumTrialsPerSchedule is int))
            {
                Console.WriteLine("CONFIG ERROR: numTrialsPerSchedule must be integer.");
                valid = false;
            }
            if (!(Symmetric is bool))
            {
                Console.WriteLine("CONFIG ERROR: symmetric must be bool.");
                valid = false;
            }

            if (!valid)
            {
                return false;
            }

            if (SessionLength <= 0 || VirtualSessionLength <= 0)
            {
                Console.WriteLine("CONFIG ERROR: Session lengths must be greater than 0.");
                valid = false;
            }
            if (StartTime < 0)
            {
                Console.WriteLine("CONFIG ERROR: start_time must be greater than or equal to 0.");
                valid = false;
            }
            if (EndTime <= StartTime)
            {
                Console.WriteLine("CONFIG ERROR: end_time must be greater than start_time.");
                valid = false;
            }
            if (NumAA < 0 || NumGDX < 0 || NumGVWY < 0 || NumSHVR < 0 || NumZIC < 0 || NumZIP < 0)
            {
                Console.WriteLine("CONFIG ERROR: All trader schedule values must be greater than or equal to 0.");
                valid = false;
            }
            if (StepMode != "fixed" && StepMode != "jittered" && StepMode != "random")
            {
                Console.WriteLine("CONFIG ERROR: stepmode must be 'fixed', 'jittered' or 'random'.");
                valid = false;
            }
            if (TimeMode != "periodic" && TimeMode != "drip-fixed" && TimeMode != "drip-jittered" && TimeMode != "drip-poisson")
            {
                Console.WriteLine("CONFIG ERROR: timemode must be 'periodic', 'drip-fixed', 'drip-jittered' or 'drip-poisson'.");
                valid = false;
            }
            if (Interval <= 0)
            {
                Console.WriteLine("CONFIG ERROR: interval must be greater than 0.");
                valid = false;
            }
            if (Supply["RangeMax"]["RangeHigh"] < 0 || Supply["RangeMax"]["RangeLow"] < 0 ||
                Supply["RangeMin"]["RangeHigh"] < 0 || Supply["RangeMin"]["RangeLow"]< 0 ||
                Demand["RangeMax"]["RangeHigh"] < 0 || Demand["RangeMax"]["RangeLow"] < 0 ||
                Demand["RangeMin"]["RangeHigh"] < 0 || Demand["RangeMin"]["RangeLow"] < 0)
            {
                Console.WriteLine("CONFIG ERROR: Supply range values must be greater than 0.");
                valid = false;
            }
            if (Supply["RangeMax"]["RangeHigh"] < Supply["RangeMax"]["RangeLow"] || Demand["RangeMax"]["RangeHigh"] < Demand["RangeMax"]["RangeLow"] ||
                Supply["RangeMin"]["RangeLow"] < Supply["RangeMin"]["RangeLow"] || Demand["RangeMin"]["RangeHigh"] < Demand["RangeMin"]["RangeLow"])
            {
                Console.WriteLine("CONFIG ERROR: rangeMax must be greater than or equal to rangeMin.");
                valid = false;
            }
            if (NumTrials < 1)
            {
                Console.WriteLine("CONFIG ERROR: numTrials must be greater than or equal to 1.");
                valid = false;
            }
            if (NumSchedulesPerRatio < 1)
            {
                Console.WriteLine("CONFIG ERROR: numSchedulesPerRatio must be greater than or equal to 1.");
                valid = false;
            }
            if (NumTrialsPerSchedule < 1)
            {
                Console.WriteLine("CONFIG ERROR: numTrialsPerSchedule must be greater than or equal to 1.");
                valid = false;
            }

            return valid;
        }
    }
}

