using System;
using System.Runtime.Serialization;

namespace Decadence.Models
{
    [DataContract]
    public class CachedTrack
    {
        [DataMember]
        public string FilePath { get; set; }

        [DataMember]
        public string Title { get; set; }

        [DataMember]
        public string Artist { get; set; }

        [DataMember]
        public string Album { get; set; }

        [DataMember]
        public string Duration { get; set; }

        [DataMember]
        public DateTime LastModified { get; set; }

        [DataMember]
        public DateTime LastScanned { get; set; }
    }
}