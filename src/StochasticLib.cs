﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Landis.Extension.Landispro.Fire
{
    class StochasticLib
        : TRandomMersenne
    {
        /***********************************************************************

                      constants

        ***********************************************************************/
        public const double SHAT1 = 2.943035529371538573; // 8/e
        public const double SHAT2 = 0.8989161620588987408; // 3-sqrt(12/e)
        //public TRandomMersenne tempRandom;
        const int MAXCOLORS = 100;
        /***********************************************************************

                     Error message function

        ***********************************************************************/
        protected void FatalError(string ErrorText)
        {
            // This function outputs an error message and aborts the program.
            //
            // Important: There is no universally portable way of printing an 
            // error message. You may have to modify this function to output
            // the error message in a way that is appropriate for your system
#if FatalAppExit
            // in Windows, use FatalAppExit:
            FatalAppExit(0, ErrorText);
#else
            // in console mode, print error message  
            Console.Write("\n{0}\n", ErrorText);
            // wait for user to press a key (remove this if not needed):
            Console.ReadKey(true);
            // make sure to catch user's attention in case standard output is not visible
            // (remove this if not needed):
            Debug.Assert(ErrorText != "Parameter out of range or other error in random generator library function");
#endif
            // Terminate program
            Environment.Exit(1);
        }

        /***********************************************************************

                              Poisson distribution

        ***********************************************************************/
        public virtual int Poisson(double L)
        {
            /*
               This function generates a random variate with the poisson distribution
               Uses inversion by chop-down method for L < 17, and ratio-of-uniforms
               method for L >= 17.
               For L < 1.E-6 numerical inaccuracy is avoided by direct calculation.
            */
            //------------------------------------------------------------------
            //                 choose method
            //------------------------------------------------------------------
            if (L < 17)
            {
                if (L < 0.000001)
                {
                    if (L == 0)
                    {
                        return 0;
                    }
                    if (L < 0)
                    {
                        FatalError("Parameter negative in poisson function");
                    }
                    //--------------------------------------------------------------
                    // calculate probabilities
                    //--------------------------------------------------------------
                    // For extremely small L we calculate the probabilities of x = 1
                    // and x = 2 (ignoring higher x). The reason for using this 
                    // method is to prevent numerical inaccuracies in other methods.
                    //--------------------------------------------------------------
                    return PoissonLow(L);
                }
                else
                {
                    //--------------------------------------------------------------
                    // inversion method
                    //--------------------------------------------------------------
                    // The computation time for this method grows with L.
                    // Gives overflow for L > 80
                    //--------------------------------------------------------------
                    return PoissonInver(L);
                }
            }
            else
            {
                if (L > 2000000000)
                {
                    FatalError("Parameter too big in poisson function");
                }
                //----------------------------------------------------------------
                // ratio-of-uniforms method
                //----------------------------------------------------------------
                // The computation time for this method does not depend on L.
                // Use where other methods would be slower.
                //----------------------------------------------------------------
                return PoissonRatioUniforms(L);
            }
        }
        /***********************************************************************

                      Binomial distribution

        ***********************************************************************/
        public virtual int Binomial(int n, double p)
        {
            /*
               This function generates a random variate with the binomial distribution.
               Uses inversion by chop-down method for n*p < 35, and ratio-of-uniforms
               method for n*p >= 35
               For n*p < 1.E-6 numerical inaccuracy is avoided by poisson approximation.
            */
            int inv = 0; // invert
            int x; // result
            double np = n * p;
            if (p > 0.5)
            { // faster calculation by inversion

                p = 1.0 - p;
                inv = 1;
            }
            if (n <= 0 || p <= 0)
            {
                if (n == 0 || p == 0)
                {
                    return inv * n; // only one possible result
                }

                FatalError("Parameter out or range in binomial function");
            } // error exit
            //------------------------------------------------------------------

            //                 choose method

            //------------------------------------------------------------------
            if (np < 35.0)
            {
                if (np < 0.000001)
                {
                    // Poisson approximation for extremely low np
                    x = PoissonLow(np);
                }
                else
                {
                    // inversion method, using chop-down search from 0
                    x = BinomialInver(n, p);
                }
            }
            else
            {
                // ratio of uniforms method
                x = BinomialRatioOfUniforms(n, p);
            }
            if (inv != 0)
            {
                x = n - x;
            } // undo inversion
            return x;
        }


        /***********************************************************************

                      Hypergeometric distribution

        ***********************************************************************/
        public virtual int Hypergeometric(int n, int m, int t)
        {
            /*
               This function generates a random variate with the hypergeometric
               distribution. This is the distribution you get when drawing balls without 
               replacement from an urn with two colors. n is the number of balls you take,
               m is the number of red balls in the urn, t is the total number of balls in 
               the urn, and the return value is the number of red balls you get.
               This function uses inversion by chop-down search from the mode when
               parameters are small, and the ratio-of-uniforms method when the former
               method would be too slow or would give overflow.
            */
            int fak; // used for undoing transformations
            int addd;
            int x; // result
            // check if parameters are valid
            if (n > t || m > t || n < 0 || m < 0)
            {
                FatalError("Parameter out of range in hypergeometric function");
            }
            // symmetry transformations
            fak = 1;
            addd = 0;
            if (m > t / 2)
            {
                // invert m
                m = t - m;
                fak = -1;
                addd = n;
            }
            if (n > t / 2)
            {
                // invert n
                n = t - n;
                addd += fak * m;
                fak = -fak;
            }
            if (n > m)
            {
                // swap n and m
                x = n;
                n = m;
                m = x;
            }
            // cases with only one possible result end here
            if (n == 0)
            {
                return addd;
            }
            //------------------------------------------------------------------

            //                 choose method

            //------------------------------------------------------------------
            if (t > 680 || n > 70)
            {
                // use ratio-of-uniforms method
                x = HypRatioOfUnifoms(n, m, t);
            }
            else
            {
                // inversion method, using chop-down search from mode
                x = HypInversionMod(n, m, t);
            }
            // undo symmetry transformations  
            return x * fak + addd;
        }


        /***********************************************************************

                Non-central Hypergeometric distribution

        ***********************************************************************/
        public int NonCentralHypergeometric(int n, int m, int t, double bias)
        {
            /*
               This function generates a random variate with the noncentral 
               hypergeometric distribution. Note, that the same name is sometimes used
               for the extended hypergeometric distribution.
               The noncentral hypergeometric distribution is the distribution you get when
               drawing balls without replacement from an urn containing red and white balls,
               with bias.
               We define the weight of the balls so that the probability of taking a
               particular ball is proportional to its weight. The value of bias is the
               normalized odds ratio: bias = weight(red) / weight(white).
               If all balls have the same weight, i.e. bias = 1, then we get the
               hypergeometric distribution.
               n is the number of balls you take,
               m is the number of red balls in the urn,
               t is the total number of balls in the urn, 
               bias is the odds ratio,
               and the return value is the number of red balls you get.
               No efficient method for sampling from this distribution has been developed,
               hence the execution may be slow in some cases. For moderate values of n, 
               this function simply simulates the urn experiment, taking one ball at a time.
               When n is high and the odds ratio not extreme, the distribution is 
               approximated by a sum of extended hypergeometric variates.
            */

            int D; // number of samples to add when approx. method used
            int i; // loop counter
            int x; // number of balls of color 1 sampled
            int x1;
            int m2; // number of balls of color 2 in urn
            int n1; // sample size
            double mw1; // probabilities of each color
            double mw2;
            // check parameters
            if (n >= t != false || m >= t != false || n <= 0 || m <= 0 || bias <= 0)
            {
                // trivial cases
                if (n == 0 || m == 0 || bias == 0)
                {
                    return 0;
                }
                if (m == t)
                {
                    return n;
                }
                if (n == t)
                {
                    return m;
                }
                // illegal parameter    
                FatalError("Parameter out of range in NonCentralHypergeometric function");
            }
            if (bias == 1)
            {
                // use hypergeometric function if bias == 1
                return Hypergeometric(n, m, t);
            }
            // compute number of samples D required for approximation and find out
            // which method is fastest
            if (n < 50 || n < 20 * (D = 2 + (int)(20 * Math.Abs(Math.Log(bias)))))
            {
                // use exact method: simulate urn experiment
                x = 0;
                m2 = t - m;
                mw1 = m * bias;
                mw2 = m2;
                do
                {
                    if (Random() * (mw1 + mw2) < mw1)
                    {
                        x++;
                        m--;
                        mw1 = m * bias;
                        if (m == 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        m2--;
                        mw2 = m2;
                        if (m2 == 0)
                        {
                            x += n - 1;
                            break;
                        }
                    }
                } while (--n != 0);
            }
            else
            {
                // approximate as sum of extended hypergeometrics
                n1 = n / D;
                x = 0;
                for (i = 1; i <= D; i++)
                {
                    if (i == D)
                    {
                        n1 = n - (D - 1) * n1;
                    }
                    x1 = ExtendedHypergeometric(n1, m, t, bias);
                    x += x1;
                    m -= x1;
                    t -= n1;
                }
            }
            return x;
        }


        /***********************************************************************

                        Extended Hypergeometric distribution

        ***********************************************************************/
        public int ExtendedHypergeometric(int n, int m, int t, double bias)
        {
            /*    
               This function generates a random variate with the extended hypergeometric
               distribution.
               This distribution resembles the noncentral hypergeometric distribution
               and the two distributions are sometimes confused. A more detailed 
               explanation of this distribution is given below under the multivariate
               extended hypergeometric distribution (MultiExtendedHypergeo).
               This function uses inversion by chop-down search from zero when parameters
               are small, and the ratio-of-uniforms rejection method when the former 
               method would be too slow or would give overflow.
            */
            int fak; // used for undoing transformations
            int addd;
            int x; // result
            // check if parameters are valid
            if (n > t || m > t || n < 0 || m < 0 || bias <= 0)
            {
                if (bias == 0)
                {
                    return 0;
                }
                FatalError("Parameter out of range in hypergeometric function");
            }
            if (bias == 1)
            {
                // use hypergeometric function if bias == 1
                return Hypergeometric(n, m, t);
            }
            // symmetry transformations
            fak = 1;
            addd = 0;
            if (m > t / 2)
            {
                // invert m
                m = t - m;
                fak = -1;
                addd = n;
            }
            if (n > t / 2)
            {
                // invert n
                n = t - n;
                addd += fak * m;
                fak = -fak;
            }
            if (n > m)
            {
                // swap n and m
                x = n;
                n = m;
                m = x;
            }
            // cases with only one possible result end here
            if (n == 0)
            {
                return addd;
            }
            if (fak == -1)
            {
                // reciprocal bias if inverting
                bias = 1.0 / bias;
            }
            //------------------------------------------------------------------

            //                 choose method

            //------------------------------------------------------------------
            if (n < 30 && t < 1024 && bias > 0.00001 && bias < 100000)
            {
                // use inversion by chop down method
                x = ExtendedHypergeometricInversion(n, m, t, bias);
            }
            else
            {
                // use ratio-of-uniforms method
                x = ExtendedHypergeometricRatioOfUnifoms(n, m, t, bias);
            }
            // undo transformations  
            return x * fak + addd;
        }

        /***********************************************************************

                      Normal distribution

        ***********************************************************************/
        public double Normal(double m, double s)
        {
            // normal distribution with mean m and standard deviation s
            double x1;
            double x2;
            double w;
            do
            {
                x1 = 2.0 * Random() - 1.0;
                x2 = 2.0 * Random() - 1.0;
                w = x1 * x1 + x2 * x2;
            } while (w >= 1.0 || w < 1E-30);
            w = Math.Sqrt((-2.0 * Math.Log(w)) / w);
            x1 *= w;
            // x2 *= w;  // a second normally distributed result not used
            return x1 * s + m;
        }

        /***********************************************************************

                      Bernoulli distribution

        ***********************************************************************/
        public bool Bernoulli(double p)
        {
            // Bernoulli distribution with parameter p. This function returns 
            // 0 or 1 with probability (1-p) and p, respectively.
            if (p < 0 || p > 1)
            {
                FatalError("Parameter out of range in bernoulli function");
            }
            return Random() < p;
        }

        /***********************************************************************

                      Uniform distribution

        ***********************************************************************/
        public uint Uniform(int min, int max)
        {
            return IRandom(min, max);
        }

        /***********************************************************************

                      Multinomial distribution

        ***********************************************************************/
        public void Multinomial(int[] destination, double[] source, int n, int colors)
        {
            /*
               This function generates a vector of random variates, each with the
               binomial distribution.       
               The multinomial distribution is the distribution you get when drawing
               balls from an urn with more than two colors, with replacement.
               Parameters:
               destination:    An output array to receive the number of balls of each 
                               color. Must have space for at least 'colors' elements.
               source:         An input array containing the probability or fraction 
                               of each color in the urn. Must have 'colors' elements.
                               All elements must be non-negative. The sum doesn't have
                               to be 1, but the sum must be positive.
               n:              The number of balls drawn from the urn.                   
               colors:         The number of possible colors. 
            */
            double s;
            double sum;
            int x;
            int i;
            if (n < 0 || colors < 0)
            {
                FatalError("Parameter negative in multinomial function");
            }
            if (colors == 0)
            {
                return;
            }
            // compute sum of probabilities
            for (i = 0, sum = 0; i < colors; i++)
            {
                s = source[i];
                if (s < 0)
                {
                    FatalError("Parameter negative in multinomial function");
                }
                sum += s;
            }
            if (sum == 0 && n > 0)
            {
                FatalError("Zero sum in multinomial function");
            }
            for (i = 0; i < colors - 1; i++)
            {
                // generate output by calling binomial (colors-1) times
                s = source[i];
                if (sum <= s)
                {
                    // this fixes two problems:
                    // 1. prevent division by 0 when sum = 0
                    // 2. prevent s/sum getting bigger than 1 in case of rounding errors
                    x = n;
                }
                else
                {
                    x = Binomial(n, s / sum);
                }
                n -= x;
                sum -= s;
                destination[i] = x;
            }
            // get the last one
            destination[i] = n;
        }

        public void Multinomial(int[] destination, int[] source, int n, int colors)
        {
            // same as above, with integer source
            int x;
            int p;
            int sum;
            int i;
            if (n < 0 || colors < 0)
            {
                FatalError("Parameter negative in multinomial function");
            }
            if (colors == 0)
            {
                return;
            }
            // compute sum of probabilities
            for (i = 0, sum = 0; i < colors; i++)
            {
                p = source[i];
                if (p < 0)
                {
                    FatalError("Parameter negative in multinomial function");
                }
                sum += p;
            }
            if (sum == 0 && n > 0)
            {
                FatalError("Zero sum in multinomial function");
            }
            for (i = 0; i < colors - 1; i++)
            {
                // generate output by calling binomial (colors-1) times
                if (sum == 0)
                {
                    destination[i] = 0;
                    continue;
                }
                p = source[i];
                x = Binomial(n, (double)p / sum);
                n -= x;
                sum -= p;
                destination[i] = x;
            }
            // get the last one
            destination[i] = n;
        }


        /***********************************************************************

                  Multivariate hypergeometric distribution

        ***********************************************************************/
        public void MultiHypergeo(int[] destination, int[] source, int n, int colors)
        {
            /*
               This function generates a vector of random variates, each with the
               hypergeometric distribution.
               The multivariate hypergeometric distribution is the distribution you 
               get when drawing balls from an urn with more than two colors, without
               replacement.
               Parameters:
               destination:    An output array to receive the number of balls of each 
                               color. Must have space for at least 'colors' elements.
               source:         An input array containing the number of balls of each 
                               color in the urn. Must have 'colors' elements.
                               All elements must be non-negative.
               n:              The number of balls drawn from the urn.
                               Can't exceed the total number of balls in the urn.
               colors:         The number of possible colors. 
            */

            int sum;
            int x;
            int y;
            int i;
            if (n < 0 || colors < 0)
            {
                FatalError("Parameter negative in multihypergeo function");
            }
            if (colors == 0)
            {
                return;
            }
            // compute total number of balls
            for (i = 0, sum = 0; i < colors; i++)
            {
                y = source[i];
                if (y < 0)
                {
                    FatalError("Parameter negative in multihypergeo function");
                }
                sum += y;
            }
            if (n > sum)
            {
                FatalError("n > sum in multihypergeo function");
            }
            for (i = 0; i < colors - 1; i++)
            {
                // generate output by calling hypergeometric colors-1 times
                y = source[i];
                x = Hypergeometric(n, y, sum);
                n -= x;
                sum -= y;
                destination[i] = x;
            }
            // get the last one
            destination[i] = n;
        }

        /***********************************************************************

           Multivariate noncentral hypergeometric distribution

        ***********************************************************************/
        public void MultiNonCentralHypergeo(int[] destination, int[] source, double[] weights, int n, int colors)
        {
            /*
               This function generates a vector of random variates with the 
               multivariate noncentral hypergeometric distribution.
               This distribution resembles the multivariate extended hypergeometric
               distribution and the two names are often confused, so make sure you
               get the right distribution.
               The multivariate noncentral hypergeometric distribution is the 
               distribution you get when drawing colored balls from an urn
               with any number of colors, without replacement, and with bias.
               The weights are defined so that the probability of taking a particular
               ball is proportional to its weight.
               Parameters:
               destination:    An output array to receive the number of balls of each 
                               color. Must have space for at least 'colors' elements.
               source:         An input array containing the number of balls of each 
                               color in the urn. Must have 'colors' elements.
                               All elements must be non-negative.
               weights:        The odds of each color. Must have 'colors' elements.
                               All elements must be non-negative.
               n:              The number of balls drawn from the urn.
                               Can't exceed the total number of balls with nonzero weight
                               in the urn.
               colors:         The number of possible colors.
               No efficient method for sampling from this distribution has been developed,
               hence the execution may be slow in some cases. For moderate values of n, 
               this function simply simulates the urn experiment, taking one ball at a time.
               When n is high and the odds ratio not extreme, the distribution is 
               approximated by a sum of extended hypergeometric variates.
               Tuning factors:
               f4:        Determines the number of partial samples when approximation 
                          method used
               MAXCOLORS  (defined in stocc.h): You may adjust MAXCOLORS to the maximum 
                           number of colors you need.
               The function will reduce the number of colors, if possible, by eliminating
               colors with zero weight or zero number and pooling together colors with the 
               same weight. The problem thus reduced is handled in the arrays osource, 
               oweights and osample of dimension colors2.
            */
            // constants and tuning factors
            float f4 = 20F; // decides precision of approximation method
                            // variables for both methods


            int[] order = new int[MAXCOLORS]; // sort order, index into source and destination
            int[] order2 = new int[MAXCOLORS]; // corresponding index into osource when equal weights have been pooled together
            int[] osource = new int[MAXCOLORS]; // contents of source, sorted by weight with equal weights pooled together
            int[] urn = new int[MAXCOLORS]; // balls from osource not taken yet
            int[] osample = new int[MAXCOLORS]; // balls sampled, sorted by weight
            double[] oweights = new double[MAXCOLORS]; // sorted list of weights
            int m; // number of items of one color
            int msum; // total number of items of several or all colors
            double w = 0; // weight of items of one color
            double wsum; // total weight of all items of several or all colors
            int i; // loop counters
            int j;
            int k;
            int l;
            int c = 0; // color index
            int c1;
            int c2;
            int colors2; // reduced number of colors, number of entries in osource
            // variables for exact method
            double[] wcum = new double[MAXCOLORS]; // list of accumulated probabilities
            double p; // probability

            // variables for approximation method
            int[] psample = new int[MAXCOLORS]; // partial sample
            int x = 0; // partial sample of one color
            int D; // number of partial samples
            int a; // color index delimiting weight group
            int b;
            int g;
            int ns; // size of partial sample
            int n1; // size of weight group sample
            int n2;
            int ng;
            int m1; // size of weight group
            int m2;
            double w1 = 0; // mean weight in group
            double w2 = 0;

            // check validity of parameters
            if (n < 0 || colors < 0 || colors > MAXCOLORS)
            {
                FatalError("Parameter out of range in function MultiNonCentralHypergeo");
            }
            if (colors == 0)
            {
                return;
            }
            if (n == 0)
            {
                for (i = 0; i < colors; i++)
                {
                    destination[i] = 0;
                }
                return;
            }

            // check validity of array parameters
            for (i = 0, msum = 0; i < colors; i++)
            {
                m = source[i];
                w = weights[i];
                if (m < 0 || w < 0)
                {
                    FatalError("Parameter negative in function MultiNonCentralHypergeo");
                }
                if ((int)w != 0)
                {
                    msum += m;
                }
            }

            // sort by weight, heaviest first
            for (i = 0; i < colors; i++)
            {
                order[i] = i;
            }
            for (i = 0; i < colors - 1; i++)
            {
                c = order[i];
                k = i;
                w = weights[c];
                if (source[c] == 0)
                {
                    w = 0;
                }
                for (j = i + 1; j < colors; j++)
                {
                    c2 = order[j];
                    //YYF  2019/4  if (weights[c2] > w && source[c2])
                    if (weights[c2] > w && source[c2] == 1)
                    {
                        w = weights[c2];
                        k = j;
                    }
                }
                order[i] = order[k];
                order[k] = c;
            }
            // Skip any items with zero weight
            // this solves all problems with zero weights
            while (colors != 0 && (weights[c = order[colors - 1]] == 0 || source[c] == 0))
            {
                colors--;
                destination[c] = 0;
            }
            if (n >= msum)
            {
                if (n > msum)
                {
                    FatalError("Taking more items than there are in function MultiNonCentralHypergeo");
                }
                for (i = 0; i < colors; i++)
                {
                    c = order[i];
                    destination[c] = source[c];
                }
                return;
            }

            // copy source and weights into ordered lists and pool together colors with same weight
            for (i = 0, c2 = -1; i < colors; i++)
            {
                c = order[i];
                if (i == 0 || weights[c] != w)
                {
                    c2++;
                    x = source[c];
                    oweights[c2] = w = weights[c];
                }
                else
                {
                    x += source[c];
                }
                urn[c2] = osource[c2] = x;
                order2[i] = c2;
                osample[c2] = 0;
            }
            colors2 = c2 + 1;

            // simple cases  
            if (colors2 == 1)
            {
                osample[0] = n;
            }
            if (colors2 == 2)
            {
                x = NonCentralHypergeometric(n, osource[0], msum, oweights[0] / oweights[1]);
                osample[0] = x;
                osample[1] = n - x;
            }

            if (colors2 > 2)
            {
                // compute number of samples D required for approximation and find out
                // which method is fastest
                if (n < 100 || n < 20 * (colors2 - 1) * (D = 2 + (int)(f4 * Math.Abs(Math.Log(oweights[colors2 - 1] / oweights[0])))))
                {
                    // use exact method: simulate urn experiment
                    // Make list of accumulated probabilities
                    for (i = 0, wsum = 0; i < colors2; i++)
                    {
                        wsum += urn[i] * oweights[i];
                        wcum[i] = wsum;
                    }
                    // take one item n times
                    do
                    {
                        p = Random() * wcum[colors2 - 1];
                        for (i = 0; i < colors2 - 1; i++)
                        {
                            if (p < wcum[i])
                            {
                                break;
                            }
                        }
                        n--;
                        osample[i]++;
                        urn[i]--;
                        if (urn[i] == 0)
                        { // check how many colors left
                            for (j = k = 0; j < colors2; j++)
                            {
                                if (urn[j] != 0)
                                {
                                    k++;
                                    c = j;
                                }
                            }
                            if (k == 1)
                            { // there is only one color left. stop
                                osample[c] += n;
                                break;
                            }
                        }
                        wsum = i > 0 ? wcum[i - 1] : 0.0;
                        for (j = i; j < colors2; j++)
                        {
                            wsum += urn[j] * oweights[j];
                            wcum[j] = wsum;
                        }
                    } while (n != 0);
                }
                else
                {
                    // approximate as sum of extended hypergeometrics
                    if (D > n / 4)
                    {
                        D = n / 4;
                    }
                    ns = n / D;
                    // divide weights into two groups, heavy and light
                    a = 0;
                    b = colors2 - 1;
                    w = Math.Sqrt(oweights[0] * oweights[colors2 - 1]);
                    do
                    {
                        c = (a + b) / 2;
                        if (oweights[c] > w)
                        {
                            a = c;
                        }
                        else
                        {
                            b = c;
                        }
                    } while (b > a + 1);
                    g = b; // heavy group goes from 0 to g-1, light group goes from g to colors2-1
                    // loop for D partial samples
                    for (l = 1; l <= D; l++)
                    {
                        if (l == D)
                        {
                            ns = n - (D - 1) * ns;
                        }
                        // Use approximation method 2 of MultiExtendedHypergeo instead of
                        // calling MultiExtendedHypergeo (psample, urn, oweights, ns, colors2)
                        // for the sake of speed:      
                        // calculate mean weight for heavy group

                        for (i = 0, m1 = 0, wsum = 0; i < g; i++)
                        {
                            m1 += urn[i];
                            wsum += oweights[i] * urn[i];
                        }
                        w1 = m1 != 0 ? wsum / m1 : 1;
                        // calculate mean weight for light group
                        for (i = g, m2 = 0, wsum = 0; i < colors2; i++)
                        {
                            m2 += urn[i];
                            wsum += oweights[i] * urn[i];
                        }
                        w2 = m2 != 0 ? wsum / m2 : 1;
                        // split sample n into heavy (n1) and light (n2)
                        n1 = ExtendedHypergeometric(ns, m1, m1 + m2, w1 / w2);
                        n2 = ns - n1;
                       // set parameters for group 1
                        a = 0;
                        b = g;
                        ng = n1;
                        // loop twice, for the two groops

                        for (k = 0; k < 2; k++)
                        {
                            // split group into single colors by calling ExtendedHypergeometric b-a-1 times
                            for (i = a; i < b - 1; i++)
                            {
                                m = urn[i];
                                w = oweights[i];
                                // calculate mean weight of remaining colors
                                for (j = i + 1, msum = 0, wsum = 0; j < b; j++)
                                {
                                    m1 = urn[j];
                                    w1 = oweights[j];
                                    msum += m1;
                                    wsum += m1 * w1;
                                }
                                if (w == w1)
                                {
                                    x = Hypergeometric(ng, m, msum + m);
                                }
                                else
                                {
                                    if (wsum == 0)
                                    {
                                        x = ng;
                                    }
                                    else
                                    {
                                        x = ExtendedHypergeometric(ng, m, msum + m, w * msum / wsum);
                                    }
                                }
                                psample[i] = x;
                                ng -= x;
                            }
                            // get the last one in the group
                            psample[i] = ng;
                            // set parameters for group 2
                            a = g;
                            b = colors2;
                            ng = n2;
                        }
                        // move partial sample      
                        for (j = 0; j < colors2; j++)
                        {
                            osample[j] += psample[j];
                            urn[j] -= psample[j];
                        }
                    }
                }
            }
            // un-sort sample into destination
            for (i = 0; i < colors; i++)
            {
                c1 = order[i];
                c2 = order2[i];
                if (source[c1] == osource[c2])
                {
                    destination[c1] = osample[c2];
                }
                else
                {
                    x = Hypergeometric(osample[c2], source[c1], osource[c2]);
                    destination[c1] = x;
                    osample[c2] -= x;
                    osource[c2] -= source[c1];
                }
            }
        }

        /***********************************************************************

           Multivariate extended hypergeometric distribution

        ***********************************************************************/
        public void MultiExtendedHypergeo(int[] destination, int[] source, double[] weights, int n, int colors)
        {
            /*
               This function generates a vector of random variates with the 
               multivariate extended hypergeometric distribution.
               This distribution resembles the multivariate noncentral hypergeometric
               distribution and the two names are often confused, so make sure you
               get the right distribution.
               This distribution is defined as the conditional distribution of 'colors' 
               independent binomial variates 
                  x[i] = binomial(source[i], p[i]) 
               on the condition that the sum of all x[i] is n.
               p[i] = q * weights[i] / (1 + q * weights[i]),
               q is a scale factor.
               This distribution is not normally associated with the metaphor or taking
               colored balls from an urn, but here I will apply an urn model, however
               farfetched, for the sake of analogy with the noncentral hypergeometric
               distribution and in order to explain the transformations used for reducing
               the problem, so here it goes: You are taking n balls without replacement
               from an urn containing balls of different colors. The balls have different
               weights which make the sampling biased in favor or the heavier balls.
               Before taking the balls you assign to each ball a probability of being 
               taken. These probabilities are calculated so that the expected total number
               of balls taken is n. Now you take or don't take each ball according to the
               assigned probabilities and count the total number of balls taken. If this
               number is not equal to n then put all the balls back in the urn and repeat
               the experiment. You may have to repeat this experiment many times before
               you have a sample containing exactly n balls.
               Parameters:
               destination:    An output array to receive the number of balls of each 
                               color. Must have space for at least 'colors' elements.
               source:         An input array containing the number of balls of each 
                               color in the urn. Must have 'colors' elements.
                               All elements must be non-negative.
               weights:        The odds of each color. Must have 'colors' elements.
                               All elements must be non-negative.
               n:              The number of balls drawn from the urn.
                               Can't exceed the total number of balls with nonzero weight
                               in the urn.
               colors:         The number of possible colors.
               The multivariate extended hypergeometric distribution is difficult to 
               generate accurately. Therefore, two different approximation methods are used.
               Method 1 is based on the generation of independent binomial variates
                  x[i] = binomial(source[i], p[i])
                  p[i] = q * weights[i] / (1 + q * weights[i])
                  where q is a scale factor
               The mean of x[i] is source[i]*p[i]
               q is adjusted so that the sum of the means is the sample size that we aim at:
                  summa(source[i]*p[i]) = aim
               This equation is solved for q by iteration.
               The aim is slightly below n:
                  aim = n - 0.5 * f1 * sqrt(n).
               If the actual sample size s = summa(x[i]) is above n or below n-f1*sqrt(n)
               then the sample is rejected and a new try is made. 
               If s < n then the process is repeated with n1 = n - s using method 1 again
               or, finally, method 2, and the samples are added to give a total sample size
               of n.
               Method 2 is based on the univariate extended hypergeometric distribution.
               The sample size n is split into two groups of colors, n1 for colors with
               high weight, and n2 for colors with low weight. The split is done using
               the extended hypergeometric function, where the weight for each group
               is the mean weight of all balls in the group. Each group is then split
               up further until there is only one color in each group. The color groups
               are arranged so that the variation in weight within each group is as low as 
               possible. If it is not possible to keep the ratio between the heaviest 
               and the lightest ball in a group below the factor f3, then method 1
               is used instead.
               Method 2 is much faster than method 1.
               Tuning factors:
               f1:        Determines the size of the acceptance interval for method 1.
                          Lower values giver higher precision. Higher values give higher
                          speed. Suggested interval 0 < f1 < 0.5
               f2:        Determines when to stop iteration of method 1 and take the last
                          sample using method 2. Lower values give slightly higher precision.
                          Suggested interval 0 < f2 < 1.
               f3:        Determines when to use method 2. f3 is the maximum odds ratio
                          within a group of colors. A lower value will make you use the
                          slow method 1 more. Suggested interval 1 < f3 < 10.
               MAXCOLORS  (defined in stocc.h): You may adjust MAXCOLORS to the maximum 
                          number of colors you need.
               The function will reduce the number of colors, if possible, by eliminating
               colors with zero weight or zero number and pooling together colors with the 
               same weight. A symmetry transformation is used if more than half the balls
               are taken. The problem thus reduced is handled in the arrays osource, 
               oweights and osample of dimension colors2.
            */
            // constants and tuning factors
            float f1 = (float)0.2; // decides precision of method 1.
            float f2 = (float)0.05; // determines when to stop method 1.
            float f3 = (float)2.0; // max odds ratio for which method 2 can be used
            // variables for both methods
            int[] order = new int[MAXCOLORS]; // sort order, index into source and destination
            int[] order2 = new int[MAXCOLORS]; // corresponding index into osource when equal weights have been pooled together
            int[] osource = new int[MAXCOLORS]; // contents of source, sorted by weight with equal weights pooled together       
            int[] urn = new int[MAXCOLORS]; // items not taken yet
            int[] osample = new int[MAXCOLORS]; // balls sampled, sorted by weight
            double[] oweights = new double[MAXCOLORS]; // sorted list of weights
            int m; // number of items of one color
            int msum; // total number of items of several or all colors
            double w = 0; // weight of items of one color
            double wsum; // total weight of all items of several or all colors
            int i; // loop counters
            int j;
            int k;
            int c = 0; // color index
            int c1;
            int c2;
            int colors2; // reduced number of colors, number of entries in osource
            int invert = 0; // 1 if symmetry transformation used
            int method; // calculation method

            // variables for method 1
            int[] sample = new int[MAXCOLORS]; // items tentatively taken
            int x = 0; // number of items of color i tentatively taken
            int nsample; // total number of items tentatively taken
            double aim; // sample size aimed at in binomial process
            double p; // probability in binomial process
            double q; // scale factor
            double d; // sample interval
            double a1; // temporary in iteration
            int s1; // minimum sample size
            int nmin; // lower limit for n for repeating with method 1

            // variables for method 2
            int a; // limits for weight group
            int b;
            int m1; // number of items in each weight group
            int m2;
            double w1; // mean weight of each weight group
            double w2;
            int n1; // sample size for each weight group
            int n2;
            double odds; // weight ratio

            // check validity of parameters
            if (n < 0 || colors < 0 || colors > MAXCOLORS)
            {
                FatalError("Parameter out of range in function MultiExtendedHypergeo");
            }
            if (colors == 0)
            {
                return;
            }
            if (n == 0)
            {
                for (i = 0; i < colors; i++)
                {
                    destination[i] = 0;
                }
                return;
            }

            // check validity of array parameters
            for (i = 0, msum = 0; i < colors; i++)
            {
                m = source[i];
                w = weights[i];
                if (m < 0 || w < 0)
                {
                    FatalError("Parameter negative in function MultiExtendedHypergeo");
                }
                if ((int)w != 0)
                {
                    msum += m;
                }
            }

            // sort by weight, heaviest first
            for (i = 0; i < colors; i++)
            {
                order[i] = i;
            }
            for (i = 0; i < colors - 1; i++)
            {
                c = order[i];
                k = i;
                w = weights[c];
                if (source[c] == 0)
                {
                    w = 0;
                }
                for (j = i + 1; j < colors; j++)
                {
                    c2 = order[j];
                    //YYF
                    if (weights[c2] > w && source[c2] == 1)
                    {
                        w = weights[c2];
                        k = j;
                    }
                }
                order[i] = order[k];
                order[k] = c;
            }
            // Skip any items with zero weight
            // this solves all problems with zero weights
            while (colors != 0 && (weights[c = order[colors - 1]] == 0 || source[c] == 0))
            {
                colors--;
                destination[c] = 0;
            }
            if (n >= msum)
            {
                if (n > msum)
                {
                    FatalError("Taking more items than there are in function MultiExtendedHypergeo");
                }
                for (i = 0; i < colors; i++)
                {
                    c = order[i];
                    destination[c] = source[c];
                }
                return;
            }
            if (n > msum / 2)
            {
                // improve accuracy by symmetry transformation
                for (i = 0, j = colors - 1; i < j; i++, j--)
                { // reverse order list
                    c = order[i];
                    order[i] = order[j];
                    order[j] = c;
                }
                n = msum - n;
                invert = 1;
            }
            // copy source and weights into ordered lists and pool together colors with same weight
            for (i = 0, c2 = -1; i < colors; i++)
            {
                c = order[i];
                if (i == 0 || weights[c] != w)
                {
                    c2++;
                    x = source[c];
                    oweights[c2] = w = invert != 0 ? 1.0 / weights[c] : weights[c];
                }
                else
                {
                    x += source[c];
                }
                urn[c2] = osource[c2] = x;
                order2[i] = c2;
                osample[c2] = 0;
            }
            colors2 = c2 + 1;
            // simple cases  
            if (colors2 == 1)
            {
                osample[0] = n;
            }
            if (colors2 == 2)
            {
                x = ExtendedHypergeometric(n, osource[0], msum, oweights[0] / oweights[1]);
                osample[0] = x;
                osample[1] = n - x;
            }
            if (colors2 > 2)
            {
                // check if method 2 is applicable:    
                // divide weights into two groups, heavy and light
                a = 0;
                b = colors2 - 1;
                w = Math.Sqrt(oweights[0] * oweights[colors2 - 1]);
                do
                {
                    c = (a + b) / 2;
                    if (oweights[c] > w)
                    {
                        a = c;
                    }
                    else
                    {
                        b = c;
                    }
                } while (b > a + 1);
                a = 0; // heavy group goes from a to b-1, light group goes from b to colors2-1
                // check if odds ratio in each group <= f3
                if (oweights[b] <= oweights[colors2 - 1] * f3 && oweights[a] <= oweights[b - 1] * f3)
                {
                    method = 2;
                } // method 2 can be used
                else
                {
                    method = 1;
                } // use method 1
                if (method == 1)
                {
                    // method 1:
                    // determine when to stop
                    nmin = (int)f2 * n;
                    if (nmin < 6)
                    {
                        nmin = 6;
                    }
                    // compute total weight
                    for (i = 0, wsum = 0; i < colors2; i++)
                    {
                        m = osource[i];
                        w = oweights[i];
                        wsum += w * m;
                    }
                    do
                    { // method 1 repetition loop (do at least once)
                        // decide the sample size we are aiming at
                        d = f1 * Math.Sqrt((float)n);
                        s1 = (int)(n - d);
                        if (s1 < 1)
                        {
                            s1 = 1;
                        }
                        aim = (n + s1) * 0.5;
                        // find scale factor q by iteration
                        q = aim * msum / ((msum - aim) * wsum);
                        do
                        {
                            for (i = 0, a1 = 0; i < colors2; i++)
                            {
                                a1 += urn[i] * oweights[i] * q / (1 + oweights[i] * q);
                            }
                            q *= aim * (msum - a1) / (a1 * (msum - aim));
                        } while (Math.Abs(a1 - aim) > 1);
                        do
                        { // make tentative sample until accepted
                            // generate (colors2) independent binomial variates
                            for (i = 0, nsample = 0; i < colors2; i++)
                            {
                                p = oweights[i] * q / (1 + oweights[i] * q);
                                x = Binomial(urn[i], p);
                                sample[i] = x;
                                nsample += x;
                            }
                        } while (nsample > n || nsample < s1);

                        // reject sample if we have taken too few or too many.
                        // accepted. add sample to osample and re-calculate wsum
                        for (i = 0, wsum = 0; i < colors2; i++)
                        {
                            x = sample[i];
                            osample[i] += x;
                            urn[i] -= x;
                            wsum += urn[i] * oweights[i];
                        }
                        n -= nsample;
                        msum -= nsample;
                    } while (n > nmin && wsum > 0);
                   // stop method 1 when nmin is reached, continue with method 2
                }
                // method 2:

                if (n != 0)
                {
                    // calculate mean weight for heavy group
                    for (i = a, m1 = 0, wsum = 0; i < b; i++)
                    {
                        m1 += urn[i];
                        wsum += oweights[i] * urn[i];
                    }
                    w1 = m1 != 0 ? wsum / m1 : 1;

                    // calculate mean weight for light group
                    for (i = b, m2 = 0, wsum = 0; i < colors2; i++)
                    {
                        m2 += urn[i];
                        wsum += oweights[i] * urn[i];
                    }
                    w2 = m2 != 0 ? wsum / m2 : 1;
                    // split sample n into heavy (n1) and light (n2)
                    n1 = ExtendedHypergeometric(n, m1, m1 + m2, w1 / w2);
                    n2 = n - n1;
                    n = n1;

                    // loop twice, for the two groops
                    for (k = 0; k < 2; k++)
                    {
                        // split group into single colors by calling ExtendedHypergeometric b-a-1 times
                        for (i = a; i < b - 1; i++)
                        {
                            m = urn[i];
                            w = oweights[i];
                            // calculate mean weight of remaining colors
                            for (j = i + 1, msum = 0, wsum = 0; j < b; j++)
                            {
                                m1 = urn[j];
                                w1 = oweights[j];
                                msum += m1;
                                wsum += m1 * w1;
                            }
                            if (w == w1)
                            {
                                x = Hypergeometric(n, m, msum + m);
                            }
                            else
                            {
                                if (wsum == 0)
                                {
                                    x = n;
                                }
                                else
                                {
                                    odds = w * msum / wsum;
                                    x = ExtendedHypergeometric(n, m, msum + m, odds);
                                }
                            }
                            osample[i] += x;
                            n -= x;
                        }
                        // get the last one in the group
                        osample[i] += n;
                        // set parameters for second group
                        a = b;
                        b = colors2;
                        n = n2;
                    }
                }
            }
            if (invert != 0)
            {
                // reverse symmetry transformation on result
                for (i = 0; i < colors2; i++)
                {
                    osample[i] = osource[i] - osample[i];
                }
            }

            // un-sort sample into destination
            for (i = 0; i < colors; i++)
            {
                c1 = order[i];
                c2 = order2[i];
                if (source[c1] == osource[c2])
                {
                    destination[c1] = osample[c2];
                }
                else
                {
                    x = Hypergeometric(osample[c2], source[c1], osource[c2]);
                    destination[c1] = x;
                    osample[c2] -= x;
                    osource[c2] -= source[c1];
                }
            }
        }

        /***********************************************************************

                      Shuffle function

        ***********************************************************************/
        public void Shuffle(int[] list, int min, int n)
        {
            /*
               This function makes a list of the n numbers from min to min+n-1
               in random order.
               The parameter 'list' must be an array with at least n elements.
               The array index goes from 0 to n-1.
               If you want to shuffle something else than integers then use the 
               integers in list as an index into a table of the items you want to shuffle.
            */

            int i;
            int j;
            int swap;
            // put numbers from min to min+n-1 into list
            for (i = 0, j = min; i < n; i++, j++)
            {
                list[i] = j;
            }
            // shuffle list
            for (i = 0; i < n - 1; i++)
            {
                // item number i has n-i numbers to choose between
                j = (int)IRandom(i, n - 1);
                // swap items i and j
                swap = list[j];
                list[j] = list[i];
                list[i] = swap;
            }
        }

        /***********************************************************************

                      Subfunctions used by poisson

        ***********************************************************************/
        public int PoissonLow(double L)
        {
            /*
               This subfunction generates a random variate with the poisson 
               distribution for extremely low values of L.
               The method is a simple calculation of the probabilities of x = 1
               and x = 2. Higher values are ignored.
               The reason for using this method is to avoid the numerical inaccuracies 
               in other methods.
            */
            double d;
            double r;
            d = Math.Sqrt(L);
            if (Random() >= d)
            {
                return 0;
            }
            r = Random() * d;
            if (r > L * (1.0 - L))
            {
                return 0;
            }
            if (r > 0.5 * L * L * (1.0 - L))
            {
                return 1;
            }
            return 2;
        }

        public static double p_L_last = -1.0; // previous value of L
        public static double p_f0; // value at x=0
        public int PoissonInver(double L)
        {
            /*
               This subfunction generates a random variate with the poisson 
               distribution using inversion by the chop down method (PIN).
               Execution time grows with L. Gives overflow for L > 80.
               The value of bound must be adjusted to the maximal value of L.
            */
            const int bound = 130; // safety bound. Must be > L + 8*sqrt(L).

            double r; // uniform random number
            double f; // function value
            int x; // return value
            if (L != p_L_last)
            { // set up
                p_L_last = L;
                p_f0 = Math.Exp(-L);
            } // f(0) = probability of x=0

            while (true)
            {
                r = Random();
                x = 0;
                f = p_f0;

                do
                { // recursive calculation: f(x) = f(x-1) * L / x
                    r -= f;
                    if (r <= 0)
                    {
                        return x;
                    }
                    x++;
                    f *= L;
                    r *= x;
                } while (x <= bound);// instead of f /= x
            }
        }

        public static double PoissonRatioUniforms_p_L_last = -1.0; // previous L
        public static double p_a; // hat center
        public static double p_h; // hat width
        public static double p_g; // ln(L)
        public static double p_q; // value at mode
        public static int p_bound; // upper bound
        public int PoissonRatioUniforms(double L)
        {
            /*
               This subfunction generates a random variate with the poisson 
               distribution using the ratio-of-uniforms rejection method (PRUAt).
               Execution time does not depend on L, except that it matters whether L
               is within the range where ln(n!) is tabulated.
               Reference: E. Stadlober: "The ratio of uniforms approach for generating
               discrete random variates". Journal of Computational and Applied Mathematics,
               vol. 31, no. 1, 1990, pp. 181-189.
            */
            int mode; // mode
            double u; // uniform random
            double lf; // ln(f(x))
            double x; // real sample
            int k; // integer sample

            if (PoissonRatioUniforms_p_L_last != L)
            {
                PoissonRatioUniforms_p_L_last = L; // Set-up
                p_a = L + 0.5; // hat center
                mode = (int)L; // mode
                p_g = Math.Log(L);
                p_q = mode * p_g - LnFac(mode); // value at mode
                p_h = Math.Sqrt(SHAT1 * (L + 0.5)) + SHAT2; // hat width
                p_bound = (int)(p_a + 6.0 * p_h);
            } // safety-bound

            while (true)
            {
                u = Random();
                if (u == 0)
                {
                    continue;
                }
                x = p_a + p_h * (Random() - 0.5) / u;
                if (x < 0 || x >= p_bound)
                {
                    continue; // reject if outside valid range
                }
                k = (int)(x);
                lf = k * p_g - LnFac(k) - p_q;
                if (lf >= u * (4.0 - u) - 3.0)
                {
                    break; // quick acceptance
                }
                if ((u * (u - lf) > 1.0) != false)
                {
                    continue; // quick rejection
                }
                if ((2.0 * Math.Log(u) <= lf) != false)
                {
                    break;
                }
            } // final acceptance
            return (k);
        }

        public int BinomialInver(int n, double p)
        {
            /* 
              Subfunction for Binomial distribution. Assumes p < 0.5.
              Uses inversion method by search starting at 0 (BIN).
              Gives overflow for n*p > 60.
              This method is fast when n*p is low. 
            */
            double f0;
            double f;
            double q;
            int bound;
            double pn;
            double r;
            double rc;
            int x;
            int n1;
            int i;

            // f(0) = probability of x=0 is (1-p)^n
            // fast calculation of (1-p)^n
            f0 = 1.0;
            pn = 1.0 - p;
            n1 = n;
            while (n1 != 0)
            {
                if ((n1 & 1) != 0)
                {
                    f0 *= pn;
                }
                pn *= pn;
                n1 >>= 1;
            }

            // calculate safety bound
            rc = (n + 1) * p;
            bound = (int)(rc + 11.0 * (Math.Sqrt(rc) + 1.0));
            if (bound > n)
            {
                bound = n;
            }
            q = p / (1.0 - p);

            while (true)
            {
                r = Random();
                // recursive calculation: f(x) = f(x-1) * (n-x+1)/x*p/(1-p)
                f = f0;
                x = 0;
                i = n;
                do
                {
                    r -= f;
                    if (r <= 0)
                    {
                        return x;
                    }
                    x++;
                    f *= q * i;
                    r *= x; // it is faster to multiply r by x than dividing f by x
                    i--;
                } while (x <= bound);
            }
        }

        public  static int b_n_last = -1; // last n
        public  static double b_p_last = -1.0; // last p
        public  static int b_mode; // mode
        public  static int b_bound; // upper bound
        public  static double b_a; // hat center
        public  static double b_h; // hat width
        public  static double b_g; // value at mode
        public  static double b_r1; // ln(p/(1-p))
        public int BinomialRatioOfUniforms(int n, double p)
        {
            /* 
              Subfunction for Binomial distribution. Assumes p < 0.5.
              Uses the Ratio-of-Uniforms rejection method (BRUAt).
              The computation time hardly depends on the parameters, except that it matters
              a lot whether parameters are within the range where the LnFac function is 
              tabulated.
              Reference: E. Stadlober: "The ratio of uniforms approach for generating
              discrete random variates". Journal of Computational and Applied Mathematics,
              vol. 31, no. 1, 1990, pp. 181-189.
            */
            double u; // uniform random
            double q1; // 1-p
            double np; // n*p
            double @var; // variance
            double lf; // ln(f(x))
            double x; // real sample
            int k; // integer sample
            if (b_n_last != n || b_p_last != p)
            { // Set_up
                b_n_last = n;
                b_p_last = p;
                q1 = 1.0 - p;
                np = n * p;
                b_mode = (int)(np + p); // mode
                b_a = np + 0.5; // hat center
                b_r1 = Math.Log(p / q1);
                b_g = LnFac(b_mode) + LnFac(n - b_mode);
                @var = np * q1; // variance
                b_h = Math.Sqrt(SHAT1 * (@var + 0.5)) + SHAT2; // hat width
                b_bound = (int)(b_a + 6.0 * b_h); // safety-bound
                if (b_bound > n)
                {
                    b_bound = n;
                }
            } // safety-bound

            while (true)
            { // rejection loop
                u = Random();
                if (u == 0)
                {
                    continue;
                }
                x = b_a + b_h * (Random() - 0.5) / u;
                if (x < 0 || (k = (int)x) > b_bound)
                {
                    continue; // reject if k is outside range
                }
                lf = (k - b_mode) * b_r1 + b_g - LnFac(k) - LnFac(n - k); // ln(f(k))
                if ((u * (4.0 - u) - 3.0 <= lf) != false)
                {
                    break; // lower squeeze accept
                }
                if ((u * (u - lf) > 1.0) != false)
                {
                    continue; // upper squeeze reject
                }
                if ((2.0 * Math.Log(u) <= lf) != false)
                {
                    break;
                }
            } // final acceptance
            return k;
        }

        public static int h_n_last = -1, h_m_last = -1, h_t_last = -1;
        public static int h_mode, h_mp, h_bound;
        public static double h_fm;
        public int HypInversionMod(int n, int m, int t)
        {
            /* 
              Subfunction for Hypergeometric distribution. Assumes 0 <= n <= m <= t/2.
              Overflow protection is needed when t > 680 or n > 75.
              Hypergeometric distribution by inversion method, using down-up 
              search starting at the mode using the chop-down technique (HMDU).
              This method is faster than the rejection method when the variance is low.
            */
            int L;
            int I;
            int K;
            double modef;
            double Mp;
            double np;
            double p;
            double c;
            double d;
            double U;
            double divisor;

            Mp = m + 1;
            np = n + 1;
            L = t - m - n;

            if (t != h_t_last || m != h_m_last || n != h_n_last)
            {
                // set-up when parameters have changed
                h_t_last = t;
                h_m_last = m;
                h_n_last = n;
                p = Mp / (t + 2.0);
                modef = np * p; // mode, real
                h_mode = (int)modef; // mode, integer
                if (h_mode == modef && p == 0.5)
                {
                    h_mp = h_mode--;
                }
                else
                {
                    h_mp = h_mode + 1;
                }
                // mode probability, using log factorial function
                // (may read directly from fac_table if t < FAK_LEN)
                h_fm = Math.Exp(LnFac(t - m) - LnFac(L + h_mode) - LnFac(n - h_mode) + LnFac(m) - LnFac(m - h_mode) - LnFac(h_mode) - LnFac(t) + LnFac(t - n) + LnFac(n));

                // safety bound - guarantees at least 17 significant decimal digits
                // bound = min(n, (long int)(modef + k*c'))
                h_bound = (int)(modef + 11.0 * Math.Sqrt(modef * (1.0 - p) * (1.0 - n / (double)t) + 1.0));
                if (h_bound > n)
                {
                    h_bound = n;
                }
            }

            // loop until accepted
            while (true)
            {
                U = Random(); // uniform random number to be converted
                if ((U -= h_fm) <= 0.0)
                {
                    return (h_mode);
                }
                c = d = h_fm;

                // alternating down- and upward search from the mode
                for (I = 1; I <= h_mode; I++)
                {
                    K = h_mp - I; // downward search
                    divisor = (np - K) * (Mp - K);
                    // Instead of dividing c with divisor, we multiply U and d because 
                    // multiplication is faster. This will give overflow if t > 800
                    U *= divisor;
                    d *= divisor;
                    c *= (double)K * (double)(L + K);
                    if ((U -= c) <= 0.0)
                    {
                        return (K - 1);
                    }

                    K = h_mode + I; // upward search
                    divisor = (double)K * (double)(L + K);
                    U *= divisor;
                    c *= divisor; // re-scale parameters to avoid time-consuming division
                    d *= (np - K) * (Mp - K);
                    if ((U -= d) <= 0.0)
                    {
                        return (K);
                    }
                    // Values of n > 75 or t > 680 may give overflow if you leave out this..
                    // overflow protection
                    // if (U > 1.E100) {U *= 1.E-100; c *= 1.E-100; d *= 1.E-100;}
                }

                // upward search from K = 2*mode + 1 to K = bound
                for (K = h_mp + h_mode; K <= h_bound; K++)
                {
                    divisor = (double)K * (double)(L + K);
                    U *= divisor;
                    d *= (np - K) * (Mp - K);
                    if ((U -= d) <= 0.0)
                    {
                        return (K);
                    }
                    // more overflow protection
                    // if (U > 1.E100) {U *= 1.E-100; d *= 1.E-100;}
                }
            }
        }

        public static int HypRatioOfUnifoms_h_t_last = -1, HypRatioOfUnifoms_h_m_last = -1, HypRatioOfUnifoms_h_n_last = -1; // previous parameters
        static int HypRatioOfUnifoms_h_bound; // upper bound
        static double h_a; // hat center
        static double h_h; // hat width
        static double h_g; // value at mode
        public int HypRatioOfUnifoms(int n, int m, int t)
        {
            /* 
              Subfunction for Hypergeometric distribution using the ratio-of-uniforms
              rejection method (HRUAt).
              This code is valid for 0 < n <= m <= t/2.
              The computation time hardly depends on the parameters, except that it matters
              a lot whether parameters are within the range where the LnFac function is 
              tabulated.
              Reference: E. Stadlober: "The ratio of uniforms approach for generating
              discrete random variates". Journal of Computational and Applied Mathematics,
              vol. 31, no. 1, 1990, pp. 181-189.
            */
            int L; // t-m-n
            int mode; // mode
            int k; // integer sample
            double x; // real sample
            double rtt; // 1/(t*(t+2))
            double my; // mean
            double @var; // variance
            double u; // uniform random
            double lf; // ln(f(x))

            L = t - m - n;
            if (HypRatioOfUnifoms_h_t_last != t || h_m_last != m || h_n_last != n)
            {
                HypRatioOfUnifoms_h_t_last = t;
                h_m_last = m;
                h_n_last = n; // Set-up
                rtt = 1.0 / ((double)t * (t + 2)); // make two divisions in one
                my = (double)n * m * rtt * (t + 2); // mean = n*m/t
                mode = (int)((double)(n + 1) * (double)(m + 1) * rtt * t); // mode = floor((n+1)*(m+1)/(t+2))
                @var = (double)n * m * (t - m) * (t - n) / ((double)t * t * (t - 1)); // variance
                h_h = Math.Sqrt(SHAT1 * (@var + 0.5)) + SHAT2; // hat width
                h_a = my + 0.5; // hat center
                h_g = fc_lnpk(mode, L, m, n); // maximum
                h_bound = (int)(h_a + 4.0 * h_h); // safety-bound

                if (HypRatioOfUnifoms_h_bound > n)
                {
                    HypRatioOfUnifoms_h_bound = n;
                }
            }

            while (true)
            {
                u = Random();
                if (u == 0)
                {
                    continue; // avoid division by 0
                }
                x = h_a + h_h * (Random() - 0.5) / u;

                if (x < 0)
                {
                    continue; // reject
                }
                k = (int)x;
                if (k > HypRatioOfUnifoms_h_bound)
                {
                    continue; // reject if outside safety bound
                }
                lf = h_g - fc_lnpk(k, L, m, n); // ln(f(k))
                if ((u * (4.0 - u) - 3.0 <= lf) != false)
                {
                    break; // lower squeeze accept
                }
                if ((u * (u - lf) > 1.0) != false)
                {
                    continue; // upper squeeze reject
                }
                if ((2.0 * Math.Log(u) <= lf) != false)
                {
                    break;
                }
            } // final acceptance
            return k;
        }

        static int xh_n_last = -1, xh_m_last = -1, xh_t_last = -1;
        static double xh_b_last = -1, xh_f0, xh_scale;
        public int ExtendedHypergeometricInversion(int n, int m, int t, double b)
        {
            /* 
              Subfunction for ExtendedHypergeometric distribution.
              Implements extended hypergeometric distribution by inversion method, 
              using chop-down search starting at zero.
              Valid only for 0 <= n <= m <= t/2.
              Without overflow check the parameters must be limited to n < 30, t < 1024,
              and 1.E-5 < bias < 1.E5. This limitation is OK because this method is slow
              for higher n.
              The execution time of this function grows with n.
              See the file nchyper.doc for theoretical explanation.
            */
            int x;
            int i;
            int j;
            int k;
            int L;

            double f;
            double u;
            double sum;
            double f1;
            double f2;

            L = t - m - n;
            if (n !=xh_n_last || m != xh_m_last || t != xh_t_last || b != xh_b_last)
            {
                // set-up
                xh_n_last = n;
                xh_m_last = m;
                xh_t_last = t;
                xh_b_last = b;
                // f(0) is set to an arbitrary value because it cancels out.
                // A low value is chosen to avoid overflow.
                xh_f0 = Math.Pow(10,-100);
                // calculate summation of e(x), using the formula:
                // f(x) = f(x-1) * (m-x+1)*(n-x+1)*b / (x*(L+x))
                // All divisions are avoided by scaling the parameters
                sum = f = xh_f0;
                xh_scale = 1.0;
                i = m;
                j = n;
                k = L + 1;
                for (x = 1; x <= n; x++)
                {
                    f1 = i * j * b;
                    f2 = x * k;
                    i--;
                    j--;
                    k++;
                    f *= f1;
                    sum *= f2;
                    xh_scale *= f2;
                    sum += f;
                    // overflow check. not needed if parameters are limited:
                    // if (sum > 1E100) {sum *= 1E-100; f *= 1E-100; xh_scale *= 1E-100;}
                }
                xh_f0 *= xh_scale;
                xh_scale = sum;
                // now f(0) = xh_f0 / xh_scale.
                // We are still avoiding all divisions by saving the scale factor
            }
            // uniform random
            u = Random() * xh_scale;
            // recursive calculation:
            // f(x) = f(x-1) * (m-x+1)*(n-x+1)*b / (x*(L+x))
            f = xh_f0;
            x = 0;
            i = m;
            j = n;
            k = L;
            do
            {
                u -= f;
                if (u <= 0)
                {
                    break;
                }
                x++;
                k++;
                f *= i * j * b;
                u *= x * k;
                // overflow check. not needed if parameters are limited:
                // if (u > 1.E100) {u *= 1E-100;  f *= 1E-100;}
                i--;
                j--;
            } while (x < n);
            return x;
        }

        static int ExtendedHypergeometricRatioOfUnifoms_xh_n_last = -1, ExtendedHypergeometricRatioOfUnifoms_xh_m_last = -1, ExtendedHypergeometricRatioOfUnifoms_xh_t_last = -1; // previous parameters
        static double ExtendedHypergeometricRatioOfUnifoms_xh_b_last = -1;
        static int nh_bound; // upper bound
        static double nh_a; // hat center
        static double nh_h; // hat width
        static double nh_lfm; // ln(f(mode))
        static double nh_logb; // ln(b)
        public int ExtendedHypergeometricRatioOfUnifoms(int n, int m, int t, double b)
        {
            /* 
              Subfunction for ExtendedHypergeometric distribution. 
              Valid for 0 <= n <= m <= t/2, b != 1
              Extended hypergeometric distribution by ratio-of-uniforms rejection method.
              The execution time of this function is almost independent of the parameters.
            */

            int L; // t-m-n
            int mode; // mode
            double mean; // mean
            double variance; // variance
            double x; // real sample
            int k; // integer sample
            double u; // uniform random
            double lf; // ln(f(x))
            double AA; // temporary
            double BB;
            double g1;
            double g2;

            L = t - m - n;
            if (n != ExtendedHypergeometricRatioOfUnifoms_xh_n_last || m != xh_m_last || t != xh_t_last || b != ExtendedHypergeometricRatioOfUnifoms_xh_b_last)
            {
                // set-up
                ExtendedHypergeometricRatioOfUnifoms_xh_n_last = n;
                xh_m_last = m;
                xh_t_last = t;
                ExtendedHypergeometricRatioOfUnifoms_xh_b_last = b;
                // find approximate mean
                AA = (m + n) * b + L;
                BB = Math.Sqrt(AA * AA - 4 * b * (b - 1) * m * n);
                mean = (AA - BB) / (2 * (b - 1));
                // find approximate variance
                AA = mean * (m - mean);
                BB = (n - mean) * (mean + L);
                variance = t * AA * BB / ((t - 1) * (m * BB + (n + L) * AA));
                // find center and width of hat function
                nh_a = mean + 0.5;
                nh_h = Math.Sqrt(SHAT1 * (variance + 0.5)) + 2.0 * 0.6425;
                // find safety bound           
                nh_bound = (int)(mean + 4.0 * nh_h);
                if (nh_bound > n)
                {
                    nh_bound = n;
                }
                // find mode
                mode = (int)(mean);
                g1 = (double)(m - mode) * (n - mode) * b;
                g2 = (double)(mode + 1) * (L + mode + 1);
                if (g1 > g2 && mode < n)
                {
                    mode++;
                }
                // compute log(b)
                nh_logb = Math.Log(b);
                // value at mode to scale with:
                nh_lfm = mode * nh_logb - fc_lnpk(mode, L, m, n);
            }
            while (true)
            {
                u = Random();
                if (u == 0)
                {
                    continue; // avoid divide by 0
                }
                x = nh_a + nh_h * (Random() - 0.5) / u;
                if (x < 0)
                {
                    continue; // reject
                }
                k = (int)(x); // truncate
                if (k > nh_bound)
                {
                    continue; // reject if outside safety bound
                }
                lf = k * nh_logb - fc_lnpk(k, L, m, n) - nh_lfm; // compute function value

                if ((u * (4.0 - u) - 3.0 <= lf) != false)
                {
                    break; // lower squeeze accept
                }
                if ((u * (u - lf) > 1.0) != false)
                {
                    continue; // upper squeeze reject
                }
                if ((2.0 * Math.Log(u) <= lf) != false)
                {
                    break;
                }
            } // final acceptance
            return k;
        }

        /***********************************************************************

                  Subfunctions used by several functions

        ***********************************************************************/
        const int FAK_LEN = 1024;
        public double fc_lnpk(int k, int L, int m, int n)
        {

            // subfunction used by hypergeometric and extended hypergeometric distribution

            return (LnFac(k) + LnFac(m - k) + LnFac(n - k) + LnFac(L + k));
        }
        public static double[] fac_table = new double[FAK_LEN];// table of ln(n!):
        public static double C0 = 0.918938533204672722;
        public static double C1 = 1.0 / 12.0;
        public static double C3 = -1.0 / 360.0;
        public double LnFac(int n)
        {
            // log factorial function. gives ln(n!)
            if (n <= 1)
            {
                if (n < 0)
                {
                    FatalError("Parameter negative in LnFac function");
                }
                return 0;
            }
            if (n < FAK_LEN)
            {
                return fac_table[n];
            }
            // not found in table. use Stirling approximation
            // C5 =  1./1260.,  // use r^5 term if FAK_LEN < 50
            // C7 = -1./1680.;  // use r^7 term if FAK_LEN < 20
            double n1;
            double r;
            n1 = n;
            r = 1.0 / n1;
            return (n1 + 0.5) * Math.Log(n1) - n1 + C0 + r * (C1 + r * r * C3);
        }

        /***********************************************************************

                      Constructor

        ***********************************************************************/
        public StochasticLib(int seed)
            :base(seed)
        {
            fac_table[0] = 0;
            fac_table[1] = 0;
            fac_table[2] = 0;
            if (fac_table[2] == 0)
            {
                // make table of ln(n!)
                double sum = 0;
                fac_table[0] = 0;
                for (int i = 1; i < FAK_LEN; i++)
                {
                    sum += Math.Log((double)i);
                    fac_table[i] = sum;
                }
            }
        }

        public double Exponential(double beta)
        {
            double t;
            t = -Math.Log(Random()) * beta;
            return t;
        }
    }
}