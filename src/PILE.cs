#if !PILE_H

#define PILE_H

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Landis.Extension.Landispro.Fire
{
    class PILE
    {
        public const int MAXPILE = 25000; //changed from 25000 to 16000

        private LDPOINT[] items = new LDPOINT[MAXPILE];
        private int numItems;

        public PILE()
        //Constructor.
        {
            numItems = 0;
        }

        public bool isEmpty()
        //This returns true if the pile is empty, false otherwise.
        {
            if (numItems == 0) return true;
            else return false;
        }

        public bool push(LDPOINT p)
        //Pushes p onto the pile.  Returns true if unsuccesful, false otherwise.
        {
            if (numItems < (MAXPILE - 1))
            {
                items[numItems++] = p;
                return false;
            }
            else
            {
                Console.WriteLine("MAXIMUM PILE SPACE EXCEEDED!.");
                return true;
            }
        }

        public LDPOINT pull()
        //Pulls p off of the pile.
        {
            LDPOINT p = new LDPOINT();
            int pos = (int)(Landis.Extension.Succession.Landispro.system1.frand() * (float)numItems);
            if (pos < 0 || pos >= numItems)
            {
                pos = 0;
            }
            p = items[pos];
            for (int i = pos; i < numItems - 1; i++)
            {
                items[i] = items[i + 1];
            }
            numItems--;
            return p;
        }

        public void reset()
        //This will reset the pile to an empty state.
        {
            numItems = 0;
        }

    }
}
#endif
