#if !AFX_FinneyCell_H__D61BD8F8_992C_4244_B10E_9C1A9EB5CDD4__INCLUDED_

#define AFX_FinneyCell_H__D61BD8F8_992C_4244_B10E_9C1A9EB5CDD4__INCLUDED_

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Landis.Extension.Landispro.Fire
{


    class CFinneyCell
    {
        public double minTime;
        public int col;
        public int row;
         
        public CFinneyCell(){ }
        ~CFinneyCell() { }

        public void SetValue(int r, int c, double time)
        {
            row = r;
            col = c;
            minTime = time;
        }

        public static bool operator < (CFinneyCell cur, CFinneyCell right)
        {
            return (cur.minTime < right.minTime);
        }
        public static bool operator >(CFinneyCell cur, CFinneyCell right)
        {
            return (cur.minTime > right.minTime);
        }

        public static bool operator >= (CFinneyCell cur, CFinneyCell right)
        {
            return (cur.minTime >= right.minTime);
        }

        public static bool operator <=(CFinneyCell cur, CFinneyCell right)
        {
            return (cur.minTime <= right.minTime);
        }

        public static bool operator == (CFinneyCell cur, CFinneyCell right)
        {
            return (cur.minTime == right.minTime);
        }

        public static bool operator !=(CFinneyCell cur, CFinneyCell right)
        {
            return (cur.minTime != right.minTime);
        }

        //public static bool Equal(CFinneyCell cur, CFinneyCell right)
        //{
        //    return (cur.minTime == right.minTime);
        //}

        //public static bool NotEuqal(CFinneyCell cur, CFinneyCell right)
        //{
        //    return (cur.minTime != right.minTime);
        //}

    }


}

#endif