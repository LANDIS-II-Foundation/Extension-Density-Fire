#if !AFX_FIREREGIMEUNIT_H__B75FC4FA_AA05_4AB3_B4FA_1E0DFCD9F143__INCLUDED_

#define AFX_FIREREGIMEUNIT_H__B75FC4FA_AA05_4AB3_B4FA_1E0DFCD9F143__INCLUDED_

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Landis.Extension.Landispro.Fire
{
    internal static class DefineConstants
    {
        public const int PASSIVE = 0;
        public const int ACTIVE = 1;
        public const int WATER = 2;
        public const int WETLAND = 3;
        public const int BOG = 4;
        public const int LOWLAND = 5;
        public const int NONFOREST = 6;
    }

    class CFireRegimeUnit
    {
        private int status; //Either ACTIVE, PASSIVE, or WATER.

        public string name; //Fire Regime Unit name.

        public int index; //0, 1, 2, ...

        public int fireInterval; //This holds fire classes in conjunction with fire
        public int initialLastFire;
        public int[] windCurve = new int[5];
        public int[] windClass = new int[5];
        public int[] fireCurve = new int[5];
        public int[] fireClass = new int[5];

        public double m_fIgPoisson; //Fire ignition Poisson parameter
        public double m_fMFS; // mean fire size in this landtype (ha)
        public double m_fFireSTD; //fire size variance squar root in this landtype (ha)

        public CFireRegimeUnit()
        {
            name = null;
            m_fIgPoisson = 0.0;
            m_fMFS = 0.0;
            m_fFireSTD = 0.0;
            fireInterval = 0;
        }
        public void Dispose()
        {
            if (name != null)
            {
                name = null; //Nim: changed delete to delete []
            }
        }

        public bool Active()
        {
            if (status == DefineConstants.ACTIVE)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Dump()
        {
            int i;
            Console.Write("Name:          {0}\n", name);
            Console.Write("fireInterval:  {0:D}\n", fireInterval);
            Console.Write("IgnitionPoissonParameter:  {0:f}\n", m_fIgPoisson);
            Console.Write("MFS:      {0:f}\n", m_fMFS);
            Console.Write("FS STD deviation:      {0:f}\n", m_fFireSTD);
            for (i = 0; i < 5; i++) //Nim: changed int i to i
            {
                Console.Write("Class {0:D}:  {1:D}\n", fireClass[i], fireCurve[i]);
            }
        }

        public void Read(StreamReader inFile)
        {
            string instring;
            string[] sarray;

            if ((instring = inFile.ReadLine()) == null)
                throw new Exception("Error reading in name from landtype file.");
            sarray = instring.Split('#');
            if (name != null)
            {
                name = null;
            }
            name = sarray[0].Trim();

            if ((instring = inFile.ReadLine()) == null)
                throw new Exception("Error reading in fireInterval from landtype file.");
            sarray = instring.Split('#');
            fireInterval = int.Parse(sarray[0].Trim());

            if ((instring = inFile.ReadLine()) == null)
                throw new Exception("Error reading in fire ignition poisson parameter from landtype file.");
            sarray = instring.Split('#');
            m_fIgPoisson = double.Parse(sarray[0]);

            if ((instring = inFile.ReadLine()) == null)
                throw new Exception("Error reading in MFS from landtype file.");
            sarray = instring.Split('#');
            m_fMFS = double.Parse(sarray[0]);

            if ((instring = inFile.ReadLine()) == null)
                throw new Exception("Error reading in fire size Variance from landtype file.");
            sarray = instring.Split('#');
            m_fFireSTD = double.Parse(sarray[0]);

            if ((instring = inFile.ReadLine()) == null)
                throw new Exception("Error reading in initialLastFire from landtype file.");
            sarray = instring.Split('#');
            initialLastFire = int.Parse(sarray[0]);

            if ((instring = inFile.ReadLine()) == null)
                throw new Exception("Error reading in fireCurve from landtype file.");
            sarray = instring.Split(' ');
            for (int i = 0; i < 5; i++)
            {
                fireCurve[i] = int.Parse(sarray[i]);
            }

            if ((instring = inFile.ReadLine()) == null)
                throw new Exception("Error reading in fireClass from landtype file.");
            sarray = System.Text.RegularExpressions.Regex.Split(instring.Trim(), @"\s+");
            for (int i = 0; i < 5; i++)
            {
                fireClass[i] = int.Parse(sarray[i]);
            }

            if ((instring = inFile.ReadLine()) == null)
                throw new Exception("Error reading in fireCurve from landtype file.");
            sarray = System.Text.RegularExpressions.Regex.Split(instring.Trim(), @"\s+");
            for (int i = 0; i < 5; i++)
            {
                windCurve[i] = int.Parse(sarray[i]);
            }

            if ((instring = inFile.ReadLine()) == null)
                throw new Exception("Error reading in fireClass from landtype file.");
            sarray = System.Text.RegularExpressions.Regex.Split(instring.Trim(), @"\s+");

            inFile.ReadLine();

            for (int i = 0; i < 5; i++)
            {
                windClass[i] = int.Parse(sarray[i]);
            }

            if (name.Equals("empty"))
            {
                status = DefineConstants.PASSIVE;
            }
            else if (name.Equals("water"))
            {
                status = DefineConstants.WATER;
            }
            else if (name.Equals("wetland"))
            {
                status = DefineConstants.WETLAND;
            }
            else if (name.Equals("bog"))
            {
                status = DefineConstants.BOG;
            }
            else if (name.Equals("lowland"))
            {
                status = DefineConstants.LOWLAND;
            }
            else if (name.Equals("nonforest"))
            {
                status = DefineConstants.NONFOREST;
            }
            else status = DefineConstants.ACTIVE;
        }

        public bool Water()
        {
            if (status == DefineConstants.WATER)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Write(StreamWriter outfile)

        {
            int i;
            outfile.WriteLine(fireInterval);
            outfile.WriteLine(m_fIgPoisson);
            outfile.WriteLine(m_fMFS);
            outfile.WriteLine(m_fFireSTD);

            for (i = 0; i < 5; i++)
            {
                outfile.WriteLine(fireCurve[i]);
            }
            outfile.WriteLine();
            for (i = 0; i < 5; i++)
            {
                outfile.WriteLine(fireClass[i]);
            }
            outfile.WriteLine();
        }

    }
}

#endif