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
    static class DEFINE
    {
        public const int MAX_TRY = 255;
        public const int FNSIZE = 256;
        public const int MAX_LANDUNITS = 65535;
    }

    public class FireParam
    {
        //output files
        public string yearlyFn; // log fire where each row record one fire event
        public string finalFn;
        public string logFn;
        //input files
        public string fireRegimeAttrFn; //#fire regime changing input - #the gis file having elevation value for each site. it is 16 bit. if flag of using DEM data is 0, this item has to be N/A
        public string fireRegimeDataFn;
        public string FinneyParamFN;
        public string DEMDataFn;
        public string File_regime_change;
        //fire spread parameters
        public double fSpreadProb; //Base probability for fuel class 3
        public double fCoeff1; //Wind coefficient,Topography coefficient,Predefined fire size distribution coefficient
        public double fCoeff2;
        public double fCoeff3;
        //prevailing wind regime (across landscape)
        public int iNonWindPercent;
        public int[] iCummWindClass = new int[40];
        /*	
            int iWindIntensity;	
            int iWindDirectionIndex;		//Prevailing wind direction Index, 0-NW; 1-W; 2-SW; 3-S; 4-SE; 5-E; 6-NE; 7-N
        */
        //Flags
        public int iWindFlag;
        public int iFuelFlag; //if it turned off, use old fire initiation and spread routines.
        public int iDEMFlag;
        public int iFireRegimeFlag;
        public int iTSLFFlag;
        public double[] fInitiationProb = new double[5];
        public int cellSize;
        public int m_nFNOI;
        public int rows;
        public int cols;
        //<Add By Qia on Oct 9 2012>
        //Fire mortality parameters
        public double[,] fire_betavalues = new double[5, 3];
        public double[] fire_X2values = new double[5];
        //</Add By Qia on Oct 9 2012>
    }

    public class FinneyParam
    {
        public double[] ellipseAxisRatio = new double[6]; //w.r.t wind class
        public double fuelWeight;
        public double windWeight;
        public double TopoWeight;
        public double a;
        public double b;
        public double c;
        public double[,] spreadRate = new double[6, 6]; //ROS for flat area. slope class 0
        public double[,] ROS_low = new double[6, 6]; //slope class 1
        public double[,] ROS_moderate = new double[6, 6]; //slope class 2
        public double[,] ROS_high = new double[6, 6]; //slope class 3
        public double[,] ROS_extreme = new double[6, 6]; //slope class 4
    }

    public class FireFront
    {
        public int row;
        public int col;
        public int burnningTime;
        public int burnningLimit;
    }

    class CFIRE
    {
        public const double PI = 3.1415926;
        public const int __DEBUG = 0;
        public static double[] lifespan = new double[5] { (float)0.0, (float).2, (float).4, (float).7, (float).85 };

        public static int[] red = new int[Succession.Landispro.map8.maxLeg];
        public static int[] green = new int[Succession.Landispro.map8.maxLeg];
        public static int[] blue = new int[Succession.Landispro.map8.maxLeg];
        public static PILE pile;

        public int flag_regime_update;
        private int m_itr; //current iteration
        private StreamWriter m_LogFP;
        private string m_strTSLF;
        private string m_strFireOutputDirectory;
        private double m_dFSFactor;
        private double m_fSlope;
        private double m_fWind;
        private int m_iOriginRow;
        private int m_iOriginColumn;
        private int m_iMapRow;
        private int m_iMapColumn;
        private int m_lPredefinedFireSize; //in pixels
        private double m_fPredefinedDuration; //in minutes
        private double m_fFireSize; //in ha
        private int[] burnedCells = new int[DEFINE.MAX_LANDUNITS];
        private int m_FinneyDebugOutput;
        private FireParam m_fireParam = new FireParam();
        private FinneyParam m_finneyParam = new FinneyParam();
        private CFireSites m_pFireSites;
        private CFireRegimeUnits m_pFireRegimeUnits;
        private int m_DLLMode;
        private Succession.Landispro.map8 m_Map = new Succession.Landispro.map8(); //Map of damage for model run.
        private Succession.Landispro.map8 m_cummMap = new Succession.Landispro.map8();
        private Succession.Landispro.map8 m_InitiationMap = new Succession.Landispro.map8();
        private Succession.Landispro.map8 m_cummInitiationMap = new Succession.Landispro.map8();
        private int[] m_lptrIgnitionOccurance;
        private int[] m_iptrValidLandUnits;
        private int m_iNumLU;
        private int m_iWindIntensity;
        private int m_iWindDirectionIndex;
        private int[] m_iWindEventsLog = new int[42];
        private int FinneynumCohorts;
        private double m_maxWindRate;

        private double m_fRatio;
        private double m_fA; //minor axis length
        private double m_fB; //major axis length
        private double m_fC; //Squar root of B^2 - A^2

        private int length_BurnedCells;
        private bool m_FinneyCutoff;

        private int[][] m_checkMap;
        private double[][] m_fuelCostMap;
        private double[][] m_windCostMap;
        private double[][] m_minTimeMap;
        private double[][] m_actualROSMap;

        private int[] m_pIgnitionStatusArray;
        private int[] m_FRUAvailableCells = new int[255];

        private StochasticLib m_pStochastic;
        private Succession.Landispro.pdp m_pPDP;
        private Succession.Landispro.speciesattrs m_pSPECIESATTRS;
        private Succession.Landispro.sites m_pLAND;
        private List<CFinneyCell> FinneyList = new List<CFinneyCell>();
        //<Add By Qia on July 31 2012>
        private List<string> Fire_regime_files = new List<string>();
        private List<string> Fire_regime_gisfiles = new List<string>();
        //</Add By Qia on July 31 2012>

        
        private void InitializationColor()
        {
            (new int[] {0, 0, 100, 150, 200, 0, 0, 0, 150, 0, 150, 255, 80, 150, 255}).CopyTo(red, 0);
            (new int[] {0, 0, 0, 0, 0, 100, 150, 255, 0, 150, 150, 255, 80, 150, 255}).CopyTo(green, 0);
            (new int[] {0, 150, 0, 0, 0, 0, 0, 0, 150, 150, 0, 0, 80, 150, 255}).CopyTo(blue, 0);
        }

        //////////////////////////////////////////////////////////////////////

        // Construction/Destruction

        //////////////////////////////////////////////////////////////////////
        public CFIRE()
        {

        }

        public CFIRE(string strfn, int mode, Succession.Landispro.sites outsites, 
            Succession.Landispro.landunits outlus, Succession.Landispro.speciesattrs outsa, 
            Succession.Landispro.pdp ppdp, int nFNOI, string strOutput, int randSeed)
        {
            Dataset fpImg; //*
            double[] adfGeoTransform = new double[6]; //*            
            Gdal.AllRegister(); //*
            double[] wAdfGeoTransform = new double[6]; //*

            InitializationColor();

            StreamReader fp1 = new StreamReader(strfn.Trim());
            ReadParam(fp1);
            fp1.Close();
            //try
            //{
            //    Console.WriteLine(strfn + "!!");

            //    StreamReader fp = new StreamReader(strfn.Trim());
            //    ReadParam(fp);
            //    fp.Close();
            //    //using (StreamReader fp = new StreamReader(strfn))
            //    //{
            //    //    ReadParam(fp);
            //    //    fp.Close();
            //    //}
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine("FIRE: FIRE parameter file not found.");
            //     throw new Exception(e.Message);
            //}

            //add the main output directory in front of the subdirectory
            string s;
            s = string.Format("{0}\\Fire", strOutput);
            if (!Directory.Exists(s))
            {
                Directory.CreateDirectory(s);
            }

            s = string.Format("{0}\\Fire\\{1}", strOutput, m_fireParam.logFn);
            m_fireParam.logFn = string.Format("{0}", s);

            s = string.Format("{0}\\Fire\\{1}", strOutput, m_fireParam.yearlyFn);
            m_fireParam.yearlyFn = string.Format("{0}", s);

            s = string.Format("{0}\\Fire\\{1}", strOutput, m_fireParam.finalFn);
            m_fireParam.finalFn = string.Format("{0}", s);

            m_strTSLF = string.Format("{0}\\Fire\\TSLF", strOutput);
            m_strFireOutputDirectory = string.Format("{0}\\Fire\\", strOutput);

            //data initilization
            m_lptrIgnitionOccurance = null;
            m_iptrValidLandUnits = null;
            //m_pStochastic = new StochasticLib(time(0));
            if (randSeed != 0)
            {
                m_pStochastic = new StochasticLib(randSeed);
            }
            else
            {
                m_pStochastic = new StochasticLib(int.Parse(DateTime.Now.ToString()));
            }

            m_pPDP = ppdp;
            m_pSPECIESATTRS = outsa;
            m_pLAND = outsites;
            m_fireParam.m_nFNOI = nFNOI;
            m_DLLMode = mode;
            m_fireParam.rows = (int)outsites.numRows;
            m_fireParam.cols = (int)outsites.numColumns;

            //FireSites
            m_pFireSites = new CFireSites(m_fireParam.rows, m_fireParam.cols);

            //DEM
            /*
            FILE * fptemp;
            fptemp = LDfopen_0("testFire.txt","a");
            LDfprintf0(fptemp,"m_fireParam.iDEMFlag is %d\n",m_fireParam.iDEMFlag);
            LDfclose(fptemp);
            */

            if (m_fireParam.iDEMFlag == 1)
            {
                ReadDEM(m_fireParam.DEMDataFn);
            }

            //FireRegimeUnits
            m_pFireRegimeUnits = new CFireRegimeUnits(DEFINE.MAX_LANDUNITS, m_pStochastic);
            StreamReader fp = new StreamReader(m_fireParam.fireRegimeAttrFn);
            m_pFireRegimeUnits.read(fp);
            fp.Close();
            //try
            //{
            //    using (StreamReader fp = new StreamReader(m_fireParam.fireRegimeAttrFn))
            //    {
            //        m_pFireRegimeUnits.read(fp);
            //        fp.Close();
            //    }
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine("FIRE: fire regime attribute file not found.");
            //    Console.WriteLine(e.Message);
            //}

            m_pFireRegimeUnits.attach(m_pFireSites);
            if (m_fireParam.iFireRegimeFlag != 0)
            {
                /*fp = LDfopen(m_fireParam.fireRegimeDataFn, 2);
                if (fp == NULL)
                    errorSys("FIRE: fire regime GIS file not found.", STOP);*/
                
                if ((fpImg = (Dataset)Gdal.Open(m_fireParam.fireRegimeDataFn, Access.GA_ReadOnly)) == null) //* landtype.img
                {
                    throw new Exception("landtype img map input file not found."); //*
                }

                //YYF
                //this might be potentially problematic
                //if (fpImg.GetGeoTransform(adfGeoTransform)) //*
                //{
                //    for (int i = 0; i < 6; i++)
                //    {
                //        wAdfGeoTransform[i] = adfGeoTransform[i]; //*
                //    } //*

                //} //*
                fpImg.GetGeoTransform(adfGeoTransform);
                for (int k = 0; k < 6; k++)
                {
                    wAdfGeoTransform[k] = adfGeoTransform[k]; //*
                }
                //m_pFireRegimeUnits->readFireRegimeGIS(fp); 
                m_pFireRegimeUnits.readFireRegimeIMG(fpImg);
                fpImg.Dispose();
            }
            else
            {
                AttachLandUnitGIS(outsites);
            }
            m_pFireRegimeUnits.dispatch();
            m_pFireRegimeUnits.updateIGDensity(m_fireParam.cellSize);
            //initilize fire info in public data pool


            int i;
            int j;
            for (i = 1; i <= outsites.numRows; i++)
            {
                for (j = 1; j <= outsites.numColumns; j++)
                {
                    int tempID;
                    tempID = m_pFireSites[i, j].FRUIndex;
                    //Console.WriteLine(tempID);
                    m_pPDP.sTSLFire[i-1,j-1] = (short)m_pFireRegimeUnits[tempID].initialLastFire;
                }
            }
            m_cummInitiationMap.dim(outsites.numRows, outsites.numColumns);
            for (i = 1; i <= outsites.numRows; i++)
            {
                for (j = 1; j <= outsites.numColumns; j++)
                {
                    m_cummInitiationMap[(uint)i, (uint)j] = 0;
                }
            }
            //write TSLF
            WriteTSLF((int)outsites.numRows, (int)outsites.numColumns, 0, wAdfGeoTransform);
            m_FinneyDebugOutput = 0;
        }

        public void Dispose()
        {          
            m_pStochastic = null;
            m_pFireSites = null;
            m_pFireRegimeUnits = null;
        }

        public static int count;
        public void Activate(int itr, int[] freq, double[] wAdfGeoTransform)
        {
            StreamWriter logfile; //fire log file.
            double probForSite; //This is the probability of firethr. init. on a site.
  
            DateTime t1 = new DateTime();
            DateTime t2 = new DateTime();
            DateTime t3 = new DateTime();
            DateTime t4 = new DateTime();
            DateTime t5 = new DateTime();
            DateTime t6 = new DateTime();
            DateTime t7 = new DateTime();
            DateTime t8 = new DateTime();
            DateTime t9 = new DateTime();
            DateTime t10 = new DateTime();
            DateTime t11 = new DateTime();
            DateTime t12 = new DateTime();
            DateTime t13 = new DateTime();
            DateTime t14 = new DateTime();
            DateTime t15 = new DateTime();

            string str; //Character string.
            int i;
            int j;
            int k;
            int snr;
            int snc;
            int fireINTERV;
            int max_sites_examined = 0;
            int arealeft;
            int amtdamaged = 0;
            int numFRU = m_pFireRegimeUnits.number(); //Number of fire regime unit

            t1 = DateTime.Now;
            //Console.WriteLine("t1 is {0}", t1);
            for (i = 0; i < 42; i++)
            {
                m_iWindEventsLog[i] = 0;
            }

            t2 = DateTime.Now;
            //Console.WriteLine("t2 is {0}", t2);
            m_itr = itr;

            //Fill map.
            snr = m_pFireSites.numRows();
            snc = m_pFireSites.numColumns();
            m_iMapRow = snr;
            m_iMapColumn = snc;

            t3 = DateTime.Now;
            //Console.WriteLine("t3 is {0}", t3);
            if (m_fireParam.iFuelFlag == 2 || m_fireParam.iFuelFlag == 3)
            {
                FinneyInitilization();
            }

            t4 = DateTime.Now;
           // Console.WriteLine("t4 is {0}", t4);
            m_Map.dim((uint)snr, (uint)snc);
            for (i = 1; i <= snr; i++)
            {
                for (j = 1; j <= snc; j++)
                {
                    int tempID;
                    tempID = m_pFireSites[i, j].FRUIndex;
                    if (m_pFireRegimeUnits[tempID].Active())
				    {
                        m_Map[(uint)i, (uint)j] = 1;
                    }   
				    else
				    {
                        m_Map[(uint)i, (uint)j] = 0;
                    }
                }
            }

            t5 = DateTime.Now;
            //Console.WriteLine("t5 is {0}", t5);
            if (itr == 1)
		    {    
			    m_cummMap = m_Map;
		    }
            m_InitiationMap.dim((uint)snr, (uint)snc);

            t6 = DateTime.Now;
            //Console.WriteLine("t6 is {0}", t6);
            for (i = 1; i <= snr; i++)
		    {
			    for (j = 1;j <= snc; j++)
			    {    
				    m_InitiationMap[(uint)i, (uint)j] = 0;
			    }
		    }

            t7 = DateTime.Now;
            //Console.WriteLine("t7 is {0}", t7);
            //Setup fire parameters.
            //fseed(parameters.randSeed+itr*6);

            if (itr == 1)
            {
                FileStream fs = new FileStream(m_fireParam.logFn, FileMode.Create, FileAccess.Write, FileShare.Write);
                logfile = new StreamWriter(fs, System.Text.Encoding.Default);
                logfile.WriteLine("TIMESTEP,COL,ROW,TOTAREA,NUMSITES,NUMCOHORTS");
                for (int it = 0; it < numFRU; it++)
                {
                    logfile.WriteLine("FRU{0}", it);
                }
                logfile.WriteLine();
            }
            else
            {
                FileStream fs = new FileStream(m_fireParam.logFn, FileMode.Append, FileAccess.Write, FileShare.Write);
                logfile = new StreamWriter(fs, System.Text.Encoding.Default);
            }
          
            t8 = DateTime.Now;
            //Console.WriteLine("t8 is {0}", t8);
            int lIgChecking = 0;
            m_iNumLU = m_pFireRegimeUnits.number();

            m_lptrIgnitionOccurance = new int[m_iNumLU];
            m_iptrValidLandUnits = new int[m_iNumLU];
            m_pIgnitionStatusArray = new int[snr* snc + 1];
    		//<Add By Qia on Dec 26 2013>
	    	m_pFireSites.create_FireRegimeUnitsListByIndex();
		    //</Add By Qia on Dec 26 2013>
    		int totalCells = snr * snc;    
	    	for (i = 1; i <= totalCells; i++)
		    {
    			m_pIgnitionStatusArray[i] = 0;
	    	}

            t9 = DateTime.Now;
            //Console.WriteLine("t9 is {0}", t9);
            /* for each landtype, generate a poisson X which stands for how many 
            Ignitions in this iteration for each landtype
            */
            #if __FIREDEBUG__  
		    Console.Write("Ignitions: \n");  
            #endif
            j = m_iNumLU;
		    for (k = 0,i = 0;i<j;i++)
    		{
    			double IgDensity = 0;
                IgDensity = m_pFireRegimeUnits[i].m_fIgPoisson / 10 * m_pLAND.TimeStepFire;
                m_FRUAvailableCells[i] = m_pFireRegimeUnits.NumOfSites[i];   
			    if (IgDensity > 0F)
			    {
				    m_lptrIgnitionOccurance[i] = m_pStochastic.Poisson(IgDensity);
			    }
			    else
			    {
				    m_lptrIgnitionOccurance[i] = 0;
			    }
    
                #if __FIREDEBUG__  
			    Console.Write("{0:D} ",m_lptrIgnitionOccurance[i]);    
                #endif   

			    if (m_lptrIgnitionOccurance[i] > 0)
			    { 
				    m_iptrValidLandUnits[k++] = i;
			    }   
			    else
			    {  
				    m_iNumLU--;
			    }  
		    }

            t10 = DateTime.Now;
            //Console.WriteLine("t10 is {0}", t10);
            //cout << " t10 is " << ctime(&t10) << endl;
            #if __FIREDEBUG__
        		Console.Write("%\n");
            #endif
            /*
                if any of lIgnitionOccurance is >0
                consume one Ignition in any valid landtype randomly
            */
            LDPOINT p1 = new LDPOINT();    
		    while (m_iNumLU > 0) //Dead Lock in this loop //Qia on May 1st 2009   
		    {
                t11 = DateTime.Now;
                //Console.WriteLine("t11 is {0}", t11);
                count = 0;
                lIgChecking++;  
			    //randomly select k from 0 to iNumLU-1 //J.Yang use stochastic.uniform instead?
			    k = Succession.Landispro.system1.irand(0, m_iNumLU - 1);
                k = (int) m_pStochastic.IRandom(0, m_iNumLU - 1);
                /*
                randomly select a point from landunits.sitesWRTLandtypeListArray[iptrValidLandUnits[k]]
                sitesWRTLandtypeListArray is an array[numLU] in which every element is a pointer
                to a list, the list contains all points with respect to that landtype and size(how many sites)
                */
                p1 = Retrieve(m_iptrValidLandUnits[k]);
                //change relative lptrIgnitionOccurance[i] 
                //& if lptrIgnitionOccurance[i] == 0 then 
                //decrease iNumLU one and redim iptrValidLandUnits[]
                m_lptrIgnitionOccurance[m_iptrValidLandUnits[k]]--;
    			if (m_lptrIgnitionOccurance[m_iptrValidLandUnits[k]] == 0)
    			{
    				for (i = k;i<m_iNumLU - 1;i++)
	    			{
    					m_iptrValidLandUnits[i] = m_iptrValidLandUnits[i + 1];
	    			}
    				m_iNumLU--;
    			}
    			//check and perform disturbance
    			int p1y;
                int p1x;
                p1y = p1.y;
    			p1x = p1.x;
       			int tempID;
                tempID = m_pFireSites[p1y, p1x].FRUIndex;
                //Console.WriteLine("{0}, {1}, {2}",p1y, p1x, tempID);
                //YYF
                //May have some trouble without FUEL, with m_DLL
       			if (m_pFireRegimeUnits[tempID].Active())
    			{
    				//Calculating fire initiation probability
    				if ((m_DLLMode & Succession.Landispro.defines.G_FUEL) == 0) //using old fire initiation routine          
				    {
    					fireINTERV = m_pFireRegimeUnits[tempID].fireInterval;
					    if (fireINTERV == 0)
					    {    
						    probForSite = 1;
					    }  
					    else
					    {
                            //Console.WriteLine("{0} {1}", p1y, p1x);

                            probForSite = Math.Exp((double) m_pPDP.sTSLFire[p1y-1,p1x-1] * ((double) - 1 / fireINTERV)); //10*m_pLAND->TimeStep;
					    }
    					// probForSite is the reliability probability: has life time as least t
    					// say X is life time of system, t = lastFire;
    					// probForSite = p[X>=t]
    					// using bernoulli try Bernoulli(p) p = probForSite
    					// 1 success means no fire till this time
    					// 0 failure means fire occure till now
    				}
				    else
				    {
					    //using FF loading class
					    //FUEL module has to be turned on to use fine fuel
					    i = (int) m_pPDP.cFineFuel[p1y,p1x];
					    if (i >= 1 && i <= 5)
					    {
						    probForSite = (1 - m_fireParam.fInitiationProb[i - 1]); //10*m_pLAND->TimeStep;// Bu, Rencang April 28, 2009
					    }
					    else
					    {
						    probForSite = 1;
					    }
					    // 1 success means no fire till this time
					    // 0 failure means fire occure till now
				    }

                    t12 = DateTime.Now;
                    //Console.WriteLine("t12 is {0}", t12);
                    m_fWind = 0.0;
				    m_fSlope = 0.0;
				    m_dFSFactor = 0.0;
				    if (probForSite > 1)
				    {
					    Console.Write("1 - fire initiation probability (probForSite: {0:f}) is larger than 1",probForSite);
					    Console.Write(" at site {0:D} {1:D} \n", p1y,p1x);
                        probForSite = 1;
				    }
				    if (probForSite< 0)
				    {
					    Console.Write("1 - fire initiation probability (probForSite: {0:f}) is less than 0",probForSite);
					    Console.Write(" at site {0:D} {1:D} \n", p1y,p1x);
                        probForSite = 0;
				    }
				    bool bernoulliProb = m_pStochastic.Bernoulli(probForSite);
    
				    if (bernoulliProb == false)
				    {
					    // start a fire		
   					    //update initiation map
					    m_InitiationMap[(uint)p1y, (uint)p1x] = 1;
					    m_cummInitiationMap[(uint)p1y, (uint)p1x]++;
					    if (m_fireParam.iFuelFlag == 2 || m_fireParam.iFuelFlag == 3)
					    {
						    //amtdamaged = (long) fireSpread(p1y,p1x);
						    //change it to Finney method
						    m_iOriginRow = p1y;
						    m_iOriginColumn = p1x;
						    amtdamaged = FinneySpread();
					    }
					    else if (m_fireParam.iFuelFlag == 1)
					    {
						    //percolation method
						    amtdamaged = (int) fireSpread(p1y, p1x);
					    }
					    else
					    {
                            tempID = m_pFireSites[p1y, p1x].FRUIndex;
						    double tempMFS;
                            double tempSTD;
                            tempMFS = m_pFireRegimeUnits[tempID].m_fMFS;
						    tempSTD = m_pFireRegimeUnits[tempID].m_fFireSTD;
						    arealeft = fireSize(tempMFS, tempSTD);
                            amtdamaged = (int) disturb(p1y, p1x, (int) arealeft, m_Map, logfile, probForSite, itr);
					    }
				    }
				    else
				    {
					    //dummy
				    }
			    }
			    //cout << "m_iNumLU = " << m_iNumLU << endl;
			    //count++;
			    //cout << "count =" << count << endl;
		    }

            t13 = DateTime.Now;
            //Console.WriteLine("t13 is {0}", t13);
            //Add data to cummMap
		    //and change TSLF
		    for (i = 1;i <= snr;i++)
		    {
			    for (j = 1;j <= snc;j++)
			    {
				    if (m_Map[(uint)i, (uint)j] >= 2)
				    {
					    m_cummMap[(uint)i, (uint)j] = Math.Max(m_Map[(uint)i, (uint)j],m_cummMap[(uint)i, (uint)j]);
    				    //J.Yang should be max(m_cummMap(i,j), m_Map(i,j))
					    m_pPDP.sTSLFire[i-1,j-1] = 0;
				    }
				    else
				    {
					    m_pPDP.sTSLFire[i-1,j-1] += (short)m_pLAND.TimeStepFire;
				    }
			    }
		    }

            t14 = DateTime.Now;
            //Console.WriteLine("t14 is {0}", t14);
		    if (((itr % freq[1]) == 0) && (freq[1] <= m_pLAND.TimeStepFire) || (itr* m_pLAND.TimeStepFire == freq[1]) && (freq[1] >= 0))
		    {
			    //Write map output file.
			    str = string.Format("fire damage assessment for year {0:D}.", itr* m_pLAND.TimeStepFire);
                m_Map.setHeader(m_pLAND.Header);
			    m_Map.rename(str);
			    for (i = 0;i< Succession.Landispro.map8.maxLeg;i++)
			    {
				    m_Map.assignLeg((uint)i,"");
			    }
			    m_Map.assignLeg(0,"NonActive");
			    m_Map.assignLeg(1,"No Fires");
			    m_Map.assignLeg(2,"Class 1");
			    m_Map.assignLeg(3,"Class 2");
			    m_Map.assignLeg(4,"Class 3");
			    m_Map.assignLeg(5,"Class 4");
			    m_Map.assignLeg(6,"Class 5");
			    str = string.Format("{0}{1:D}", m_fireParam.yearlyFn, itr* m_pLAND.TimeStepFire);
                m_Map.CellSize = m_fireParam.cellSize;
			    //double wAdfGeoTransform[6] = { 0.00, m_fireParam.cellSize, 0.00, 600.00, 0.00, -m_fireParam.cellSize };//*
			    double nodata = 0;
                m_Map.write(str, red, green, blue);
			    WriteInitiationMap(snr, snc, itr, wAdfGeoTransform);
                //write time-since-last-fire map
                WriteTSLF(snr, snc, itr, wAdfGeoTransform);
		    }
            logfile.Close();
		    if (itr == m_fireParam.m_nFNOI)
		    {
			    //Write cumulative map output file.
			    str = "Cumulative fire damage assessment.";
			    m_cummMap.setHeader(m_pLAND.Header);
			    m_cummMap.rename(str);
			    for (i = 0;i<Succession.Landispro.map8.maxLeg;i++)
			    {
				    m_cummMap.assignLeg((uint)i,"");
			    }
			    m_cummMap.assignLeg(0,"NonActive");
			    m_cummMap.assignLeg(1,"No Fires");
			    m_cummMap.assignLeg(2,"Class 1");
			    m_cummMap.assignLeg(3,"Class 2");
			    m_cummMap.assignLeg(4,"Class 3");
			    m_cummMap.assignLeg(5,"Class 4");
			    m_cummMap.assignLeg(6,"Class 5");
			    str = string.Format("{0}", m_fireParam.finalFn);
                m_cummMap.CellSize = m_fireParam.cellSize;
			    //double wAdfGeoTransform[6] = { 0.00, m_fireParam.cellSize, 0.00, 600.00, 0.00, -m_fireParam.cellSize };//*
			    double nodata = 0;
                m_cummMap.write(str, red, green, blue);
                WriteCummInitiationMap(snr, snc, itr, wAdfGeoTransform);
		    }
            t15 = DateTime.Now;
            //Console.WriteLine(t15);
            PrintWindLog();
		    if (m_fireParam.iFuelFlag == 2 || m_fireParam.iFuelFlag == 3)
		    {
			    FinneyFreeMemory();
		    }
		    m_pIgnitionStatusArray = null;
            m_lptrIgnitionOccurance = null;
            m_iptrValidLandUnits = null;
	    }

        public void ReadParam(StreamReader infile)
        {
            int i;
            int j;
            string inString = infile.ReadLine();
            inString = infile.ReadLine();
            inString = infile.ReadLine();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading new-fire-algorithms-flag.");
            string[] sarray = inString.Split('#');
            m_fireParam.iFuelFlag = int.Parse(sarray[0]);

            inString = infile.ReadLine();
            inString = infile.ReadLine();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading wind flag.");
            sarray = inString.Split('#');
            m_fireParam.iWindFlag = int.Parse(sarray[0]);

            inString = infile.ReadLine();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading DEM flag.");
            sarray = inString.Split('#');
            m_fireParam.iDEMFlag = int.Parse(sarray[0]);

            inString = infile.ReadLine();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading fire regime flag.");
            sarray = inString.Split('#');
            m_fireParam.iFireRegimeFlag = int.Parse(sarray[0]);

            inString = infile.ReadLine();
            inString = infile.ReadLine();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading TSLF flag.");
            sarray = inString.Split('#');
            m_fireParam.iTSLFFlag = int.Parse(sarray[0]);

            inString = infile.ReadLine();
            inString = infile.ReadLine();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error reading in Initiation Probability.");
            sarray = inString.Split('#');
            sarray = sarray[0].Split(' ');
            for (i = 0; i < 5; i++)
            {
                m_fireParam.fInitiationProb[i] = double.Parse(sarray[i]);
            }

            inString = infile.ReadLine();
            inString = infile.ReadLine();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading SpreadProbability.");
            sarray = inString.Split('#');
            m_fireParam.fSpreadProb = double.Parse(sarray[0]);

            inString = infile.ReadLine();
            inString = infile.ReadLine();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading Wind coefficient.");
            sarray = inString.Split('#');
            m_fireParam.fCoeff1 = double.Parse(sarray[0]);

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading Topography coefficient.");
            sarray = inString.Split('#');
            m_fireParam.fCoeff2 = double.Parse(sarray[0]);

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading Predefined fire size distribution coefficient.");
            sarray = inString.Split('#');
            m_fireParam.fCoeff3 = double.Parse(sarray[0]);

            /* Reading prevaling fire regime
            */
            inString = infile.ReadLine();
            inString = infile.ReadLine();
            inString = infile.ReadLine();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading Percentage of Non wind events.");
            sarray = inString.Split('#');
            m_fireParam.iNonWindPercent = int.Parse(sarray[0]);

            inString = infile.ReadLine();
            inString = infile.ReadLine();
            inString = infile.ReadLine();
            inString = infile.ReadLine();
            inString = infile.ReadLine();
            inString = infile.ReadLine();

            for (i = 1; i <= 8; i++)
            {
                if ((inString = infile.ReadLine()) == null)
                    throw new Exception("Error reading in Cummulative probabilities of wind classes.");
                sarray = inString.Split('#');
                sarray = System.Text.RegularExpressions.Regex.Split(sarray[2].Trim(), @"\s+");
                for (int k = 1; k <= 5; k++)
                {
                    m_fireParam.iCummWindClass[i * k - 1] = int.Parse(sarray[k - 1]);
                }
            }

            inString = infile.ReadLine();
            inString = infile.ReadLine();
            inString = infile.ReadLine();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading file name for fire Regime Attributes");
            sarray = inString.Split('#');
            m_fireParam.fireRegimeAttrFn = sarray[0].Trim();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading file name for fire Regime data");
            sarray = inString.Split('#');
            m_fireParam.fireRegimeDataFn = sarray[0].Trim();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading file name for DEM data");
            sarray = inString.Split('#');
            m_fireParam.DEMDataFn = sarray[0].Trim();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading flag for updating fire regime");
            sarray = inString.Split('#');
            m_fireParam.File_regime_change = sarray[0].Trim();
            if (!m_fireParam.File_regime_change.Equals("N/A"))
            {
                flag_regime_update = 0;
            }
            else if (!m_fireParam.File_regime_change.Equals("0"))
            {
                flag_regime_update = 0;
            }
            else
            {
                flag_regime_update = 1;
            }

            inString = infile.ReadLine();
            inString = infile.ReadLine();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading file name for iterationaly fire information output");
            sarray = inString.Split('#');
            m_fireParam.yearlyFn = sarray[0].Trim();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading file name for final fire information output");
            sarray = inString.Split('#');
            m_fireParam.finalFn = sarray[0].Trim();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading file name for log fire information output");
            sarray = inString.Split('#');
            m_fireParam.logFn = sarray[0].Trim();

            inString = infile.ReadLine();
            inString = infile.ReadLine();
            inString = infile.ReadLine();

            if ((inString = infile.ReadLine()) == null)
                throw new Exception("Error in reading cell size.");
            sarray = inString.Split('#');
            m_fireParam.cellSize = int.Parse(sarray[0]);

            inString = infile.ReadLine();
            inString = infile.ReadLine();
            inString = infile.ReadLine();

            for (i = 0; i < 5; i++)
            {
                if ((inString = infile.ReadLine()) == null)
                    throw new Exception("Error reading in Cummulative probabilities of wind classes.");
                sarray = System.Text.RegularExpressions.Regex.Split(inString.Trim(), @"\s+");

                for (j = 0; j < 3; j++)
                {
                    m_fireParam.fire_betavalues[i,j] = double.Parse(sarray[j+1]);
                }
                m_fireParam.fire_X2values[i] = double.Parse(sarray[5]);
            }

            //YYF
            //Didn't find the file and not clear about its format, may need to defined later.
            //if (2 == m_fireParam.iFuelFlag || 3 == m_fireParam.iFuelFlag) //using Finney spread method 2 duration. 3 fire size
            //{
            //    //use Finney spread method, it needs another file for parameters used in Finney method
            //    if (fscanc(infile, "%s", m_fireParam.FinneyParamFN) != 1)
            //    {
            //        errorSys("Error in reading parameter file name for using Finney spread method", STOP);
            //    }
            //    //read in finney parameter space
            //    FILE fp;
            //    fp = LDfopen(m_fireParam.FinneyParamFN, 1);
            //    if (fp == null)
            //    {
            //        errorSys("FIRE: FIRE Finney parameter file not found.", STOP);
            //    }
            //    for (i = 0; i < 6; i++)
            //    {
            //        for (j = 0; j < 6; j++)
            //        {
            //            if (fscanc(fp, "%f", m_finneyParam.spreadRate[i][j]) != 1)
            //            {
            //                errorSys("Error reading in fire spread rate for using Finney spread method.", STOP);
            //            }
            //        }
            //    }
            //    for (i = 0; i < 6; i++)
            //    {
            //        if (fscanc(fp, "%f", m_finneyParam.ellipseAxisRatio[i]) != 1)
            //        {
            //            errorSys("Error reading in ellipse axis ratio for using Finney spread method.", STOP);
            //        }
            //    }
            //    if (fscanc(fp, "%f", m_finneyParam.fuelWeight) != 1)
            //    {
            //        errorSys("Error reading in fuel weight for using Finney spread method.", STOP);
            //    }
            //    if (fscanc(fp, "%f", m_finneyParam.windWeight) != 1)
            //    {
            //        errorSys("Error reading in wind weight for using Finney spread method.", STOP);
            //    }
            //    if (fscanc(fp, "%f", m_finneyParam.TopoWeight) != 1)
            //    {
            //        errorSys("Error reading in topo weight for using Finney spread method.", STOP);
            //    }
            //    if (1 == m_fireParam.iDEMFlag)
            //    {
            //        for (i = 0; i < 6; i++)
            //        {
            //            for (j = 0; j < 6; j++)
            //            {
            //                if (fscanc(fp, "%f", m_finneyParam.ROS_low[i][j]) != 1)
            //                {
            //                    errorSys("Error reading in ROS for low steepness (slope class 1) area.", STOP);
            //                }
            //            }
            //        }
            //        for (i = 0; i < 6; i++)
            //        {
            //            for (j = 0; j < 6; j++)
            //            {
            //                if (fscanc(fp, "%f", m_finneyParam.ROS_moderate[i][j]) != 1)
            //                {
            //                    errorSys("Error reading in ROS for moderate steepness (slope class 2) area.", STOP);
            //                }
            //            }
            //        }
            //        for (i = 0; i < 6; i++)
            //        {
            //            for (j = 0; j < 6; j++)
            //            {
            //                if (fscanc(fp, "%f", m_finneyParam.ROS_high[i][j]) != 1)
            //                {
            //                    errorSys("Error reading in ROS for high steepness (slope class 3) area.", STOP);
            //                }
            //            }
            //        }
            //        for (i = 0; i < 6; i++)
            //        {
            //            for (j = 0; j < 6; j++)
            //            {
            //                if (fscanc(fp, "%f", m_finneyParam.ROS_extreme[i][j]) != 1)
            //                {
            //                    errorSys("Error reading in ROS for extreme steepness (slope class 4) area.", STOP);
            //                }
            //            }
            //        }
            //    }
            //    LDfclose(fp);
            //}
        }

        public void AttachLandUnitGIS(Succession.Landispro.sites outsites)
        {
            int numFRU;
            numFRU = m_pFireRegimeUnits.number();
            for (uint i = outsites.numRows; i > 0; i--)
            {
                for (uint j = 1; j <= outsites.numColumns; j++)
                {
                    int tempID = outsites.locateLanduPt(i, j).LtID;
                    //original landis4.0: int tempID = outsites->operator ()(i,j)->landUnit->ltID;
                    //Changed By Qia on Oct 13 2008
                    //Console.WriteLine(tempID);
                    if (tempID <= numFRU)
                    { //<Add By Qia on Nov 24 2008>
                        m_pFireSites.BefStChg((int)i, (int)j);
                        //</Add By Qia on Nov 24 2008>
                        m_pFireSites[(int)i, (int)j].FRUIndex = tempID;
                        //<Add By Qia on Nov 24 2008>
                        m_pFireSites.AftStChg((int)i, (int)j);
                        //</Add By Qia on Nov 24 2008>
                    }
                    else
                    {
                        throw new Exception("fire regime unit is not consistent with the land unit.");
                    }
                }
            }
        }

        public int fireSize(double MFS, double STD)
        {
            //generate random fire size based on lognormal distribution
            double size;
            double VAR;
            int numSites; //Square meters on a singular site.
            int siteSize;
            //if x is fire size following lognormal distribution with mean MFS and variance as VAR
            //then log(x) follows normal distribution with      
            //mean = 2logMFS - 1/2log(VAR+MFS square)
            //variance = log(VAR+MFS square) - 2logMFS
            double mean;
            double @var;
            double std;
            numSites = m_pFireSites.numRows() * m_pFireSites.numColumns();
            siteSize = m_fireParam.cellSize * m_fireParam.cellSize;
            VAR = STD * STD;
            mean = 2.0 * Math.Log(MFS) - 0.5 * Math.Log(VAR + MFS * MFS);
            @var = Math.Log(1.0 * (VAR + MFS * MFS)) - 2.0 * Math.Log(MFS);
            std = Math.Sqrt(@var);
            while (true) //standard disturbance
            {
                size = m_pStochastic.Normal(mean, std);
                if (size == double.NaN)
                {
                    size = mean;
                    break;
                }
                size = Math.Exp(size); //unit: hectare. 1 ha = 10,000 squre meters
                size = (int)10000 * size / siteSize;
                if ((size <= numSites) && (size >= 1))
                {
                    break;
                }
            }
            return (int)size;
        }

        public int disturb(int row, int col, int totArea, Succession.Landispro.map8 m, StreamWriter logfile, double x, int itr)
        //This will activate a fire disturbance at row r and col c.  The size of
        //the fire will be totArea where totArea is in number of pixels.
        //The output map is contained in m and the output file is logfile.
        //area is the total area consumed by fire
        {
            LDPOINT p1 = new LDPOINT();
            LDPOINT p2 = new LDPOINT();
            Succession.Landispro.map8 dist = new Succession.Landispro.map8(); //This will be marked true if an area is disturbed.
            int numSites = 0; //number of landunits
            int numCohorts = 0;
            int fireClass = 0;
            int singleTry = 0;
            int stopFlag = 0;
            int fireINTERV;
            int numLU;

            int[] land = new int[DEFINE.MAX_LANDUNITS]; //fire on different landunits
            double siteProb; //verified new probability on each site
            int[] FS = new int[DEFINE.MAX_LANDUNITS]; // fire size for each landunits
            int[] landOnFire = new int[DEFINE.MAX_LANDUNITS]; // if fire on this landunits, it's 1, otherwise, 0
            int j;
            int k;
            double dTSLF; //TimeSinceLastFire
            int nCKilled;

#if __FIREDEBUG__
		    string str;
		    StreamWriter txtFile;
		    str = string.Format("{0}", m_fireParam.logFn);
		    str = string.Format("{0}.txt", str);
            txtFile = new StreamWriter(str)
            txtFile.WriteLine("{0}, {1}, {2}", itr * m_pLAND.TimeStepFire, m_pFireSites[row, col].pFireRegimeUnit.name, m_pPDP.sTSLFire[row,col]);
#endif
            numLU = m_pFireRegimeUnits.number();
            for (int t = 0; t < numLU; t++)
            {
                land[t] = 0;
                landOnFire[t] = 0;
                FS[t] = 0;
            }
            pile.reset();
            dist.dim((uint)m_pFireSites.numRows(), (uint)m_pFireSites.numColumns());
            dist.fill(0);
            dist[(uint)row, (uint)col] = 1;
            p1.x = col;
            p1.y = row;
            numCohorts = damage(p1, ref fireClass);
            m[(uint)row, (uint)col] = (byte)(1 + fireClass);
            //J.Yang put the fireClass into the map

            int tempID;
            tempID = m_pFireSites[p1.y, p1.x].FRUIndex;
            land[tempID]++;
            landOnFire[tempID] = 1;
            FS[tempID] = fireSize(m_pFireRegimeUnits[tempID].m_fMFS, m_pFireRegimeUnits[tempID].m_fFireSTD);
            totArea = FS[tempID];
            numSites = 1;
            //now totArea means that the firesize for the 1st landtype(start point)

            if (p1.x - 1 > 0 && dist[(uint)p1.y, (uint)p1.x - 1]==0)
            {
                p2.x = p1.x - 1;
                p2.y = p1.y;
                dist[(uint)p2.y, (uint)p2.x] = 1;
                tempID = m_pFireSites[p2.y, p2.x].FRUIndex;
                if (m_pFireRegimeUnits[tempID].Active())
			    {
                    pile.push(p2);
                }
            }

            if (p1.x + 1 <= m_pLAND.numColumns && dist[(uint)p1.y, (uint)p1.x + 1] == 0)
            {
                p2.x = p1.x + 1;
                p2.y = p1.y;
                dist[(uint)p2.y, (uint)p2.x] = 1;
                tempID = m_pFireSites[p2.y, p2.x].FRUIndex;
                if (m_pFireRegimeUnits[tempID].Active())
			    {
                    pile.push(p2);
                }
            }

            if (p1.y - 1 > 0 && dist[(uint)p1.y - 1, (uint)p1.x] == 0)
            {
                p2.x = p1.x;
                p2.y = p1.y - 1;
                dist[(uint)p2.y, (uint)p2.x] = 1;
                tempID = m_pFireSites[p2.y, p2.x].FRUIndex;
                if (m_pFireRegimeUnits[tempID].Active())
			    {
                    pile.push(p2);
                }
            }

            if (p1.y + 1 <= m_pLAND.numRows && dist[(uint)p1.y + 1, (uint)p1.x] == 0)
            {
                p2.x = p1.x;
                p2.y = p1.y + 1;
                dist[(uint)p2.y, (uint)p2.x] = 1;
                tempID = m_pFireSites[p2.y, p2.x].FRUIndex;
                if (m_pFireRegimeUnits[tempID].Active())
			    {
                    pile.push(p2);
                }
            }

            while (!pile.isEmpty() && stopFlag == 0)
            {
                p1 = pile.pull();
                singleTry++;
                nCKilled = 0;
                j = tempID = m_pFireSites[p1.y, p1.x].FRUIndex;
                fireINTERV = m_pFireRegimeUnits[tempID].fireInterval;
                dTSLF = m_pPDP.sTSLFire[p1.y-1,p1.x-1];
                if (fireINTERV == 0)
                {
                    siteProb = 1;
                }
                else
                {
                    siteProb = Math.Exp(dTSLF * ((double)-1 / fireINTERV)); //10*m_pLAND->TimeStep; Bu, Rencang, April 18, 2009
                }

                //modified siteProbability based on how much sites already burned
                if (landOnFire[j] == 1) //it's fire spreading
                {
                    //siteProb = exp(log(FS[j]- land[j] + 1)*log(siteProb));
                    if (FS[j] > land[j])
                    {
                        siteProb = 0;
                    }
                    else
                    {
                        siteProb = 1;
                    }
                }
                else
                {
                    //if ignitionChecking is out, siteProb = 1. it means that fire cannot be ignited
                    if (m_lptrIgnitionOccurance[j] == 0)
                    {
                        siteProb = 1;
                    }
                    else
                    { //consume one ignition
                        //decrease m_lptrIgnitionOccurance[j] by 1 and related work			
                        m_lptrIgnitionOccurance[j]--;
                        if (m_lptrIgnitionOccurance[j] == 0)
                        {
                            for (k = 0; k < m_iNumLU; k++)
                            {
                                if (m_iptrValidLandUnits[k] == j)
                                {
                                    break;
                                }
                            }
                            for (int i = k; i < m_iNumLU - 1; i++)
                            {
                                m_iptrValidLandUnits[i] = m_iptrValidLandUnits[i + 1];
                            }
                            m_iNumLU--;
                        }
                    }
                }

                if (siteProb > 1)
                {
                    Console.Write("1 - fire spread probability (siteProb: {0:f}) is larger than 1", siteProb);
                    siteProb = 1;
                }

                if (siteProb < 0)
                {
                    Console.Write("1 - fire spread probability (siteProb: {0:f}) is less than 0", siteProb);
                    siteProb = 0;
                }

                if (m_pStochastic.Bernoulli(siteProb) == false)
                { // either fire spreading or fire initiation
                    nCKilled = damage(p1, ref fireClass);
                    m[(uint)p1.y, (uint)p1.x] = (byte)(1 + fireClass);
                    //J.Yang put fireClass into the map
                    land[j]++;
                    numSites++;
                    if (landOnFire[j] == 0)
                    { // an initiation
                        landOnFire[j] = 1;
                        FS[j] = fireSize(m_pFireRegimeUnits[j].m_fMFS, m_pFireRegimeUnits[j].m_fFireSTD);
                    }
                }
                else
                {
                    pile.push(p1);
                }

                numCohorts += nCKilled;
                if (nCKilled != 0)
                {
                    singleTry = 0;
                    m[(uint)p1.y, (uint)p1.x] = (byte)(1 + fireClass);
                    if (p1.x - 1 > 0 && dist[(uint)p1.y, (uint)p1.x - 1] == 0)
                    {
                        p2.x = p1.x - 1;
                        p2.y = p1.y;
                        dist[(uint)p2.y, (uint)p2.x] = 1;
                        tempID = m_pFireSites[p2.y, p2.x].FRUIndex;
                        if (m_pFireRegimeUnits[tempID].Active())
					    {
                            pile.push(p2);
                        }
                    }

                    if (p1.x + 1 <= m_pLAND.numColumns && dist[(uint)p1.y, (uint)p1.x + 1] == 0)
                    {
                        p2.x = p1.x + 1;
                        p2.y = p1.y;
                        dist[(uint)p2.y, (uint)p2.x] = 1;
                        tempID = m_pFireSites[p2.y, p2.x].FRUIndex;
                        if (m_pFireRegimeUnits[tempID].Active())
					    {
                            pile.push(p2);
                        }
                    }

                    if (p1.y - 1 > 0 && dist[(uint)p1.y - 1, (uint)p1.x] == 0)
                    {
                        p2.x = p1.x;
                        p2.y = p1.y - 1;
                        dist[(uint)p2.y, (uint)p2.x] = 1;
                        tempID = m_pFireSites[p2.y, p2.x].FRUIndex;
                        if (m_pFireRegimeUnits[tempID].Active())
					    {
                            pile.push(p2);
                        }
                    }

                    if (p1.y + 1 <= m_pLAND.numRows && dist[(uint)p1.y + 1, (uint)p1.x] == 0)
                    {
                        p2.x = p1.x;
                        p2.y = p1.y + 1;
                        dist[(uint)p2.y, (uint)p2.x] = 1;
                        tempID = m_pFireSites[p2.y, p2.x].FRUIndex;
                        if (m_pFireRegimeUnits[tempID].Active())
					    {
                            pile.push(p2);
                        }
                    }
                } //end if nCKilled

                stopFlag = 1;
                for (k = 0; k < numLU; k++)
                {
                    if (FS[k] > land[k])
                    {
                        stopFlag = 0;
                        break;
                    }
                }

                if (singleTry > DEFINE.MAX_TRY)
                {
                    stopFlag = 2;
                    break;
                }
            } //end while

            //add to fire log file
            logfile.Write("{0}, ",itr * m_pLAND.TimeStepFire);
            logfile.Write("{0}, {1}, {2}, ", col, row, totArea);
            logfile.Write("{0}, {1}, {2}, ", numSites, numCohorts);
            for (int i = 0; i < numLU; i++)
            {
                logfile.WriteLine(", {0}", land[i]);
            }
            logfile.WriteLine();
#if __FIREDEBUG__
        txtFile.WriteLine("{0}", stopFlag);
		txtFile.Close(); 
#endif
            return numSites;
        }

        public int damage(LDPOINT p, ref int fireClass)
        //This will cause damage from the fire at POINT p.  It will return the
        //actual number of cohorts killed.  Class is the return value of the fire
        //class.
        //Note p.y is row, p.x is col
        {
            int i;
            int j;
            int count70;
            double tmpBiomass;
            double tmpCarbon;
            uint specAtNum = m_pSPECIESATTRS.NumAttrs;
            int tempID = m_pFireSites[p.y, p.x].FRUIndex;
            //<Add By Qia on Aug 03 2009>
            double tmpDQ;
            double TmpMortality;
            double DeadTree;

            Succession.Landispro.site siteptr = m_pLAND[(uint)p.y, (uint)p.x];
            Succession.Landispro.landunit l;
            l = m_pLAND.locateLanduPt((uint)p.y, (uint)p.x);
            tmpDQ = 0F;

            //</Add By Qia on Aug 03 2009>
            if (!m_pFireRegimeUnits[tempID].Active())
		    {
                return 0;
            }

            CFireRegimeUnit pFRU = m_pFireRegimeUnits[tempID];

            //Determine fire class.
            //YYF, may have error because of m_DLLMode with Fule module
            if (m_fireParam.iFuelFlag == 0 || (m_DLLMode & Succession.Landispro.defines.G_FUEL) == 0)
            {
                fireClass = 0;
                for (i = 4; i >= 0; i--)
                {
                    if (pFRU.fireCurve[i] <= m_pPDP.sTSLFire[p.y-1,p.x-1])
                    {
                        fireClass = Math.Max(fireClass, pFRU.fireClass[i]);
                    }
                }
            }
            else
            {
                fireClass = (int)m_pPDP.cFireIntensityClass[p.y,p.x];
            }

            // J.Yang need to consider when windDLL is turned on
            //how to simulate wind effect on fire damage	
            if ((m_DLLMode & Succession.Landispro.defines.G_WIND) != 0)
            {
                if (m_pPDP.sTSLFire[p.y-1,p.x-1] > m_pPDP.sTSLWind[p.y, p.x])
                {
                    for (i = 4; i >= 0; i--)
                    {
                        if (pFRU.windCurve[i] <= m_pPDP.sTSLWind[p.y, p.x])
                        {
                            fireClass = Math.Max(fireClass, pFRU.windClass[i]);
                        }
                    }
                }
            }
            if (fireClass <= 0)
            {
                return 0;
            }



            //Perform fire damage.   
            int numCohorts = 0;
            int shade = 0;
            //<Add By Qia on Nov 24 2008>
            m_pLAND.BefStChg(p.y, p.x);
            //</Add By Qia on Nov 24 2008>
            for (i = 1; i <= specAtNum; i++)
            {
                //<Add By Qia on Aug 03 2009>
                if (siteptr.specAtt(i).SpType >= 0)
                {
                    for (j = 1; j <= siteptr.specAtt(i).Longevity / m_pLAND.SuccessionTimeStep; j++)
                    {
                        int tolerance_index = siteptr.specAtt(i).FireTolerance;

                        int severity_index = fireClass;
                        if (tolerance_index < 1 || tolerance_index > 5)
                        {
                            throw new Exception("fire tolerance index error.");
                        }
                        if (severity_index < 1 || severity_index > 5)
                        {
                            throw new Exception("fire severity index error.");
                        }
                        double beta1 = m_fireParam.fire_betavalues[tolerance_index - 1, 0];
                        double beta2 = m_fireParam.fire_betavalues[tolerance_index - 1, 1];
                        double beta3 = m_fireParam.fire_betavalues[tolerance_index - 1,2];
                        double X_value = m_fireParam.fire_X2values[severity_index - 1];
                        double tempGrow = m_pLAND.GetGrowthRates(i, j, l.LtID);
                        double prob_burn_param = beta1 + beta2 * tempGrow + beta3 * X_value;
                        double prob_burn = Math.Pow((1.0 + Math.Exp(0.0 - prob_burn_param)), -1.0);
                        int tree_num_agecohort = (int)siteptr.SpecieIndex(i).getTreeNum(j, i);
                        DeadTree = tree_num_agecohort * prob_burn;
                        siteptr.SpecieIndex(i).setTreeNum(j, i, (int)(tree_num_agecohort - DeadTree));

                        if ((siteptr.specAtt(i).MaxSproutAge / m_pLAND.SuccessionTimeStep >= j) & (siteptr.specAtt(i).MinSproutAge / m_pLAND.SuccessionTimeStep <= j))

                        {
                            siteptr.SpecieIndex(i).TreesFromVeg += (int)(DeadTree * siteptr.specAtt(i).VegReprodProb);
                        }
                    }
                }
            }
            //<Add By Qia on Nov 24 2008>
            m_pLAND.AftStChg(p.y, p.x);
            //</Add By Qia on Nov 24 2008>
            if (numCohorts != 0)
            {
                m_pPDP.sTSLFire[p.y-1,p.x-1] = 0;
            }
            return numCohorts;
        }

        //YYF
        //May have error: Don't find DEM file
        public void ReadDEM(string fileName)
        {
            try
            {
                using (StreamReader fp = new StreamReader(fileName))
                {
                    String inString;
                    for (int i = m_fireParam.rows; i >= 1; i--)
                    {
                        for (int j = 1; j <= m_fireParam.cols; j++)
                        {
                            if ((inString = fp.ReadLine()) == null)
                            {
                                throw new Exception("FIRE: Error reading in topo value.");
                            }
                            //<Add By Qia on Nov 24 2008>
                            m_pFireSites.BefStChg(i, j);
                            //</Add By Qia on Nov 24 2008>
                            m_pFireSites[i, j].DEM = int.Parse(inString);
                            //<Add By Qia on Nov 24 2008>
                            m_pFireSites.AftStChg(i, j);
                            //</Add By Qia on Nov 24 2008>
                        }
                    }
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("FIRE: DEM file not found.");
                Console.WriteLine(e.Message);
            }
        }

        //Add by YYF
        //Deep copy of FireFront
        public void DeepClone(ref FireFront[] a, FireFront[] b)
        {
            for (int i=0; i<b.Length; i++)
            {
                a[i].row = b[i].row;
                a[i].col = b[i].col;
                a[i].burnningTime = b[i].burnningTime;
                a[i].burnningLimit = b[i].burnningLimit;
            }
        }

        //Fire spread from ignition point
        //Return the size of burnt area (number of sites burned)
        //assume the logFire is a data memember of CFIRE
        //Notice the value of row: 1 ...... rows
        //				   of col: 1 ...... cols
        public int fireSpread(int row, int col)
        {
            LDPOINT point = new LDPOINT();
            int numSites = 1; //Fire class (1-5).
            int totArea;
            int numCohorts = 0;
            int numFRU = m_pFireRegimeUnits.number();
            int fireClass = 0;
            int[] land = new int[DEFINE.MAX_LANDUNITS]; //fire on different landunits
            int[] FS = new int[DEFINE.MAX_LANDUNITS]; // predefined fire size for each landunits
            int[] landOnFire = new int[DEFINE.MAX_LANDUNITS]; // if fire on this landunits, it's 1, otherwise, 0
            int i;
            int j;
            int k;
            for (i = 0; i < DEFINE.MAX_LANDUNITS; i++)
            {
                land[i] = 0;
                FS[i] = 0;
                landOnFire[i] = 0;
            }

            int MemoryAllocNum = 500;
            FireFront[] front1 = new FireFront[MemoryAllocNum];
            FireFront[] front2 = new FireFront[MemoryAllocNum];
            FireFront[] frontTemp; //Add By Qia on Feb 10 2009
            int lLength1;
            int lLength2;
            int l;

            //</Add By Qia on Feb 10 2009>
            //front1 = new fireFront [m_fireParam.rows * m_fireParam.cols * 8];
            //front2 = new fireFront [m_fireParam.rows * m_fireParam.cols * 8];//Commented By Qia on Feb 10 2009
            //the initiation point

            point.x = col;
            point.y = row;
            int tempID = m_pFireSites[row, col].FRUIndex;
            j = tempID;
            land[j]++;
            landOnFire[j] = 1;
            FS[j] = fireSize(m_pFireRegimeUnits[j].m_fMFS, m_pFireRegimeUnits[j].m_fFireSTD);
            totArea = FS[j];
            DrawWindEvent();

           
            lLength1 = 1;
            front1[0].burnningTime = 1;
            front1[0].burnningLimit = 1;
            front1[0].row = row;
            front1[0].col = col;
            int tempRow;
            int tempCol;
            lLength2 = 0;
            while (lLength1 > 0)
            {
                for (l = 0; l < lLength1; l++)
                {
                    if (front1[l].burnningTime < front1[l].burnningLimit)
                    {
                        front2[lLength2].burnningLimit = front1[l].burnningLimit;
                        front2[lLength2].burnningTime = front1[l].burnningTime + 1;
                        front2[lLength2].row = front1[l].row;
                        front2[lLength2].col = front1[l].col;
                        lLength2++;
                        //<Add By Qia on Feb 10 2009>
                        if (lLength2 >= MemoryAllocNum)
                        {
                            Console.Write("Fire memory size reallocate {0:D}\n", MemoryAllocNum);
                            frontTemp = new FireFront[MemoryAllocNum];

                            DeepClone(ref frontTemp, front2);
                            front2 = null;
                            front2 = new FireFront[MemoryAllocNum * 2];
                            if (front2 == null)
                            {
                                throw new Exception("fire memory allocation fail\n");
                            }
                            DeepClone(ref front2, frontTemp);

                            DeepClone(ref frontTemp, front1);
                            front1 = null;
                            front1 = new FireFront[MemoryAllocNum * 2];
                            if (front1 == null)
                            {
                                throw new Exception("fire memory allocation fail\n");
                            }
                            DeepClone(ref front1, frontTemp);

                            MemoryAllocNum = MemoryAllocNum * 2;
                        }
                        //</Add By Qia on Feb 10 2009>
                    }
                    else
                    {
                        //burnt out
                        tempRow = front1[l].row;
                        tempCol = front1[l].col;
                        point.y = tempRow;
                        point.x = tempCol;
                        numCohorts += damage(point, ref fireClass);
                        m_Map[(uint)tempRow, (uint)tempCol] = (byte)(1 + fireClass);
                        //J.Yang A bug fixed here

                        for (i = 0; i < 8; i++)
                        {
                            switch (i)
                            {
                                case 0: //SW
                                    tempRow = front1[l].row - 1;
                                    tempCol = front1[l].col - 1;
                                    break;
                                case 1: //W
                                    tempRow = front1[l].row;
                                    tempCol = front1[l].col - 1;
                                    break;
                                case 2: //NW
                                    tempRow = front1[l].row + 1;
                                    tempCol = front1[l].col - 1;
                                    break;
                                case 3: //N
                                    tempRow = front1[l].row + 1;
                                    tempCol = front1[l].col;
                                    break;
                                case 4: //NE
                                    tempRow = front1[l].row + 1;
                                    tempCol = front1[l].col + 1;
                                    break;
                                case 5: //E
                                    tempRow = front1[l].row;
                                    tempCol = front1[l].col + 1;
                                    break;
                                case 6: //SE
                                    tempRow = front1[l].row - 1;
                                    tempCol = front1[l].col + 1;
                                    break;
                                case 7: //S
                                    tempRow = front1[l].row - 1;
                                    tempCol = front1[l].col;
                                    break;
                                default:
                                    break;
                            }
                            //check its neighbors
                            double fireSpreadProb;
                            //isValid means the site is in the bound
                            //and it is in an active site
                            //and it is unburnt site
                            //if the site is in a new fire regime unit even it is active
                            //check the limit of fire occurrences for this FRU
                            if (isValid(tempRow, tempCol, landOnFire))
                            {
                                j = m_pFireSites[tempRow, tempCol].FRUIndex;
                                CalculateWind(i);
                                CalculateSlope(front1[l].row, front1[l].col, tempRow, tempCol);
                                CalculateFSFactor(land[j], FS[j]);
                                fireSpreadProb = modifiedSpreadProb(tempRow, tempCol); //10*m_pLAND->TimeStep; Bu, Rencang, April 28, 2009;
                                if (fireSpreadProb > 1)
                                {
                                    Console.Write("fire spread probability (fireSpreadProb: {0:f}) is larger than 1", fireSpreadProb);
                                    Console.Write(" at site {0:D} {1:D} \n", tempRow, tempCol);
                                    fireSpreadProb = 1;
                                }
                                if (fireSpreadProb < 0)
                                {
                                    Console.Write("fire spread probability (fireSpreadProb: {0:f}) is less than 0", fireSpreadProb);
                                    Console.Write(" at site {0:D} {1:D} \n", tempRow, tempCol);
                                    fireSpreadProb = 0;
                                }
                                if (m_pStochastic.Bernoulli(fireSpreadProb))
                                {
                                    //burnning site
                                    front2[lLength2].row = tempRow;
                                    front2[lLength2].col = tempCol;
                                    front2[lLength2].burnningTime = 1;
                                    front2[lLength2].burnningLimit = 1;
                                    lLength2++;

                                    //<Add By Qia on Feb 10 2009>
                                    if (lLength2 >= MemoryAllocNum)
                                    {
                                        Console.Write("Fire memory size reallocate {0:D}\n", MemoryAllocNum);
                                        frontTemp = new FireFront[MemoryAllocNum];

                                        DeepClone(ref frontTemp, front2);
                                        front2 = null;
                                        front2 = new FireFront[MemoryAllocNum * 2];
                                        if (front2 == null)
                                        {
                                            throw new Exception("fire memory allocation fail\n");
                                        }
                                        DeepClone(ref front2, frontTemp);

                                        DeepClone(ref frontTemp, front1);
                                        front1 = null;
                                        front1 = new FireFront[MemoryAllocNum * 2];
                                        if (front1 == null)
                                        {
                                            throw new Exception("fire memory allocation fail\n");
                                        }
                                        DeepClone(ref front1, frontTemp);

                                        MemoryAllocNum = MemoryAllocNum * 2;
                                    }
                                    //</Add By Qia on Feb 10 2009>
                                    //change map value for this site
                                    m_Map[(uint)tempRow, (uint)tempCol] = 2; //0 not active; 1 no fire ; 2 fire class = 1 (lowest)
                                    //increase fire size (numSites) w.r.t. fire regime unit
                                    land[j]++;
                                    numSites++;
                                    if (landOnFire[j] == 0)
                                    { // an initiation
                                        landOnFire[j] = 1;
                                        FS[j] = fireSize(m_pFireRegimeUnits[j].m_fMFS, m_pFireRegimeUnits[j].m_fFireSTD);
                                        m_lptrIgnitionOccurance[j]--;
                                        if (m_lptrIgnitionOccurance[j] == 0)
                                        {
                                            for (k = 0; k < m_iNumLU; k++)
                                            {
                                                if (m_iptrValidLandUnits[k] == j)
                                                {
                                                    break;
                                                }
                                            }
                                            for (int ii = k; ii < m_iNumLU - 1; ii++)
                                            {
                                                m_iptrValidLandUnits[ii] = m_iptrValidLandUnits[ii + 1];
                                            }
                                            m_iNumLU--;
                                        }
                                    } //end of an initiation
                                }
                                else
                                {
                                    //dummy now
                                }
                            } // end one neighbor
                        } // end for looping in 8 neighbors
                    } //end else
                } //end for (looping in the front1)

                lLength1 = lLength2;
                if (lLength2 > 0)
                {
                    //copy elt from front2 to front 1;
                    for (l = 0; l < lLength2; l++)
                    {
                        front1[l].burnningLimit = front2[l].burnningLimit;
                        front1[l].burnningTime = front2[l].burnningTime;
                        front1[l].row = front2[l].row;
                        front1[l].col = front2[l].col;
                    }
                    lLength2 = 0;
                }
            } //end while

            front1 = null;
            front2 = null;
            //add to fire log file
            FileStream fs = new FileStream(m_fireParam.logFn, FileMode.Append, FileAccess.Write, FileShare.Write);
            m_LogFP = new StreamWriter(fs, System.Text.Encoding.Default);
            m_LogFP.WriteLine("{0}, ", m_itr * m_pLAND.TimeStepFire);
            m_LogFP.WriteLine("{0}, {1}, {2}, ", col, row, totArea);
            m_LogFP.WriteLine("{0}, {1}", numSites, numCohorts);
            for (i = 0; i < numFRU; i++)
            {
                m_LogFP.WriteLine(", {1}", land[i]);
            }
            m_LogFP.WriteLine();
            m_LogFP.Close();
            return numSites;
        }

        //Notice the value of row: 1 ...... rows
        //				   of col: 1 ...... cols
        public bool isValid(int row, int col, int[] landOnFire)
        {
            //check map boundary
            //check map value (0 not active, 1 no fires yet, 2 burnning or fire class is 1)
            //check the site is in an active FRU (Fire Regime Unit)
            //check the site is in a new FRU which still allows new initiations
            if (row < 1 || row > m_fireParam.rows || col < 1 || col > m_fireParam.cols)
            {
                return false;
            }
            if (m_Map[(uint)row, (uint)col] > 1)
            {
                return false;
            }
            //if (!m_pFireSites->operator ()(row,col)->pFireRegimeUnit->active())
            //	return 0;
            int j;
            j = m_pFireSites[row, col].FRUIndex;
            if (landOnFire[j] == 0) //there is no fire spreading at this FRU
            {
                if (m_lptrIgnitionOccurance[j] == 0)
                {
                    return false;
                }
            }
            return true;
        }

        public double modifiedSpreadProb(int row, int col)
        {
            double ret = 0.0;
            double k = 0.0;
            k = Math.Log(1 - m_fireParam.fSpreadProb) / (-3);
            if ((m_DLLMode & Succession.Landispro.defines.G_FUEL) != 0)
            {
                ret = 1 - Math.Exp(-1 * Math.Pow((1.0f + m_fireParam.fCoeff1), m_fWind) * Math.Pow((1.0f + m_fireParam.fCoeff2), m_fSlope) * Math.Pow((double)(1.0f + m_fireParam.fCoeff3), m_dFSFactor) * k * (int)m_pPDP.cFireIntensityClass[row,col]);
            }      
            else
            {
                ret = 1 - Math.Exp(-1 * Math.Pow((1 + m_fireParam.fCoeff1), m_fWind) * Math.Pow((1 + m_fireParam.fCoeff2), m_fSlope) * Math.Pow((double)(1 + m_fireParam.fCoeff3), m_dFSFactor) * k * 1);
            }
            return ret;
        }


        /************** The Coordination System in LANDIS ***************/
        /* (500,1) (500,2) (500,3) ..........(500,500)
        /* (499,1) (499,2) (499,3) ..........(499,500)
        /* ...........................................
        /* ...........................................
        /* (1,1)   (1,2)   (1,3) .............(1,500)
        For a 500 x 500 map
        */

        public void CalculateWind(int index)
        {
            //index is the direction along fire is spreading into
            //prevailing wind index and prevailing wind intensity is
            //	reserved in m_fireParam
            //simulated wind index and intensity will be done later
            m_fWind = 0.0;
            if (m_fireParam.iWindFlag == 0)
            {
                return;
            }
            /* draw a wind event
            */
            //comment it, move it to the begining of fire spread
            //otherwise, wind direction and speed is actually 
            //simulated at each cell burning time rather than 
            //at each fire ignition time.
            //DrawWindEvent();
            if (m_iWindIntensity == 0)
            {
                return;
            }
            int diff;
            diff = Math.Abs(m_iWindDirectionIndex - index);
            switch (diff)
            {
                case 0:
                    m_fWind = 0.2;
                    break;
                case 1:
                    m_fWind = 0.1;
                    break;
                case 7:
                    m_fWind = 0.1;
                    break;
                case 2:
                    m_fWind = 0.0;
                    break;
                case 6:
                    m_fWind = 0.0;
                    break;
                case 3:
                    m_fWind = -0.1;
                    break;
                case 5:
                    m_fWind = -0.1;
                    break;
                case 4:
                    m_fWind = -0.2;
                    break;
                default:
                    m_fWind = 0.0;
                    break;
            }
            m_fWind = m_fWind * m_iWindIntensity;
        }

        public void CalculateSlope(int row1, int col1, int row2, int col2)
        {
            m_fSlope = 0.0;
            double diff;
            if (m_fireParam.iDEMFlag == 1)
            {
                diff = m_pFireSites[row2, col2].DEM - m_pFireSites[row1, col1].DEM;
                m_fSlope = Math.Atan(diff / m_fireParam.cellSize);
            }
        }

        public void CalculateFSFactor(int lCurrentFS, int lFS)
        {
            if (lFS > 0)
            {
                m_dFSFactor = 1 - (double)2 * lCurrentFS / lFS;
            }
            else
            {
                m_dFSFactor = 0;
            }
        }

        //YYF
        //May have error: don't find update file
        public void updateFire_Regime_Map(int i)
        {
            string FireRegimeNametemp;
            string FireMapGIS;
            Dataset fpImg;
            double[] adfGeoTransform = new double[6];
            if (i / m_pLAND.TimeStepFire == 1)
            {
                StreamReader FpFireupdate;
                Fire_regime_files.Clear();
                Fire_regime_gisfiles.Clear();
                if ((FpFireupdate = new StreamReader(m_fireParam.File_regime_change)) == null)
                {
                    Console.Write("fire update file: {0}\n", m_fireParam.File_regime_change);
                    throw new Exception("Can not open fire update file");
                }
                String[] sarray;
                String inString = FpFireupdate.ReadLine();
                int num_of_files = int.Parse(inString);
                //while(!feof(FpFireupdate))
                for (int ii_count_num = 0; ii_count_num < num_of_files; ii_count_num++)
                {
                    if ((inString = FpFireupdate.ReadLine()) == null)
                    {
                        throw new Exception("Error reading in fire regime updating file\n");
                    }
                    sarray = inString.Trim().Split(' ');
                    FireMapGIS = sarray[0];
                    FireRegimeNametemp = sarray[1];
                    Fire_regime_files.Add(FireRegimeNametemp);
                    Fire_regime_gisfiles.Add(FireMapGIS);
                }
                FpFireupdate.Close();

                int index = i / m_pLAND.TimeStepFire - 1;
                if (index < Fire_regime_files.Count)
                {
                    FireRegimeNametemp = string.Format("{0}", Fire_regime_files[index]);
                    FireMapGIS = string.Format("{0}", Fire_regime_gisfiles[index]);
                    StreamReader luFile;
                    m_fireParam.fireRegimeAttrFn = FireRegimeNametemp;
                    if ((luFile = new StreamReader(m_fireParam.fireRegimeAttrFn)) == null)
                    {
                        Console.Write("Fire regime file {0} not found.\n", m_fireParam.fireRegimeAttrFn);
                        throw new Exception(m_fireParam.fireRegimeAttrFn);
                    }
                    else
                    {
                        //update landtype attribute
                        m_pFireRegimeUnits.read(luFile);
                        luFile.Close();
                        m_pFireRegimeUnits.updateIGDensity(m_fireParam.cellSize);
                    }

                    Console.Write("\nFire regime parameter updated.\n");
                    m_fireParam.fireRegimeDataFn = FireMapGIS;
                    if ((fpImg = Gdal.Open(m_fireParam.fireRegimeDataFn, Access.GA_ReadOnly)) == null)
                    { //* landtype.img
                        Console.Write("File regime map file {0} not found.\n", m_fireParam.fireRegimeDataFn);
                        throw new Exception(m_fireParam.fireRegimeDataFn); //*
                    }
                    else
                    {
                        //update landtype attribute
                        m_pFireRegimeUnits.readFireRegimeIMG(fpImg);
                        fpImg.Dispose();
                    }
                    Console.Write("\nFire Regime map Updated.\n");
                }
            }
            if (i / m_pLAND.TimeStepFire > 1)
            {
                int index = i / m_pLAND.TimeStepFire - 1;
                if (index < Fire_regime_files.Count)
                {
                    FireRegimeNametemp = string.Format("{0}", Fire_regime_files[index]);
                    FireMapGIS = string.Format("{0}", Fire_regime_gisfiles[index]);
                    StreamReader luFile;
                    m_fireParam.fireRegimeAttrFn = FireRegimeNametemp;
                    if ((luFile = new StreamReader(m_fireParam.fireRegimeAttrFn)) == null)
                    {
                        Console.Write("File regime map file {0} not found.\n", m_fireParam.fireRegimeAttrFn);
                        throw new Exception(m_fireParam.fireRegimeAttrFn);
                    }
                    else
                    {
                        //update landtype attribute
                        m_pFireRegimeUnits.read(luFile);
                        luFile.Dispose();
                        m_pFireRegimeUnits.updateIGDensity(m_fireParam.cellSize);
                    }
                    Console.Write("\nFire regime parameter updated.\n");

                    m_fireParam.fireRegimeDataFn = FireMapGIS;
                    if ((fpImg = (Dataset)Gdal.Open(m_fireParam.fireRegimeDataFn, Access.GA_ReadOnly)) == null)
                    { //* landtype.img
                        Console.Write("File regime map file {0} not found.\n", m_fireParam.fireRegimeDataFn);
                        throw new Exception(m_fireParam.fireRegimeDataFn); //*
                    }
                    else
                    {
                        //update landtype attribute
                        m_pFireRegimeUnits.readFireRegimeIMG(fpImg);
                        fpImg.Dispose();
                    }
                    Console.Write("\nFire Regime map Updated.\n");
                }
            }
        }

        public void updateFRU(int itr)
        {
            StreamReader fp;
            string str;
            string iterString;
            iterString = System.Convert.ToString(itr, m_pLAND.TimeStepFire);
            str = m_fireParam.fireRegimeAttrFn;
            str += iterString;
            

            if ((fp = new StreamReader(str)) == null)
            {
                Console.Write("Fire regime attribute file {0} not found.\n", str);
                throw new Exception("FIRE: fire regime attribute can not be updated.");
            }
            m_pFireRegimeUnits.read(fp);
            fp.Close();

            if (m_fireParam.iFireRegimeFlag != 0)
            {
                str = m_fireParam.fireRegimeDataFn;
                str += iterString;
                FileStream fs = new FileStream(str, FileMode.Open, FileAccess.Read);                ;
                BinaryReader fp2;
                if ((fp2 = new BinaryReader(fs)) == null)
                {
                    Console.Write("Fire regime GIS file {0} not found.\n", str);
                    throw new Exception("FIRE: fire regime GIS can not be updated.");
                }
                m_pFireRegimeUnits.readFireRegimeGIS(fp2);
                fp2.Close();
            }
            else
            {
                AttachLandUnitGIS(m_pLAND);
            }
            //j.Yang check this
            m_pFireRegimeUnits.dispatch();
            m_pFireRegimeUnits.updateIGDensity(m_fireParam.cellSize);
        }

        public void DrawWindEvent()
        {
            /* 
            generate a number that follows uniform distribution 
            bw. 0 -- 100, then compare it with the frqeuncy distributions
            if it is less than Percentage of NonWindEvents, then we write no wind
            otherwise, it is a wind event
            Do similar thing to decide wind class
            */
            m_iWindEventsLog[41]++;
            double z;
            z = m_pStochastic.Random() * 100;
            if (z <= m_fireParam.iNonWindPercent)
            {
                m_iWindIntensity = 0;
                m_iWindDirectionIndex = 0; //any thing, doesn't matter
                m_iWindEventsLog[0]++;
                return;
            }
            z = m_pStochastic.Random() * 100;
            for (int i = 0; i < 40; i++)
            {
                if (z <= m_fireParam.iCummWindClass[i])
                {
                    //it is i'th class 
                    m_iWindDirectionIndex = (int)(Math.Floor((double)i / 5));
                    m_iWindIntensity = (i % 5) + 1;
                    m_iWindEventsLog[i + 1]++;
                    return;
                }
            }
        }

        public void PrintWindLog()
        {
            if (m_fireParam.iWindFlag == 0)
            {
                return;
            }
            int[] percentage = new int[41];
            int i;
            if (m_iWindEventsLog[41] == 0)
            {
                percentage[0] = 0;
            }
            else
            {
                percentage[0] = 100 * m_iWindEventsLog[0] / m_iWindEventsLog[41];
            }
            int temp;
            temp = m_iWindEventsLog[41] - m_iWindEventsLog[0];
            for (i = 1; i < 41; i++)
            {
                if (temp <= 0)
                {
                    percentage[i] = 0;
                }
                else
                {
                    percentage[i] = 100 * m_iWindEventsLog[i] / temp;
                }
            }
            Console.Write("There are {0:D} fire events simulated in this iteraion\t", m_iWindEventsLog[41]);
            Console.Write("Among them, there are {0:D} ({1:D} percent) non-wind events\n", m_iWindEventsLog[0], percentage[0]);
            Console.Write("Number of wind class simulated and its respective percentage of wind events are:\n");
            for (i = 1; i < 41; i++)
            {
                if ((i % 5) == 0)
                {
                    Console.Write("{0,5:D} ({1,2:D})\n", m_iWindEventsLog[i], percentage[i]);
                }
                else
                {
                    Console.Write("{0,5:D} ({1,2:D})\t", m_iWindEventsLog[i], percentage[i]);
                }
            }      
        }

        public void WriteTSLF(int snr, int snc, int itr, double[] wAdfGeoTransform)
        {
            int i;
            int j;
            string str;
            if (m_fireParam.iTSLFFlag == 1)
            {
                /*
				FILE * tslfFile;
				sprintf(str,"%s/tslf%d.txt",parameters.outputDir,itr*10);
				if ((tslfFile=fopen(str,"a"))==NULL)

					errorSys("Error opening time-since-last-fire file",STOP);
				*/
                Succession.Landispro.map8 m = new Succession.Landispro.map8();
                m.dim((uint)snr, (uint)snc);
                for (i = 1; i <= snr; i++)
                {
                    for (j = 1; j <= snc; j++)
                    {
                        int tempID = m_pFireSites[i, j].FRUIndex;
                        if (m_pFireRegimeUnits[tempID].Active())
						{
                            m[(uint)i, (uint)j] = (ushort)(m_pPDP.sTSLFire[i - 1,j - 1] / 10);
                        }    
						else
						{
                            m[(uint)i , (uint)j ] = 255;
                        }
                    }
                }
                str = string.Format("Time-since-last-fire for year {0:D}.", itr* m_pLAND.TimeStepFire);
                m.setHeader(m_pLAND.Header);
			    m.rename(str);
                for (i = 0;i< Succession.Landispro.map8.maxLeg;i++)  
				{
				    str = string.Format("{0:D} year", i* m_pLAND.TimeStepFire);
                    m.assignLeg((uint)i, str);   
				}
                m.assignLeg(255,"Not Active");    
				str = string.Format("{0}{1:D}", m_strTSLF, itr* m_pLAND.TimeStepFire);
                m.CellSize = m_fireParam.cellSize;
                m.write(str, red, green, blue);
		    }   
	    }

        public void FinneyInitilization()
        {
            m_fuelCostMap = new double[m_iMapRow + 1][];
            m_windCostMap = new double[m_iMapRow + 1][];
            m_minTimeMap = new double[m_iMapRow + 1][];
            m_checkMap = new int[m_iMapRow + 1][];
            for (int i = 0; i <= m_iMapRow; i++)
            {
                m_fuelCostMap[i] = new double[m_iMapColumn + 1];
                m_windCostMap[i] = new double[m_iMapColumn + 1];
                m_minTimeMap[i] = new double[m_iMapColumn + 1];
                m_checkMap[i] = new int[m_iMapColumn + 1];
            }
            m_actualROSMap = m_windCostMap; //m_actualROSMap is identical to m_windCostMap. Just different name
        }

        public void FinneyFreeMemory()
        {
            for (int i = 0; i <= m_iMapRow; i++)
            {
                m_fuelCostMap[i] = null;
                m_windCostMap[i] = null;
                m_minTimeMap[i] = null;
                m_checkMap[i] = null;
            }
            m_fuelCostMap = null;
            m_windCostMap = null;
            m_minTimeMap = null;
            m_checkMap = null;
        }

        public void FinneyCalculateDimensions()
        {
            //m_fRatio = m_iWindIntensity * 0.195 + 0.885;
            m_fRatio = m_finneyParam.ellipseAxisRatio[m_iWindIntensity];
            //major axis divided by minor axis
            //Priliminary equation relating wind serverity to ellipse shape
            /*
            Ratio = Scalar(WindStr * 0.2 + 1)  	
            A = Scalar(SQRT((Area div PI) div Ratio))  // A = minor axis length
            B = Scalar (A * Ratio) // B = major axis length
            C = Scalar (SQRT(B * B - A * A))
            */
            //fire size unit is hectare, change it to meter square. 1 hectare = 10000 meter squar
            //A is flanking spread rate meter/day assuming the fire size is reached within one day
            //B + C is forward maximum spread rate (when Theta = 0)
            //C is the offset from the center of the ellipse to the ignition point
            m_fA = m_fRatio - Math.Sqrt(m_fRatio * m_fRatio - 1);
            m_fB = m_fA * m_fRatio;
            m_fC = 1 - m_fB;
        }

        public double fireSizeHa(double MFS, double STD)
        {
            //generate random fire size based on lognormal distribution
            double size;
            double VAR;
            //if x is fire size following lognormal distribution with mean MFS and variance as VAR
            //then log(x) follows normal distribution with
            //mean = 2logMFS - 1/2log(VAR+MFS square)
            //variance = log(VAR+MFS square) - 2logMFS
            double mean;
            double @var;
            double std;
            VAR = STD * STD;
            mean = 2.0 * Math.Log(MFS) - 0.5 * Math.Log(VAR + MFS * MFS);
            @var = Math.Log(1.0 * (VAR + MFS * MFS)) - 2.0 * Math.Log(MFS);
            std = Math.Sqrt(@var);
            size = m_pStochastic.Normal(mean, std);
            size = Math.Exp(size); //unit: hectare. 1 ha = 10,000 squre meters
            return size;
        }

        public int FinneySpread()
        {
            DrawWindEvent();
            int numFRU = m_pFireRegimeUnits.number();
            int tempID;
            InitilizeBurnedCells();
            tempID = m_pFireSites[m_iOriginRow, m_iOriginColumn].FRUIndex;
            if (m_fireParam.iFuelFlag == 2)
            {
                m_fPredefinedDuration = fireDuration(m_pFireRegimeUnits[tempID].m_fMFS, m_pFireRegimeUnits[tempID].m_fFireSTD);
            }
            else if (m_fireParam.iFuelFlag == 3)
            {
                m_lPredefinedFireSize = fireSize(m_pFireRegimeUnits[tempID].m_fMFS, m_pFireRegimeUnits[tempID].m_fFireSTD);
            }
            FinneyCalculateDimensions();
            FinneyCalculateFuelCost();
            FinneyEucDirection();
            FinneyCalculateWindCost();
            FinneyCalculateFinalCost();
            FinneyCalculateMinTime();

            //add to fire log file
            FileStream fs = new FileStream(m_fireParam.logFn, FileMode.Append, FileAccess.Write, FileShare.Write);
            m_LogFP = new StreamWriter(fs, System.Text.Encoding.Default);
            m_LogFP.Write("{0}, ", m_itr * m_pLAND.TimeStepFire);
            string str="";
            if (m_fireParam.iFuelFlag == 2)
            {
                str = string.Format("{0:f}", m_fPredefinedDuration);
            }
            else if (m_fireParam.iFuelFlag == 3)
            {
                str = string.Format("{0:D}", m_lPredefinedFireSize);
            }
            m_LogFP.Write("{0}, {1}, {2}, ", m_iOriginColumn, m_iOriginRow, str);
            m_LogFP.Write("{0}, {1}", length_BurnedCells, FinneynumCohorts);
            for (int i = 0; i < numFRU; i++)
            {
                m_LogFP.Write(", {0}", burnedCells[i]);
            }
            m_LogFP.WriteLine();
            m_LogFP.Close();
            return length_BurnedCells;
        }

        public void FinneyCalculateFuelCost()
        //function name should be calculateSpreadRate() instead in this version
        {
            int iRow = m_iMapRow;
            int iCol = m_iMapColumn;
            int i;
            int j;
            if (0 == m_fireParam.iDEMFlag)
            {
                for (i = 1; i <= iRow; i++)
                {
                    for (j = 1; j <= iCol; j++)
                    {
                        int tempFuelClass;
                        tempFuelClass = m_pPDP.cFireIntensityClass[i,j];
                        m_fuelCostMap[i][j] = m_finneyParam.spreadRate[tempFuelClass,m_iWindIntensity];
                    }
                }
            }
            else
            {
                int tempSlope;
                for (i = 1; i <= iRow; i++)
                {
                    for (j = 1; j <= iCol; j++)
                    {
                        tempSlope = m_pFireSites[i, j].DEM;
                        int tempFuelClass;
                        tempFuelClass = m_pPDP.cFireIntensityClass[i,j];
                        if (0 == tempSlope)
                        {
                            m_fuelCostMap[i][j] = m_finneyParam.spreadRate[tempFuelClass,m_iWindIntensity];
                        }
                        if (1 == tempSlope)
                        {
                            m_fuelCostMap[i][j] = m_finneyParam.ROS_low[tempFuelClass,m_iWindIntensity];
                        }

                        if (2 == tempSlope)
                        {
                            m_fuelCostMap[i][j] = m_finneyParam.ROS_moderate[tempFuelClass,m_iWindIntensity];
                        }
                        if (3 == tempSlope)
                        {
                            m_fuelCostMap[i][j] = m_finneyParam.ROS_high[tempFuelClass,m_iWindIntensity];
                        }
                        if (4 == tempSlope)
                        {
                            m_fuelCostMap[i][j] = m_finneyParam.ROS_extreme[tempFuelClass,m_iWindIntensity];
                        }
                    }
                }
            }
            if ( __DEBUG == 1 && m_FinneyDebugOutput <= 3)
            {
                //output the wind cost map to c:\temp\fuelcostmap.txt
                StreamWriter fp = new StreamWriter("c:\\temp\\fuelRateMap.txt");
                for (i = iRow; i >= 1; i--)
                {
                    for (j = 1; j <= iCol; j++)
                    {
                        fp.Write("{0}, ", m_fuelCostMap[i][j]);
                    }
                    fp.WriteLine();
                }
                fp.Close();
                m_FinneyDebugOutput++;
            }
        }

        /* this function is different from EucDirection in my fireTravel program
        because they are using different coordination system
        */
        public void FinneyEucDirection()
        {
            int iRow = m_iMapRow;
            int iCol = m_iMapColumn;
            int i;
            int j;
            /* transform
            (x,y) -->(x',y')
            where (x,y) is from the coordination system
            (row,1).......(row,col)
            :
            (1,1).........(1,col)
            and (x',y') is from 
            (0,0)........(0,col-1)
            :
            (row-1,0).....(row-1,col-1)
            then 
            x' = row - x; y' = y -1;
            */
            int originRowTrans = iRow - m_iOriginRow;
            int originColTrans = m_iOriginColumn - 1;
            for (i = 0; i < iRow; i++)
            {
                for (j = 0; j < iCol; j++)
                {
                    int distRow;
                    int distColumn;
                    int iTrans = iRow - i;
                    int jTrans = j + 1;
                    distRow = i - originRowTrans; //difference in row //note m_iOriginRow starts from 1 rather than 0
                    distColumn = j - originColTrans; //difference in column
                    if (distRow != 0)
                    {
                        if (distRow > 0) //South
                        {
                            if (distColumn <= 0) //SW 180-270
                            {
                                m_windCostMap[iTrans][jTrans] = (-1 * Math.Atan((double)distColumn / distRow) * 180 / PI) + 180;
                            }
                            else //SE 90-180
                            {
                                m_windCostMap[iTrans][jTrans] = 180 - (Math.Atan((double)distColumn / distRow) * 180 / PI);
                            }
                        }
                        else //North
                        {
                            if (distColumn <= 0) //NW 270 -360
                            {
                                m_windCostMap[iTrans][jTrans] = 360 - (Math.Atan((double)distColumn / distRow) * 180 / PI);
                            }
                            else //NE 0 - 90
                            {
                                m_windCostMap[iTrans][jTrans] = (-1 * Math.Atan((double)distColumn / distRow) * 180 / PI);
                            }
                        }
                    }
                    else //distRow == 0, either E(90) or W(270)
                    {
                        if (distColumn < 0)
                        {
                            m_windCostMap[iTrans][jTrans] = 270; //W
                        }
                        if (distColumn > 0)
                        {
                            m_windCostMap[iTrans][jTrans] = 90; //E
                        }
                        if (distColumn == 0)
                        {
                            m_windCostMap[iTrans][jTrans] = 0; //reserved for origin point
                        }
                    }
                }
            }
            /*
            for (i = iRow; i >=1 ; i--)
            {
                for (j = 1; j<= iCol; j++)
                {
                    int distRow, distColumn;
                    distRow = i - m_iOriginRow; //difference in row //note m_iOriginRow starts from 1 rather than 0
                    distColumn = j - m_iOriginColumn; //difference in column                 
                    if (distRow != 0)
                    { 
                        if (distRow > 0) //South
                        {
                            if (distColumn <= 0) //SW 180-270
                                m_windCostMap[iTrans][jTrans] = (-1* atan( (double) distColumn/distRow)* 180/PI) + 180;
                            else //SE 90-180
                                m_windCostMap[iTrans][jTrans] = 180 - (atan( (double) distColumn/distRow)* 180/PI);
                        }
                        else //North
                        {
                            if (distColumn <= 0) //NW 270 -360
                                m_windCostMap[iTrans][jTrans] = 360 - (atan( (double) distColumn/distRow)* 180/PI);
                            else //NE 0 - 90
                                m_windCostMap[iTrans][jTrans] = (-1 * atan( (double) distColumn/distRow)* 180/PI);
                        }
                    }
                    else //distRow == 0, either E(90) or W(270)
                    {
                        if (distColumn < 0)
                            m_windCostMap[iTrans][jTrans] = 270; //W
                        if (distColumn > 0)
                            m_windCostMap[iTrans][jTrans] = 90; //E
                        if (distColumn == 0)
                            m_windCostMap[iTrans][jTrans] = 0; //reserved for origin point
                    }
                }	
            }
            */
            if (__DEBUG==1 && m_FinneyDebugOutput <= 3)
            {
                //output the wind cost map to c:\temp\EucDirection.txt
                StreamWriter fp;
                fp = new StreamWriter("c:\\temp\\EucDirection.txt");
                for (i = iRow; i >= 1; i--)
                {
                    for (j = 1; j <= iCol; j++)
                    {
                        fp.Write("{0:F0} ", m_windCostMap[i][j]);
                    }
                    fp.WriteLine();
                }
                fp.Close();
                m_FinneyDebugOutput++;
            }
        }

        public void FinneyCalculateWindCost()
        {
            // if iWindServerity == 0 then all wind cost is 1
            // else update windCost for each cell
            // what about the origin cell?
            // how to incorporate wind serverity in this wind cost calculation? do we need it?
            int iRow = m_iMapRow;
            int iCol = m_iMapColumn;
            int i;
            int j;
            int degree;

            m_maxWindRate = 0;
            if (m_iWindIntensity != 0)
            {
                switch (m_iWindDirectionIndex)
                {
                    case 0:
                        //SW
                        degree = 225;
                        break;
                    case 1:
                        //W
                        degree = 270;
                        break;
                    case 2:
                        //NW
                        degree = 315;
                        break;
                    case 3:
                        //N
                        degree = 360;
                        break;
                    case 4: //NE
                        degree = 45;
                        break;
                    case 5:
                        //E
                        degree = 90;
                        break;
                    case 6:
                        //SE
                        degree = 135;
                        break;
                    case 7:
                        degree = 180;
                        break;
                    default:
                        degree = 0;
                        break;
                }
                for (i = iRow; i >= 1; i--)
                {
                    for (j = 1; j <= iCol; j++)
                    {
                        //int iTrans = iRow -i;
                        //int jTrans = j 1 1;
                        double Beta;
                        //Beta = (m_windCostMap[i][j] - 180 - m_parameterSet.iWindDirection) /360;
                        /*Brian's version counts for the fact that ArcGIS EucDirection is used to 
                        calculate the direction to the ignition rather than from igniton
                        */
                        Beta = (m_windCostMap[i][j] - degree) * PI / 180;
                        double sqA;
                        double sqB;
                        double sqC;
                        double CosBeta;
                        double SinBeta;
                        double sqCosBeta;
                        double sqSinBeta;
                        sqA = m_fA * m_fA;
                        sqB = m_fB * m_fB;
                        sqC = m_fC * m_fC;
                        CosBeta = Math.Cos(Beta);
                        SinBeta = Math.Sin(Beta);
                        sqCosBeta = CosBeta * CosBeta;
                        sqSinBeta = SinBeta * SinBeta;
                        double Theta = 0.0;
                        Theta = Math.Acos((m_fA * CosBeta * Math.Sqrt(sqA * sqCosBeta + (sqB - sqC) * sqSinBeta) - (m_fB * m_fC * sqSinBeta)) / (sqA * sqCosBeta + sqB * sqSinBeta));
                        double Rate = 1;
                        Rate = (m_fA * (m_fC * Math.Cos(Theta) + m_fB)) / (Math.Sqrt((sqA * Math.Cos(Theta) * Math.Cos(Theta)) + (sqB * Math.Sin(Theta) * Math.Sin(Theta))));
                        //  Rate is equal to distance covered in 1 burning day
                        if (m_maxWindRate < Rate)
                        {
                            m_maxWindRate = Rate;
                        }
                        m_windCostMap[i][j] = Rate; //now it is rate, rather than cost
                    }
                }
            }
            else
            {
                m_maxWindRate = 1;
                for (i = iRow; i >= 1; i--)
                {
                    for (j = 1; j <= iCol; j++)
                    {
                        m_windCostMap[i][j] = 1;
                    }
                }
            }
            if (__DEBUG == 1 && m_FinneyDebugOutput <= 3)
            {
                //output the wind cost map to c:\temp\windcostmap.txt
                StreamWriter fp;
                fp = new StreamWriter("c:\\temp\\WindIndexMap.txt");
                for (i = iRow; i > 0; i--)
                {
                    for (j = 1; j <= iCol; j++)
                    {
                        fp.Write("{0} ", m_windCostMap[i][j]);
                    }
                    fp.WriteLine();
                }
                fp.Close();
                m_FinneyDebugOutput++;
            }
        }

        //The funcion below outputs rate instead of cost now. J. Yang, 09/04/2006
        public void FinneyCalculateFinalCost()
        {
            int iRow = m_iMapRow;
            int iCol = m_iMapColumn;
            int i;
            int j;
            double WindIndex;
            double FinalFuelRate;
            double FinalWindIndex;
            for (i = 1; i <= iRow; i++)
            {
                for (j = 1; j <= iCol; j++)
                {
                    if (m_fuelCostMap[i][j] > 0)
                    {
                        WindIndex = m_windCostMap[i][j] / m_maxWindRate;
                        FinalFuelRate = Math.Pow(m_fuelCostMap[i][j], m_finneyParam.fuelWeight);
                        FinalWindIndex = Math.Pow(WindIndex, m_finneyParam.windWeight);
                        FinalFuelRate = FinalFuelRate * FinalWindIndex;
                        m_fuelCostMap[i][j] = FinalFuelRate;
                        m_windCostMap[i][j] = 0.0; //it will be used as the actual ROS in the CalculateMinTime() fn.
                        // note that m_windCostMap and m_actualROSMap are the same
                    }
                    else
                    {
                        m_windCostMap[i][j] = 0.0;
                    }
                }
            }
            if (__DEBUG == 1 && m_FinneyDebugOutput <= 4)
            {
                //output final cost
                StreamWriter fp;
                fp = new StreamWriter("c:\\temp\\finalcost.txt");
                for (i = iRow; i > 0; i--)
                {
                    for (j = 1; j <= iCol; j++)
                    {
                        if (m_fuelCostMap[i][j] != double.MaxValue)
                        {
                            fp.Write("{0} ", m_fuelCostMap[i][j]);
                        }
                        else
                        {
                            fp.Write("{0} ", 0);
                        }
                    }
                    fp.WriteLine();
                }
                fp.Close();
                m_FinneyDebugOutput++;
            }
        }

        public void FinneyCalculateMinTime()
        {
            //initilization to the + infinite first
            int iRow;
            int iCol;
            iRow = m_iMapRow;
            iCol = m_iMapColumn;
            int i;
            int j;
            for (i = 1; i <= iRow; i++)
            {
                for (j = 1; j <= iCol; j++)
                {
                    m_minTimeMap[i][j] = double.MaxValue;
                }
            }
            FinneynumCohorts = 0;
            int fireClass = 0;
            LDPOINT point = new LDPOINT();
            m_minTimeMap[m_iOriginRow][m_iOriginColumn] = 0; //m_iOriginRow starts from 1, not zero
            //damage, amtdamaged ++
            point.y = m_iOriginRow;
            point.x = m_iOriginColumn;
            FinneynumCohorts += damage(point, ref fireClass);
            m_Map[(uint)m_iOriginRow, (uint)m_iOriginColumn] = (byte)(1 + fireClass);

            int tempID;
            tempID = m_pFireSites[m_iOriginRow, m_iOriginColumn].FRUIndex;
            burnedCells[tempID] = 1;
            length_BurnedCells = 1;
            m_FinneyCutoff = false;
            FinneyList.Clear();
            FinneyInitilizeActiveCostList();
            while (FinneyList!=null && FinneyList.Count>0)
            {
                //expand activePoints by one more	
                CFinneyCell tempCell = new CFinneyCell();
                tempCell = FinneyList.First();
                FinneyList.RemoveAt(0);
                m_checkMap[tempCell.row][tempCell.col] = 1;
                //break the loop if > the cut-off

                if (m_fireParam.iFuelFlag == 2) //duration approach
                {
                    if (tempCell.minTime > m_fPredefinedDuration)
                    {
                        break;
                    }
                }
                else if (m_fireParam.iFuelFlag == 3) //fire size approach
                {
                    if ((length_BurnedCells + 1) > m_lPredefinedFireSize)
                    {
                        break;
                    }
                }

                //simulate the damage
                point.y = tempCell.row;
                point.x = tempCell.col;
                FinneynumCohorts += damage(point, ref fireClass);
                m_Map[(uint)tempCell.row, (uint)tempCell.col] = (byte)(1 + fireClass);
                //bookkeeping		
                int tempID2;
                tempID2 = m_pFireSites[tempCell.row, tempCell.col].FRUIndex;
                burnedCells[tempID2]++;
                length_BurnedCells++;
                FinneyExpandCostList(tempCell.row, tempCell.col);
            }

            if (__DEBUG == 1 && m_FinneyDebugOutput <= 5)
            {
                //output travel time
                StreamWriter fp;
                fp = new StreamWriter("c:\\temp\\traveltime.txt");

                for (i = iRow; i > 0; i--)
                {
                    for (j = 1; j <= iCol; j++)
                    {
                        if (m_minTimeMap[i][j] != double.MaxValue)
                        {
                            fp.Write("{0} ", m_minTimeMap[i][j]);
                        }
                        else
                        {
                            fp.Write("{0}, ", 0);
                        }
                    }
                    fp.WriteLine();
                }
                fp.Close();
                m_FinneyDebugOutput++;
            }
        }

        public void FinneyInitilizeActiveCostList()
        {
            int i;
            int j;
            int k;
            int tempRow = 0;
            int tempCol = 0;
            double weight = 0;
            int iRow = m_iMapRow;
            int iCol = m_iMapColumn;

            for (i = 1; i <= iRow; i++)
            {
                for (j = 1; j <= iCol; j++)
                {
                    m_checkMap[i][j] = 0;
                }
            }
            m_checkMap[m_iOriginRow][m_iOriginColumn] = 1;
            i = m_iOriginRow; //iOriginRow starts from 1
            j = m_iOriginColumn;
            //LDPOINT point;
            /*
            int neighborPointID[8];//sorted w.r.t. fuelcost from low to high
            //i.e., neighborPointID[0] = 1 means ID 1 neighbor point has least fuel cost	
            */
            for (k = 0; k < 8; k++)
            {
                switch (k)
                {
                    case 0:
                        tempRow = i - 1;
                        tempCol = j - 1;
                        weight = Math.Sqrt(2.0);
                        break;
                    case 1:
                        tempRow = i;
                        tempCol = j - 1;
                        weight = 1.0;
                        break;
                    case 2:
                        tempRow = i + 1;
                        tempCol = j - 1;
                        weight = Math.Sqrt(2.0);
                        break;
                    case 3:
                        tempRow = i + 1;
                        tempCol = j;
                        weight = 1.0;
                        break;
                    case 4:
                        tempRow = i + 1;
                        tempCol = j + 1;
                        weight = Math.Sqrt(2.0);
                        break;
                    case 5:
                        tempRow = i;
                        tempCol = j + 1;
                        weight = 1.0;
                        break;
                    case 6:
                        tempRow = i - 1;
                        tempCol = j + 1;
                        weight = Math.Sqrt(2.0);
                        break;
                    case 7:
                        tempRow = i - 1;
                        tempCol = j;
                        weight = 1.0;
                        break;
                }
                //calculate minTimeCost from the temp point to (i,j)
                //provided it is a valid point (within boundary, not an active point)
                double tempCost = 0F;
                if (FinneyIsValid(tempRow, tempCol))
                {
                    //tempCost = m_fireParam.cellSize * (m_fuelCostMap[tempRow][tempCol] + m_fuelCostMap[i][j]) * 0.5 * weight;
                    tempCost = FinneyCalculateAcceleration(i, j, tempRow, tempCol, weight);
                    m_minTimeMap[tempRow][tempCol] = tempCost;
                    // add the stuct fireCell to the listTime	
                    CFinneyCell tempCell = new CFinneyCell();
                    tempCell.SetValue(tempRow, tempCol, tempCost);
                    FinneyList.Add(tempCell);
                }
            }          
            FinneyList.Sort(); //sort in ascending order
        }

        public bool FinneyIsValid(int row, int column)
        {
            if (row < 1 || row > m_iMapRow || column < 1 || column > m_iMapColumn)
            {
                return false;
            }
            if (m_minTimeMap[row][column] < double.MaxValue) //has been updated during spread simulation
            {
                return false;
            }
            if (m_fuelCostMap[row][column] == double.MaxValue) //invalid landtype
            {
                return false;
            }
            if (m_Map[(uint)row, (uint)column] > 1) //has been burned
            {
                return false;
            }
            return true;
        }

        public void FinneyExpandCostList(int row, int col)
        {
            //put the eight neighbor points of ignition point into the costList
            int i;
            int j;
            i = row;
            j = col;
            int tempRow = 0;
            int tempCol = 0;
            double weight = 0;
            LDPOINT point = new LDPOINT();
            for (int k = 0; k < 8; k++)
            {
                switch (k)
                {
                    case 0:
                        tempRow = i - 1;
                        tempCol = j - 1;
                        weight = Math.Sqrt(2.0);
                        break;
                    case 1:
                        tempRow = i;
                        tempCol = j - 1;
                        weight = 1.0;
                        break;
                    case 2:
                        tempRow = i + 1;
                        tempCol = j - 1;
                        weight = Math.Sqrt(2.0);
                        break;
                    case 3:
                        tempRow = i + 1;
                        tempCol = j;
                        weight = 1.0F;
                        break;
                    case 4:
                        tempRow = i + 1;
                        tempCol = j + 1;
                        weight = Math.Sqrt(2.0);
                        break;
                    case 5:                    
                        tempRow = i;
                        tempCol = j + 1;
                        weight = 1.0;
                        break;
                    case 6:
                        tempRow = i - 1;
                        tempCol = j + 1;
                        weight = Math.Sqrt(2.0f);
                        break;
                    case 7:
                        tempRow = i - 1;
                        tempCol = j;
                        weight = 1.0F;
                        break;
                }
                //calculate minTimeCost from the temp point to (i,j)
                //provided it is a valid point (within boundary, not an active point)
                double tempCost = 0F;

                if (FinneyIsValid(tempRow, tempCol))
                {
                    /*
                    tempCost = m_fireParam.cellSize * (m_fuelCostMap[tempRow][tempCol] + m_fuelCostMap[i][j]) 
                        * 0.5 * weight 
                        + m_minTimeMap[i][j];
                    */
                    tempCost = FinneyCalculateAcceleration(i, j, tempRow, tempCol, weight) + m_minTimeMap[i][j];
                    m_minTimeMap[tempRow][tempCol] = tempCost;
                    CFinneyCell tempCell = new CFinneyCell();
                    tempCell.SetValue(tempRow, tempCol, tempCost);
                    //insert the cell into the list
                    FinneyInsertList(tempCell);
                }
            }
        }

        public void InitilizeBurnedCells()
        {
            for (int i = 0; i < DEFINE.MAX_LANDUNITS; i++)
            {
                burnedCells[i] = 0;
            }
        }

        public LDPOINT Retrieve(int index)
        {
            //index is the ID of FRU
            int cellNo;
            int tempNo;
            cellNo = (int)m_pStochastic.Uniform(1, m_FRUAvailableCells[index]);
            m_FRUAvailableCells[index]--;
            int i;
            int j;
            int count = 0;
            int snr = m_pFireSites.numRows();
            int snc = m_pFireSites.numColumns();
            bool found = false;

            /*for (i=1;i<=snr;i++)
            {
                for (j=1;j<=snc;j++)
                {
                    int tempID;
                    tempID = m_pFireSites->operator ()(i,j)->FRUIndex;	
                    tempNo = (i-1) * snc + j;
                    if (tempID == index && m_pIgnitionStatusArray[tempNo] == 0)
                        count ++;
                    if (count == cellNo)
                    {
                        m_pIgnitionStatusArray[tempNo] = 1;
                        found = true;
                        break;
                    }
                }
                if (found)
                    break;
            }
            if (j > snc || i > snr)
            {
                j = snc;
                i = snr;
                errorSys("Error in generating ignition points",STOP);		
            }	*/

            LDPOINT p = m_pFireSites.return_a_point_from_index(index, cellNo);
            tempNo = (p.y - 1) * snc + p.x;
            if (m_pIgnitionStatusArray[tempNo] == 0)
            {
                m_pIgnitionStatusArray[tempNo] = 1;
            }
            return p;
        }

        public double fireDuration(double mean, double std)
        {
            /*
            //generate random fire duration based on lognormal distribution	
            //if x is fire size following lognormal distribution with mean MFS and variance as VAR
            //then log(x) follows normal distribution with
            //mean = 2logMFS - 1/2log(VAR+MFS square)
            //variance = log(VAR+MFS square) - 2logMFS
            double NormalMean,NormalStd, NormalVar;
            double var;	
            var = std * std;
            NormalMean = 2.0 * log(mean) - 0.5 * log(var + mean*mean);
            NormalVar = log(1.0*(var + mean*mean)) - 2.0 * log(mean);
            NormalStd = sqrt(NormalVar);	
            double duration;
            duration = m_pStochastic->Normal(NormalMean,NormalStd);
            duration = exp(duration);
            return (float) duration;
            */
            //generate negative exponential distribution with input mean
            double duration;
            duration = std + m_pStochastic.Exponential(mean);
            return duration;
        }

        public void WriteInitiationMap(int snr, int snc, int itr, double[] wAdfGeoTransform)
        {
            int i;
            string str = new string(new char[255]);
            str = string.Format("Initiation map for year {0:D}.", itr * m_pLAND.TimeStepFire);
            m_InitiationMap.setHeader(m_pLAND.Header);
            m_InitiationMap.rename(str);
            for (i = 0; i < Succession.Landispro.map8.maxLeg; i++)
            {
                m_InitiationMap.assignLeg((uint)i, "");
            }
            m_InitiationMap.assignLeg(0, "No initiations");
            m_InitiationMap.assignLeg(1, "one initiation");
            str = string.Format("{0}Initiation{1:D}", m_strFireOutputDirectory, itr * m_pLAND.TimeStepFire);
            m_InitiationMap.CellSize = m_fireParam.cellSize;
            m_InitiationMap.write(str, red, green, blue);
        }

        public void WriteCummInitiationMap(int snr, int snc, int itr, double[] wAdfGeoTransform)
        {
            int i;
            string str;
            str = "Cummulative Initiation map";
            m_cummInitiationMap.setHeader(m_pLAND.Header);
            m_cummInitiationMap.rename(str);
            for (i = 0; i < Succession.Landispro.map8.maxLeg; i++)
            {
                m_cummInitiationMap.assignLeg((uint)i, "");
            }
            m_cummInitiationMap.assignLeg(0, "No initiations");
            m_cummInitiationMap.assignLeg(1, "one initiation");
            m_cummInitiationMap.assignLeg(2, "two initiations");
            m_cummInitiationMap.assignLeg(3, "three initiations");
            str = string.Format("{0}CummInit", m_strFireOutputDirectory);
            m_cummInitiationMap.CellSize = m_fireParam.cellSize;
            m_cummInitiationMap.write(str, red, green, blue);
        }

        public void FinneyInsertList(CFinneyCell inCell)
        {
            if (FinneyList.Count == 0)
            {
                FinneyList.Add(inCell);
                return;
            }

            int location = -1;
            for (int i=0; i < FinneyList.Count(); i++)
            {
                if (FinneyList[i] >= inCell)
                    location = i;
            }
            if (location != -1)
            {
                FinneyList.Insert(location, inCell);
            }
            else
            {
                FinneyList.Add(inCell);
            }
        }

        public double ProductLog(double kernel)
        {
            int i;
            const double eps = 4.0e-16;
            const double em1 = 0.3678794411714423215955237701614608;
            double p;
            double e;
            double t;
            double w;
            if (kernel < -em1)
            {
                return -1.0;
                //string pszMessage = new string(new char[1024]);
                //pszMessage = string.Format("LambertW: bad argument {0:g}, exiting. Code PL1\n", kernel);
                //Console.Error.Write("LambertW: bad argument {0:g}, exiting.\n", kernel);
                //Environment.Exit(1);
            }
            if (0.0 == kernel)
            {
                return 0.0;
            }
            if (kernel < -em1 + 1e-4)
            { // series near -em1 in sqrt(q)
                double q = kernel + em1;
                double r = Math.Sqrt(q);
                double q2 = q * q;
                double q3 = q2 * q;
                return -1.0 + 2.331643981597124203363536062168 * r - 1.812187885639363490240191647568 * q + 1.936631114492359755363277457668 * r * q - 2.353551201881614516821543561516 * q2 + 3.066858901050631912893148922704 * r * q2 - 4.175335600258177138854984177460 * q3 + 5.858023729874774148815053846119 * r * q3 - 8.401032217523977370984161688514 * q3 * q; // error approx 1e-16

            }
            /* initial approx for iteration... */
            if (kernel < 1.0)
            { // series near 0
                p = Math.Sqrt(2.0 * (2.7182818284590452353602874713526625 * kernel + 1.0));
                w = -1.0 + p * (1.0 + p * (-0.333333333333333333333 + p * 0.152777777777777777777777));

            }
            else
            {
                w = Math.Log(kernel); // asymptotic
            }
            if (kernel > 3.0)
            {
                w -= Math.Log(w); // useful?
            }
            for (i = 0; i < 15; i++)
            { // Halley iteration
                e = Math.Exp(w);
                t = w * e - kernel;
                p = w + 1.0;
                t /= e * p - 0.5 * (p + 1.0) * t / p;
                w -= t;
                if (Math.Abs(t) < eps * (1.0 + Math.Abs(w)))
                {
                    return w; // rel-abs error
                }
            }
            /* should never get here */
            return w;
            //string pszMessage = new string(new char[1024]);
            //pszMessage = string.Format("LambertW: bad argument {0:g}, exiting. Code PL{1:g}\n", kernel, w);
            //Console.Error.Write("LambertW: No convergence at kernel={0:g}, exiting.\n", kernel);
            //Environment.Exit(1);
        }

        public double FinneyCalculateAcceleration(int from_x, int from_y, int to_x, int to_y, double weight)
        {
            // calculate the time for fire traveling from the point (FROM) to the point (TO) and return it
            // meanwhile the function will update the actualROSMap at the point (TO)
            // theApp.m_parameterSet.iCellSize * (m_fuelCostMap[to_x][to_y] + m_fuelCostMap[from_x][from_y]) * 0.5 * weight;
            double a = 0.115;
            const double eps = 0.00000001;
            double dist;
            double y;
            double t_alpha;
            double t_beta;
            double ROS_boundary;
            double ROS_beta;
            double PL_Kernel;
            dist = m_fireParam.cellSize * 0.5 * weight;
            if (m_fuelCostMap[from_x][from_y] > eps)
            {
                y = 1 - m_actualROSMap[from_x][from_y] / m_fuelCostMap[from_x][from_y];
                PL_Kernel = -1 * y * Math.Exp(-1 * (a * dist / m_fuelCostMap[from_x][from_y] + y));
                t_alpha = dist / m_fuelCostMap[from_x][from_y] + 1 / a * y + 1 / a * ProductLog(PL_Kernel);
                ROS_boundary = m_fuelCostMap[from_x][from_y] * (1 - y * Math.Exp(-1 * a * t_alpha));
            }
            else
            {
                return double.MaxValue;
            }

            if (m_fuelCostMap[to_x][to_y] > ROS_boundary && m_fuelCostMap[to_x][to_y] > eps)
            {
                y = 1 - ROS_boundary / m_fuelCostMap[to_x][to_y];
                PL_Kernel = -1 * y * Math.Exp(-1 * (a * dist / m_fuelCostMap[to_x][to_y] + y));
                t_beta = dist / m_fuelCostMap[to_x][to_y] + 1 / a * y + 1 / a * ProductLog(PL_Kernel);
                ROS_beta = m_fuelCostMap[to_x][to_y] * (1 - y * Math.Exp(-1 * a * t_beta));
            }
            else
            {
                if (m_fuelCostMap[to_x][to_y] > eps)
                {
                    t_beta = dist / m_fuelCostMap[to_x][to_y];
                    ROS_beta = m_fuelCostMap[to_x][to_y];
                }
                else
                {
                    return double.MaxValue;
                }
            }
            m_actualROSMap[to_x][to_y] = ROS_beta;
            double tempCost;
            tempCost = t_alpha + t_beta;
            return tempCost;
        }

    }
}
