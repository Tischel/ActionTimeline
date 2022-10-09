using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ActionTimeline.Helpers
{
    public static class Utils
    {
        public static bool UnderThreshold(double start, double end)
        {
            return Math.Abs(end - start) < Plugin.Settings.GCDClippingThreshold;
        }
        
    }
}
