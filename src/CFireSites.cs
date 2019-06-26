#if !FIRESITES_H

#define FIRESITES_H

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Landis.Extension.Landispro.Fire
{
    public class FIRESITE
    {
        public int FRUIndex;
        public int DEM; //elevation, data type should be discussed, J.Yang
        public int numofsites; //Add By Qia on Nov 24 2008

        public FIRESITE() { }
    }

    //typedef std::vector<FIRESITE *>  SortedFIRESITE;
    //SortedFIRESITE === List<FIRESITE>; 

    class CFireSites
    {
        public int m_iRows;
        public int m_iCols;
        //<Add By Qia on Nov 24 2008>
        public List<FIRESITE> SortedIndex = new List<FIRESITE>();
        public List<List<LDPOINT>> FireRegimeUnitsList = new List<List<LDPOINT>>();
        public int[] FireRegimeCurrentIndex = new int[70000];


        private FIRESITE[] map;
        private FIRESITE sitetouse; //Add By Qia on Dec 19 2008

        public CFireSites()
        {
            m_iRows = 0;
            m_iCols = 0;
            //m_pFireLAND = NULL;//commented By Qia on Nov 24 2008
            for (int i = 0; i < 70000; i++)
            {
                FireRegimeUnitsList.Add(new List<LDPOINT>());
            }
        }
        public CFireSites(int a, int b)
        {
            m_iRows = a;
            m_iCols = b;
            //m_pFireLAND = new FIRESITE[i*j]; //commented By Qia on Nov 24 2008
            map = new FIRESITE[a * b]; //Add By Qia on Nov 24 2008
            Console.WriteLine("LandisPro fire map allocated");
            FIRESITE temp;
            int x;
            sitetouse = new FIRESITE();
            temp = new FIRESITE();
            temp.FRUIndex = 0;
            temp.DEM = 0;
            temp.numofsites = a * b;
            SortedIndex.Add(temp);
            for (int i = 1; i <= m_iRows; i++)
            {
                for (int j = 1; j <= m_iCols; j++)
                {
                    //this->operator ()(i,j)->DEM = 0;
                    x = (i - 1) * m_iCols;
                    x = x + j - 1;
                    map[x] = temp;
                }
            }

            for (int i=0; i<70000; i++)
            {
                FireRegimeUnitsList.Add(new List<LDPOINT>());
            }
        }

        public void Dispose()
        {
            //if (m_pFireLAND)
            //delete [] m_pFireLAND;  //commented By Qia on Nov 24 2008
            if (map != null)
            {
                map = null; //Add By Qia on Nov 24 2008
            }
            //<Add By Qia on Nov 24 2008>

            SortedIndex.Clear();
            //for (int i = 0; i < SortedIndex.Count; i++)
            //{
            //    FIRESITE temp;
            //    temp = SortedIndex[i];
            //    temp = null;
            //}
            //</Add By Qia on Nov 24 2008>
            sitetouse = null;
        }

        public FIRESITE this[int i, int j]
        {
            get
            {
                int x;
                if (i <= 0 || i > m_iRows || j <= 0 || j > m_iCols)
                {
                    string err;
                    err = string.Format("CFireSites::operator() (int,int)-> ({0:D}, {1:D}) are illegal map                  coordinates", i, j);
                    throw new Exception(err);
                }
                x = (i - 1) * m_iCols;
                x = x + j - 1;
                return map[x];
            }
            set{  }
        }

        public int numRows()
        {
            return m_iRows;
        }

        public void create_FireRegimeUnitsListByIndex()
        {
            int i = 0;
            int j = 0;
            int x;
            int max_FRUIndex = 0;
            //Console.WriteLine("{0}, {1}" ,m_iRows, m_iCols);
            for (i = 1; i <= m_iRows; i++)
            {
                for (j = 1; j <= m_iCols; j++)
                {
                    x = (i - 1) * m_iCols;
                    x = x + j - 1;
                    LDPOINT p = new LDPOINT(j, i);
                    FireRegimeUnitsList[map[x].FRUIndex].Add(p);
                }
            }
            for (i = 0; i < 70000; i++)
            {
                FireRegimeCurrentIndex[i] = 0;
            }
        }

        public LDPOINT return_a_point_from_index(int index, int cellNo)
        {
            if (cellNo > FireRegimeUnitsList[index].Count)
            {
                Console.Write("Fire index error -");
                Console.Write(index);
                Console.WriteLine();
                throw new Exception("Fire index error, contact development team");
            }
            cellNo--;
            LDPOINT p = FireRegimeUnitsList[index][cellNo];
            //std::vector<LDPOINT>::iterator sitePtr;
            //sitePtr = FireRegimeUnitsList[index].begin();
            //FireRegimeUnitsList[index].erase(sitePtr + cellNo);
            FireRegimeUnitsList[index].RemoveAt(cellNo);

            FireRegimeCurrentIndex[index] = (FireRegimeCurrentIndex[index] + 1) % 70000;
            return p;
        }

        public int numColumns()
        {
            return m_iCols;
        }

        public int number()
        //This will return the total number of sites
        {
            return numRows() * numColumns();
        }

        public int SITE_compare(int site1_x, int site1_y, int site2_x, int site2_y)
        {
            int x;
            int y;
            int result;
            FIRESITE site1;
            FIRESITE site2;

            x = (site1_x - 1) * m_iCols;
            x = x + site1_y - 1;
            y = (site2_x - 1) * m_iCols;
            y = y + site2_y - 1;

            site1 = map[x];
            site2 = map[y];
            result = SITE_compare(site1, site2);
            return result;

        }

        public int SITE_compare(FIRESITE site1, FIRESITE site2)
        {
            if (site1.FRUIndex > site2.FRUIndex)
            {
                return 1;
            }
            if (site1.FRUIndex < site2.FRUIndex)
            {
                return 2;
            }
            if (site1.DEM > site2.DEM)
            {
                return 1;
            }
            if (site1.DEM < site2.DEM)
            {
                return 2;
            }
            return 0;
        }
        public void fillinSitePt(int i, int j, FIRESITE site)
        {
            int x;
            x = (i - 1) * m_iCols;
            x = x + j - 1;
            map[x] = site;
        }

        public FIRESITE locateSitePt(int i, int j)
        {
            int x;
            x = (i - 1) * m_iCols;
            x = x + j - 1;
            return map[x];
        }

        public int SITE_delete(int pos_sortIndex, FIRESITE site, int i, int j)
        //When a site disappears, delete it
        {
            int x;
            
            x = (i - 1) * m_iCols;
            x = x + j - 1;
            if (site != SortedIndex[pos_sortIndex])
            {
                return 0;
            }
            if (site != map[x])
            {
                return 0;
            }
            site = null;
            //std::vector<FIRESITE*>::iterator temp_sitePtr;
            //temp_sitePtr = SortedIndex.begin();
            SortedIndex.RemoveAt(pos_sortIndex);
            return 1;
        }

        public int SITE_insert(int pos_sortIndex, FIRESITE site, int i, int j)
        //when there is a new site during succession or whatever, we need to 
        //check if the new site already exists, if yes combine with existing one
        //if not insert to the position according to sort
        {
            int x;
            int ifexist = 0;
            int pos = 0;
            FIRESITE temp;
            x = (i - 1) * m_iCols;
            x = x + j - 1;
            
            SITE_LocateinSortIndex(site, ref pos, ref ifexist);
            if (ifexist != 0)
            {
                map[x] = SortedIndex[pos];
                map[x].numofsites++;
                //delete  site;
            }
            else
            {
                temp = new FIRESITE();
                temp.FRUIndex = site.FRUIndex;
                temp.DEM = site.DEM;
                temp.numofsites = 1;
                map[x] = temp;
                //std::vector<FIRESITE*>::iterator temp_sitePtr;
                //temp_sitePtr = SortedIndex.begin();
                SortedIndex.Insert(pos, temp);
            }
            return 1;
        }

        public int SITE_LocateinSortIndex(FIRESITE site, ref int pos, ref int ifexist)
        //Find if a new site exists in sorted list
        //If a new site exists, find its location and set *ifexist as 1
        //if this no site matches this one, find location before which new site pointer should be inserted
        //By Qia Oct 09 2008
        {
            int begin;
            int end;
            int mid;
            FIRESITE temp;
            int temp_flag;
            ifexist = 0;
            begin = 0;
            end = SortedIndex.Count;

            if (end == 0)
            {
                Console.Write("No site at all wrong wrong wrong\n");
                return -1;
            }

            end--;
            mid = (begin + end) / 2;
            temp = SortedIndex[mid];

            while (begin < end)
            {
                temp_flag = SITE_compare(site, temp);
                if (temp_flag == 0)
                {
                    ifexist = 1;
                    pos = mid;
                    return 1;
                }
                else if (temp_flag == 1)
                {
                    begin = mid + 1;
                    mid = (begin + end) / 2;
                }
                else if (temp_flag == 2)
                {
                    end = mid - 1;
                    mid = (begin + end) / 2;
                }
                else
                {
                    return -1;
                }
                temp = SortedIndex[mid];

            }

            temp_flag = SITE_compare(site, temp);
            if (temp_flag == 0)
            {
                ifexist = 1;
                pos = mid;
                return 1;
            }
            else if (temp_flag == 2)
            {
                ifexist = 0;
                pos = mid;
                return 0;
            }
            else if (temp_flag == 1)
            {
                ifexist = 0;
                pos = mid + 1;
                return 0;
            }
            else
            {
                return -1;
            }
        }

        public int SITE_sort()
        //use babble algorithm to sort the initial site list array
        {
            int size;
            int i;
            int j;

            FIRESITE site1;
            FIRESITE site2;
            FIRESITE temp;

            size = SortedIndex.Count;
            for (i = SortedIndex.Count - 1; i > 0; i--)
            {
                for (j = 0; j <= i - 1; j++)
                {
                    site1 = SortedIndex[j];
                    site2 = SortedIndex[j + 1];
                    if (SITE_compare(site1, site2) == 1)
                    {
                        temp = SortedIndex[j];
                        SortedIndex[j] = SortedIndex[j + 1];
                        SortedIndex[j + 1] = temp;
                    }
                }
            }
            return 1;
        }

        public void BefStChg(int i, int j)
        //Before Site Change
        //This function back up a site and following changes are based on this seprated site
        //sort vector is not touched here
        {
            FIRESITE temp;
            temp = locateSitePt(i, j);
            sitetouse.FRUIndex = temp.FRUIndex;
            sitetouse.DEM = temp.DEM;

            if (temp.numofsites == 1)
            {
                int pos = 0;
                int ifexist = 0;
                SITE_LocateinSortIndex(sitetouse,ref pos,ref ifexist);

                if (ifexist != 0)
                {
                    //std::vector<FIRESITE*>::iterator temp_sitePtr;
                    //temp_sitePtr = SortedIndex.begin();
                    SortedIndex.RemoveAt(pos);
                    temp = null;
                }
                else
                {
                    Console.Write("num of vectors {0:D}\n", SortedIndex.Count);
                    Console.Write("ERROR ERROR ERROR ERROR!!~~~{0:D}\n", pos);
                }
            }
            else if (temp.numofsites <= 0)
            {
                Console.Write("FIRESITE NO NO NO NO NO\n");
            }
            else
            {
                temp.numofsites--;
            }
            fillinSitePt(i, j, sitetouse);
            return;
        }

        public void AftStChg(int i, int j)
        //After Site Change
        //This function does combination and delete of the seprated site made by BefStChg(int i, int j)
        //insert this site to the sorted vector
        {
            SITE_insert(0, sitetouse, i, j);
            return;
        }
        //</Add By Qia on Nov 24 2008>
    }
}
#endif