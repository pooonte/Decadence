using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decadence.Models
{
    public class QueueItem
    {
        public int Index { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public bool IsCurrent { get; set; }
        public string DurationText { get; set; }
    }
}
