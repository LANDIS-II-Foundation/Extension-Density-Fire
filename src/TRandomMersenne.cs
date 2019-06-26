using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
/************************** MERSENNE.CPP ******************** AgF 2001-10-18 *

*  Random Number generator 'Mersenne Twister'                                *

*                                                                            *

*  This random number generator is described in the article by               *

*  M. Matsumoto & T. Nishimura, in:                                          *

*  ACM Transactions on Modeling and Computer Simulation,                     *

*  vol. 8, no. 1, 1998, pp. 3-30.                                            *

*                                                                            *

*  Experts consider this an excellent random number generator.               *

*                                                                            *

*****************************************************************************/
namespace Landis.Extension.Landispro.Fire
{
    class TRandomMersenne
    {
#if true
        // define constants for MT11213A
        public const int N = 351;
        public const int M = 175;
        public const int R = 19;
        public const uint MATRIX_A = 0xEABD75F5;

        public const int TEMU = 11;
        public const int TEMS = 7;
        public const int TEMT = 15;
        public const int TEML = 17;
        public const uint TEMB = 0x655E5280;
        public const uint TEMC = 0xFFD58000;

#else
        // or constants for MT19937

        public const int N = 624;
        public const int M = 397;
        public const int R = 31;
        public const uint MATRIX_A = (int)0x9908B0DF;

        public const int TEMU = 11;
        public const int TEMS = 7;
        public const int TEMT = 15;
        public const int TEML = 18;
        public const int TEMB = 0x9D2C5680;
        public const int TEMC = 0xEFC60000;
#endif
        private uint[] mt = new uint[N]; // state vector
        private int mti; // index into mt

        public void RandomInit(int seed)
        {
            // re-seed generator
            uint s = (uint)seed;
            for (mti = 0; mti < N; mti++)
            {
                s = s * 29943829 - 1;
                mt[mti] = s;
            }
        }

        public uint BRandom()
        {
            // generate 32 random bits
            uint y;
            if (mti >= N)
            {
                // generate N words at one time
                uint LOWER_MASK = (1u << R) - 1; // lower R bits
                int UPPER_MASK = -1 << R; // upper 32-R bits
                int kk;
                int km;
                for (kk = 0, km = M; kk < N - 1; kk++)
                {
                    y = (uint)(mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                    mt[kk] = (uint)(mt[km] ^ (y >> 1) ^ (-(int)(y & 1) & MATRIX_A));
                    if (++km >= N)
                    {
                        km = 0;
                    }
                }
                y = (uint)(mt[N - 1] & UPPER_MASK) | (mt[0] & LOWER_MASK);
                mt[N - 1] = (uint) (mt[M - 1] ^ (y >> 1) ^ (-(int)(y & 1) & MATRIX_A));
                mti = 0;
            }
            y = mt[mti++];
            // Tempering (May be omitted):
            y ^= y >> TEMU;
            y ^= (y << TEMS) & TEMB;
            y ^= (y << TEMT) & TEMC;
            y ^= y >> TEML;
            return y;
        }

        public double Random()
        {
            // output random float number in the interval 0 <= x < 1       
            // get 32 random bits and convert to float
            uint r = BRandom();
            Convert convert = new Convert();
            convert.i[0] = r << 20;
            convert.i[1] = (r >> 12) | 0x3FF00000;
            return convert.f - 1.0;
        }

        public uint IRandom(int min, int max)
        {
            // output random integer in the interval min <= x <= max
            uint r;
            r = (uint)(((max - min + 1) * Random()) + min); // multiply interval with random and truncate
            if (r > max)
            {
                r = (uint)max;
            }
            if (max < min)
            {
                return 0x80000000;
            }
            return r;
        }

        public TRandomMersenne(int seed)
        {           // constructor

            RandomInit(seed);
        }

    }

    class Convert
    {
        public double f;
        public uint[] i;
        public Convert()
        {
            i = new uint[2];
        }
    }
}


