#if !POINT_H
#define POINT_H

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Landis.Extension.Landispro.Fire
{
    class LDPOINT
    {
        public int x;
        public int y;

        public LDPOINT()
        {
            x = y = 0;
        }
        public LDPOINT(int tx, int ty)
        {
            this.x = tx;
            this.y = ty;
        }

        public bool IsEqual(LDPOINT right)
        {
            return (x == right.x) && (y == right.y);
        }

#if __HARVEST__
        public void Print()
        {
            Console.Write("{");
            Console.Write(x);
            Console.Write(",");
            Console.Write(y);
            Console.Write("}");
        }

#endif

    }
}

#endif