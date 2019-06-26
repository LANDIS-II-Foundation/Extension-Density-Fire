using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Landis.Extension.Landispro.Fire
{
    class FireExport
    {
        public CFIRE GetFire(string strfn, int mode, Succession.Landispro.sites outsites, Succession.Landispro.landunits outlus, Succession.Landispro.speciesattrs outsa, Succession.Landispro.pdp ppdp, int nFNOI, string strOutput, int randSeed)
        {
            CFIRE pFire;
            pFire = new CFIRE(strfn, mode, outsites, outlus, outsa, ppdp, nFNOI, strOutput, randSeed);
            return pFire;
            //return new CFIRE(strfn, mode, outsites, outlus, outsa, ppdp, nFNOI,strOutput);
        }

        public void GetFireACTIVATE(CFIRE pf, int itr, int[] freq, double[] wAdfGeoTransform)
        {
            //  pw->activate(itr, freq, s, sp, cellSize, randSeed, numberOfIterations, gDLLMode);
            pf.Activate(itr, freq, wAdfGeoTransform);
        }
    }
}
