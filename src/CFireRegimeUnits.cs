using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OSGeo.OSR;
using OSGeo.OGR;
using OSGeo.GDAL;
using System.IO;

namespace Landis.Extension.Landispro.Fire
{

    //typedef List<LDPOINT> sitesWRTFRUList;
    class CFireRegimeUnits
    {
        public int[] NumOfSites = new int[256];
        private CFireRegimeUnit[] pFireRegimeUnit; //Array holding all fire regime units.
        private CFireSites m_pFireSites;
        private StochasticLib m_pStochastic;
        private int numLU; //Number of fire regime units.
        private int currentLU; //Current fire regime unit being pointed to by first
        //and next access functions.
        private int maxLU; //Maximum number of fire regime units.  Defined upon


        //////////////////////////////////////////////////////////////////////

        // Construction/Destruction

        //////////////////////////////////////////////////////////////////////
        public CFireRegimeUnits(int n, StochasticLib pStochastic)
        {
            numLU = 0;
            pFireRegimeUnit = new CFireRegimeUnit[n];
            currentLU = 0;
            maxLU = n;
            m_pStochastic = pStochastic;
            for (int i = 0; i < 256; i++)
            {
                NumOfSites[i] = 0;
            }
        }
        public void Dispose()
        {
            if (pFireRegimeUnit != null)
            {
                pFireRegimeUnit = null;
            }

        }

        public void read(StreamReader infile)
        //Read set of Fire regime units from a file.
        {
            numLU = 0;
            while (infile.Peek() >= 0)
            {
                if (numLU < maxLU) //J.Yang 04/16/2002
                {
                    pFireRegimeUnit[numLU] = new CFireRegimeUnit();
                    pFireRegimeUnit[numLU].Read(infile);
                    pFireRegimeUnit[numLU].index = numLU;
                    numLU++;
                }
                else
                {
                    throw new Exception("CFireRegimeUnits::read(FILE*)-> Array bounds error.");
                }
            }
        }

        public void write(StreamWriter outfile)
        //Write set of land units to a file.
        {
            for (int i = 0; i < numLU; i++)
            {
                pFireRegimeUnit[i].Write(outfile);
            }
        }

        public void dump()
        //Dump set of land units to the CRT.
        {
            for (int i = 0; i < numLU; i++)
            {
                pFireRegimeUnit[i].Dump();
                Console.Write("===================================\n");
            }
        }

        public CFireRegimeUnit this[int n]
        //Referrence a land unit by land unit number.
        {
            get
            {
                if (n > numLU || n < 0)
                {
                    Console.WriteLine("{0}, {1}",n, numLU);
                    return null;
                }
                else
                {
                    return pFireRegimeUnit[n]; // be careful this pointer. J.Yang
                }
            }
        }

        public CFireRegimeUnit this[string name]
        //Referrence a fire regime unit by fire regime unit name.
        {
            get
            {
                for (int i = 0; i < numLU; i++)
                {

                    if (string.Compare(name, pFireRegimeUnit[i].name) == 0)
                    {
                        return pFireRegimeUnit[i];
                    }
                }
                return null;
            }
        }

        public CFireRegimeUnit first()
        //Referrence first fire regime unit.
        {
            currentLU = 0;
            if (numLU == 0)
            {
                return null;
            }
            else
            {
                return pFireRegimeUnit[0];
            }
        }

        public CFireRegimeUnit next()
        //Referrence next fire regime unit.
        {
            currentLU++;
            if (currentLU >= numLU)
            {
                return null;
            }
            else
            {
                return pFireRegimeUnit[currentLU];
            }
        }

        public int number()
        //Returns number of fire regime units.
        {
            return numLU;
        }

        public void dispatch()
        {
            int i;
            int j;
            int k;
            int snr;
            int snc;
            snr = m_pFireSites.numRows();
            snc = m_pFireSites.numColumns();
            for (k = 0; k < numLU; k++)
            {
                NumOfSites[k] = 0;
            }
            for (i = 1; i <= snr; i++)
            {
                for (j = 1; j <= snc; j++)
                {
                    k = m_pFireSites[i, j].FRUIndex;
                    NumOfSites[k]++;
                }
            }
        }

        public void attach(CFireSites pFireSites)
        {
            m_pFireSites = pFireSites;
        }

        public void readFireRegimeGIS(BinaryReader mapFile)
        {
            //This will read fire regime map and associate fire regime Unit to each site.
            //mapFile is an Erdas 8 bit gis file.  The file pointer is
            //placed on the first map element.  yDim and xDim are the (x,y) dimensions
            //of the Erdas map.
            byte c;
            uint[] dest = new uint[64];
            int nCols;
            int nRows;
            int numRead;
            int coverType;

            FIRESITE s;
            int b16or8; //true: 16, false 8 bit
            ushort intdata;

            //LDfread((string)dest, 4, 32, mapFile);
            for (int i = 0; i < 32; i++)
                dest[i] = mapFile.ReadUInt32();

            if ((dest[1] & 0xff0000) == 0x020000)
            {
                b16or8 = 16;
            }
            else if ((dest[1] & 0xff0000) == 0)
            {
                b16or8 = 8;
            }
            else
            {
                b16or8 = -1;
                throw new Exception("Error: IO: Fire regime GIS map is niether 16 bit or 8 bit.");
            }
#if __UNIX__  
		ERDi4_c(dest[4], nCols);   
		ERDi4_c(dest[5], nRows);    
#else
            nCols = (int)dest[4];
            nRows = (int)dest[5];
#endif
            if ((nCols != m_pFireSites.m_iCols) || (nRows != m_pFireSites.m_iRows))
            {
                throw new Exception("the dimension of fire regime GIS map is not consistent.");
            }

            if (b16or8 == 8) //8 bit
            {
                for (int i = nRows; i > 0; i--)
                {
                    for (int j = 1; j <= nCols; j++)
                    {
                        //numRead = LDfread((char)(c), 1, 1, mapFile);     
                        c = mapFile.ReadByte();

                        coverType = (int)c;
                        //YYF
                        //if ((numRead >= 0) && (coverType >= 0))
                        if (coverType >= 0)
                        {
                            //s=sites(i,j);
                            //s->landUnit=landUnits(coverType);
                            //<Add By Qia on Nov 24 2008>
                            m_pFireSites.BefStChg(i, j);
                            //</Add By Qia on Nov 24 2008>
                            s = m_pFireSites[i, j]; //fire site
                            s.FRUIndex = coverType;
                            //<Add By Qia on Nov 24 2008>
                            m_pFireSites.AftStChg(i, j);
                            //</Add By Qia on Nov 24 2008>
                        }
                        else
                        {
                            throw new Exception("illegal landtype class found7.");
                        }
                    }
                }
            }
            else if (b16or8 == 16) //16 bit
            {
                for (int i = nRows; i > 0; i--)
                {
                    for (int j = 1; j <= nCols; j++)
                    {
                        //numRead = LDfread((string)(intdata), 2, 1, mapFile);
                        intdata = mapFile.ReadUInt16();
                        coverType = (int)intdata;

                        //YYF
                        //if ((numRead >= 0) && (coverType >= 0))
                        if (coverType >= 0)
                        {
                            //<Add By Qia on Nov 24 2008>
                            m_pFireSites.BefStChg(i, j);
                            //</Add By Qia on Nov 24 2008>
                            s = m_pFireSites[i, j]; //fire site
                            s.FRUIndex = coverType;
                            //<Add By Qia on Nov 24 2008>
                            m_pFireSites.AftStChg(i, j);
                            //</Add By Qia on Nov 24 2008>
                        }
                        else
                        {
                            throw new Exception("illegal landtype class found8.");
                        }
                    }
                }
            }

        }

        public void readFireRegimeIMG(Dataset fpImg)
        {
            //This will read fire regime .img map and associate fire regime Unit to each site.
            int nCols;
            int nRows;
            int numRead;
            int coverType;
            int noDataValue;
            FIRESITE s;
            //unsigned short		 intdata;
            float[] pafScanline; //*
            Band poBand; //*
            poBand = fpImg.GetRasterBand(1); //*
            //LDfread((char*)dest, 4, 32, mapFile);
#if __UNIX__  
		ERDi4_c(dest[4], nCols);    
		ERDi4_c(dest[5], nRows);    
#else
            nCols = fpImg.RasterXSize; //*
            nRows = fpImg.RasterYSize; //*
#endif
            if ((nCols != m_pFireSites.m_iCols) || (nRows != m_pFireSites.m_iRows))
            {
                throw new Exception("the dimension of fire regime GIS map is not consistent.");
            }
            pafScanline = new float[nCols * nRows];
            poBand = fpImg.GetRasterBand(1);
            poBand.ReadRaster(0, 0, nCols, nRows, pafScanline, nCols, nRows, 0, 0);

            //YYF
            //noDataValue = GDALGetRasterNoDataValue(poBand, NULL);
            //double val;
            //int hasval;
            //poBand.GetNoDataValue(out val, out hasval);
            //noDataValue = (int)val;      
            for (int i = nRows; i > 0; i--)
            {
                for (int j = 1; j <= nCols; j++)
                {
                    //Console.WriteLine(pafScanline[(nRows - i) * nCols + j - 1]);
                    coverType = (int)pafScanline[(nRows - i) * nCols + j - 1]; //*
                    //if (coverType == 15) coverType = 0;
                    //if (coverType == noDataValue)
                    //{
                    //    coverType = 0;
                    //}
                    //Console.WriteLine(coverType);
                    if (coverType >= 0)
                    {
                        m_pFireSites.BefStChg(i, j);
                        s = m_pFireSites[i, j]; //fire site
                        s.FRUIndex = coverType;
                        m_pFireSites.AftStChg(i, j);
                    }
                    else
                    {
                        throw new Exception("Illegal fire site class found9.");
                    }
                }

            }

        }

        public void updateIGDensity(int cellSize)
        {
            for (int i = 0; i < numLU; i++)
            {
                pFireRegimeUnit[i].m_fIgPoisson = pFireRegimeUnit[i].m_fIgPoisson * NumOfSites[i] * cellSize * cellSize / 10000;
            }
        }


    }
}
