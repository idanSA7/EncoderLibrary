using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EncoderLIbrary
{
    public interface IIcdItem
    {
        string Id { get; }
        int Location { get; }
        string Name { get; }
        string Mask { get; }
        //int StartBit { get; }
        int Bit { get; }
        float Min { get; }
        float Max { get; }
        string Type { get; }
    }
}
