using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Landis.Core;
using System.Diagnostics;
using Landis.SpatialModeling;
using System.IO;



namespace Landis.Extension.Landispro.Fire
{
    class PlugIn
        : ExtensionMain
    {
        public static readonly ExtensionType ExtType = new ExtensionType("disturbance:fire");
        public static readonly string ExtensionName = "Landis_pro Fire";
        private CFIRE pFire;

        private static ICore modelCore;

        public PlugIn()
            : base(ExtensionName, ExtType)
        {
        }

        public static ICore ModelCore
        {
            get
            {
                return modelCore;
            }
            private set
            {
                modelCore = value;
            }
        }

        public override void LoadParameters(string dataFile, ICore mCore)
        {
            modelCore = mCore;
            Console.WriteLine("The datafile is: " + dataFile);
            Console.WriteLine();

            Console.WriteLine("FIRE Dll loaded in...");
            Console.WriteLine();

            int gDLLMode = 0;
            gDLLMode = gDLLMode | Succession.Landispro.defines.G_FIRE;

            //Todo
            pFire = new CFIRE(dataFile, 
                gDLLMode, 
                Succession.Landispro.PlugIn.gl_sites, 
                Succession.Landispro.PlugIn.gl_landUnits, 
                Succession.Landispro.PlugIn.gl_spe_Attrs, 
                Succession.Landispro.PlugIn.pPDP, 
                Succession.Landispro.PlugIn.gl_param.Num_Iteration, 
                Succession.Landispro.PlugIn.gl_param.OutputDir, 
                Succession.Landispro.PlugIn.gl_param.RandSeed);
            Console.WriteLine("End Landis_pro FIRE Parameters Loading\n");
            Console.WriteLine("========================================\n");
        }

        public override void Initialize()
        {
            Console.WriteLine("Running Landis_pro FIRE Initialization...");
            Console.WriteLine();

            Landis.Extension.Succession.Landispro.PlugIn.gl_sites.stocking_x_value = Landis.Extension.Succession.Landispro.PlugIn.gl_param.Stocking_x_value;
            Landis.Extension.Succession.Landispro.PlugIn.gl_sites.stocking_y_value = Landis.Extension.Succession.Landispro.PlugIn.gl_param.Stocking_y_value;
            Landis.Extension.Succession.Landispro.PlugIn.gl_sites.stocking_z_value = Landis.Extension.Succession.Landispro.PlugIn.gl_param.Stocking_z_value;
            Landis.Extension.Succession.Landispro.PlugIn.gl_sites.SuccessionTimeStep = Landis.Extension.Succession.Landispro.PlugIn.gl_param.SuccessionTimestep;
            // FIRE TIMESTEP
            Landis.Extension.Succession.Landispro.PlugIn.gl_sites.TimeStepFire = Landis.Extension.Succession.Landispro.PlugIn.gl_param.TimeStepFire;
            Timestep = Landis.Extension.Succession.Landispro.PlugIn.gl_sites.TimeStepFire;

            Landis.Extension.Succession.Landispro.PlugIn.numSpecies = Landis.Extension.Succession.Landispro.PlugIn.gl_spe_Attrs.NumAttrs;

            Console.WriteLine("End Landis_pro FIRE Initialization...\n");
            Console.WriteLine("========================================\n");
        }

        public override void Run()
        {
            int i = modelCore.TimeSinceStart;

            if (i % Landis.Extension.Succession.Landispro.PlugIn.gl_sites.TimeStepFire == 0)
            {
                //And false means never run???
                if ((Succession.Landispro.PlugIn.envOn > 0) && (i % Succession.Landispro.PlugIn.envOn == 0) && i > 1 && false)
                {
                    //update fire regime unit attr and fire regime GIS
                    pFire.updateFRU(i);
                    Console.WriteLine("fire regime unit attribute and gis has been updated at iteration {0}", i);
                }                   

                if (pFire.flag_regime_update == 1)
                {
                    pFire.updateFire_Regime_Map(i);
                }
            }
            singularLandisIteration(i, Landis.Extension.Succession.Landispro.PlugIn.pPDP);

            Console.WriteLine("End Landis_pro FIRE Once");
            Console.WriteLine();
        }

        public void singularLandisIteration(int itr, Landis.Extension.Succession.Landispro.pdp ppdp)
        {
            DateTime ltime, time1, time2, time3, time4, ltimeTemp;
            TimeSpan ltimeDiff;

            string fptimeBU = Landis.Extension.Succession.Landispro.PlugIn.fpforTimeBU_name;
            using (StreamWriter fpforTimeBU = File.AppendText(Landis.Extension.Succession.Landispro.PlugIn.fpforTimeBU_name))
            {
                fpforTimeBU.WriteLine("\nProcessing succession at Year: {0}:", itr);

                if (itr % Succession.Landispro.PlugIn.gl_sites.TimeStepFire == 0)
                {
                    itr /= Succession.Landispro.PlugIn.gl_sites.TimeStepFire;

                    ltime = DateTime.Now;
                    Console.WriteLine("\nStart simulating fire disturbance ... at {0}.", ltime);

                    Succession.Landispro.system1.fseed(Succession.Landispro.PlugIn.gl_param.RandSeed + itr/Succession.Landispro.PlugIn.gl_sites.SuccessionTimeStep * 1);

                    time1 = DateTime.Now;
                    pFire.Activate(itr, Succession.Landispro.PlugIn.freq, Succession.Landispro.PlugIn.wAdfGeoTransform);

                    time2 = DateTime.Now;
                    pFire.Activate(itr, Succession.Landispro.PlugIn.freq, Succession.Landispro.PlugIn.wAdfGeoTransform);

                    time3 = DateTime.Now;
                    pFire.Activate(itr, Succession.Landispro.PlugIn.freq, Succession.Landispro.PlugIn.wAdfGeoTransform);

                    time4 = DateTime.Now;
                    pFire.Activate(itr / Succession.Landispro.PlugIn.gl_sites.TimeStepFire, Succession.Landispro.PlugIn.freq, Succession.Landispro.PlugIn.wAdfGeoTransform);


                    ltimeTemp = DateTime.Now;
                    ltimeDiff = ltimeTemp - ltime;
                    Console.WriteLine("Finish simulating fire disturbance at {0} took {1} seconds", DateTime.Now, ltimeDiff);
                    fpforTimeBU.WriteLine("Processing fire: " + ltimeDiff + " seconds");
                }
            }
        }
    }
}