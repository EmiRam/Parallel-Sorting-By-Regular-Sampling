/*
 File Name: Program.cs
 Author: Emily Ramanna

 Description:
 This is an attempt at implementing a Parallel Sorting by Regular Sampling (PSRS) algorithm.
 Within the larger PSRS algorithm, sequential quicksorts and mergesorts are used.
 Please see the readme for a higher level overview of the PSRS algorithm.

 Please note: I use the term "core" in this code's comments to better match the 
 original intention of the PSRS algorithm and for simplicity. In reality, .NET's
 runtime manages the threads in this implementation, so it's unlikely that the
 number of threads and physical cores used matches exactly with what the code would have 
 you believe.

 I used 2 sources to piece together this implemention:
 1. "Parallel Sorting by Regular Sampling" by Hanmao Shi and Jonathan Schaeffer
    Retrieved from: https://webdocs.cs.ualberta.ca/~jonathan/publications/parrallel_computing_publications/psrs1.pdf
 2. Github project "Parallel-sort-by-regular-sampling" by Shane Fitzpatrick
    Retrieved from: https://github.com/Fitzpasd/Parallel-sort-by-regular-sampling/blob/master/psrs_sort.c
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace FinalProject
{
    class Program
    {
        // Number of cores the sort intends to use
        public const int NUM_CORES = 8;

        // Sentinel value that is a substitute for 'infinity', when it comes to names,
        // there won't be a value later than this. Of course, this is not bullet-proof
        // and there are implementations of merge sort that don't require a sentinel, but
        // this saves some computation. Whether or not this is suitable depends on how the
        // input for the names is processed/validated. For example, if a character limit for each name
        // were 30, then putting 31 Zs for each name as a sentinel value would work just fine.
        public const string MERGE_SORT_SENTINEL = "ZZZZZZZZZ ZZZZZZZZZ";

        // START OF EXECUTION
        static void Main(string[] args)
        {

            // Read in the file
            string[] names = File.ReadAllLines("ListOfNames.txt");

            // Time sort and print sorting time in milliseconds
            Stopwatch stopwatch = Stopwatch.StartNew();
            ParallelSortingByRegularSampling(names, names.Length, NUM_CORES);
            stopwatch.Stop();
            Console.WriteLine("Code took {0} milliseconds to execute", stopwatch.ElapsedMilliseconds);

            // Outputs sorted names to file. Makes it easier to check that sort is correct.
            using (StreamWriter sw = new StreamWriter("SortedListOfNames.txt"))
            {
                foreach (var name in names)
                {
                    sw.WriteLine(name);
                }
            }

            Console.WriteLine("Press Return to exit");
            Console.ReadLine();
        }

        // PSRS algorithm. n is the length of the list to be sorted and p is the number of cores that should be used.
        // Has 3 main phases
        static void ParallelSortingByRegularSampling(string[] list, int n, int p)
        {
            int size = (n + p - 1) / p; // size of list each core will sort using sequential quicksort
            int rsize = (size + p - 1) / p;
            int sample_size = p * (p - 1); // How many samples will be taken

            string[][] arraySections = new string[p][]; // stores the 'local' sections of the list for all cores
            string[] sample = new string[sample_size];
            int[] sublists = new int[p * (p + 1)];
            int[] bucketSizes = new int[p];
            int[] resultPositions = new int[p];
            string[] pivots = new string[p - 1];


            // Phase 1: Each core sorts a (n/p) contiguous list using sequential quicksort
            Parallel.For(0, p, i =>
            {
                int start = i * size;
                int end = start + size - 1;
                if (end >= n)
                {
                    end = n - 1;
                }
                int arraySectionSize = (end - start + 1);
                end = end % size;

                // stores this core's portion of the overall list
                string[] arraySection = new string[arraySectionSize];
                Array.Copy(list, start, arraySection, 0, arraySectionSize);
                arraySections[i] = arraySection;

                // each core performs sequential quicksort on its portion
                Quicksort(arraySection, 0, arraySection.Length - 1);

                int offset = i * (p - 1) - 1;

                // Take representative samples from the core's section
                for (int j = 1; j < p; j++)
                {
                    if ((j * rsize) <= end)
                    {
                        sample[offset + j] = arraySection[j * rsize - 1];
                    }
                    else
                    {
                        sample[offset + j] = arraySection[end];
                    }
                }

            });

            // Synchronize

            // Phase 2: Sort samples taken (this is sequential)
            // and pivots are chosen and indices are calculated for the third phase (done in parallel)
            Quicksort(sample, 0, sample_size - 1);
            for (int i = 0; i < p - 1; i++)
            {
                pivots[i] = sample[i * p + p / 2];
            }

            // each core must figure out indices for the pivots in order to partition later
            Parallel.For(0, p, i =>
            {

                int start = i * size;
                int end = start + size - 1;
                if (end >= n)
                {
                    end = n - 1;
                }

                end = end % size;

                int offset = i * (p + 1);
                sublists[offset] = 0;
                sublists[offset + p] = end + 1;

                // this calculates the partition borders according to where the pivots are
                // (a form of binary search)
                Sublists(arraySections[i], 0, arraySections[i].Length - 1, sublists, offset, pivots, 1, p - 1);

            });

            // Synchronize

            // Once partition borders are figured out, sizes of the partitions must be calculated by each core
            Parallel.For(0, p, i =>
            {
                int max = p * (p + 1);
                bucketSizes[i] = 0;
                for (int j = i; j < max; j += (p + 1))
                {
                    bucketSizes[i] = bucketSizes[i] + sublists[j + 1] - sublists[j];
                }
            });

            // Synchronize

            // More index calculations for the final sorting
            resultPositions[0] = 0;
            for (int i = 1; i < p; i++)
            {
                resultPositions[i] = bucketSizes[i - 1] + resultPositions[i - 1];
            }

            // Phase 3: Each core performs a p-way mergesort
            // Because of the careful indexing in phase 2, these merges can be done
            // independently by each core.
            Parallel.For(0, p, i =>
            {
                int result = resultPositions[i];
                int resultSize = 0;
                if (i == p - 1)
                {
                    resultSize = n - resultPositions[i];
                }
                else
                {
                    resultSize = resultPositions[i + 1] - resultPositions[i];
                }
                result = resultPositions[i];

                for (int j = 0, k = 0; j < p; j++)
                {
                    int low = 0;
                    int high = 0;
                    int partitionSize = 0;
                    int offset = j * (p + 1) + i;
                    low = sublists[offset];
                    high = sublists[offset + 1];
                    partitionSize = high - low;


                    if (partitionSize > 0)
                    {
                        // Copies the "local" section of the list back into the original list in the correct place
                        Array.Copy(arraySections[j], low, list, result + k, partitionSize);
                        k += partitionSize;
                    }
                }
                // Perform the mergesort on the original list at indices that won't conflict with 
                // other parallel calls
                MergeSort(list, result, result + resultSize - 1);
            });
        }

        // Calculates the indices for partitioning using the pivots
        // This is essentially a binary search to find where the pivots divide a portion of the list
        static void Sublists(string[] list, int start, int end, int[] subsize, int at, string[] pivots, int fp, int lp)
        {
            int mid = (fp + lp) / 2;
            string pv = pivots[mid - 1];
            int lb = start;
            int ub = end;
            while (lb <= ub)
            {
                int center = (lb + ub) / 2;

                // Gets the indices of the first letter of the last name
                int lastNameIndex1 = list[center].IndexOf(' ') + 1;
                int lastNameIndex2 = pv.IndexOf(' ') + 1;

                // Compares last names
                int compValue = (list[center].Substring(lastNameIndex1)).CompareTo(pv.Substring(lastNameIndex2));

                //If the last names are the same, just compare from the start of the string (first name)
                if (compValue == 0)
                {
                    compValue = list[center].CompareTo(pv);
                }

                if (compValue > 0)
                {
                    ub = center - 1;

                }
                else
                {
                    lb = center + 1;
                }
            }

            subsize[at + mid] = lb;
            if (fp < mid)
            {
                Sublists(list, start, lb - 1, subsize, at, pivots, fp, mid - 1);
            }

            if (mid < lp)
            {
                Sublists(list, lb, end, subsize, at, pivots, mid + 1, lp);
            }
        }

        /* SEQUENTIAL QUICKSORT IMPLEMENTATION */
        // Used the pseudocode in "Introduction to Algorithms" by Cormen, Leiserson, Rivest, and Stein as a guide.
        static void Quicksort(string[] list, int p, int r)
        {
            if (p < r)
            {
                int q = Partition(list, p, r);
                Quicksort(list, p, q - 1);
                Quicksort(list, q + 1, r);
            }
        }

        static int Partition(string[] list, int p, int r)
        {
            string x = list[r];

            // Gets the index of the first letter of one last name
            int lastNameIndex1 = x.IndexOf(' ') + 1;

            int i = p - 1;
            for (int j = p; j < r; j++)
            {
                // Gets the index of the first letter of a second last name
                int lastNameIndex2 = list[j].IndexOf(' ') + 1;

                // Compares last names
                int compValue = (x.Substring(lastNameIndex1)).CompareTo(list[j].Substring(lastNameIndex2));

                //If the last names are the same, compare from the beginning of the string (first names)
                if (compValue == 0)
                {
                    compValue = x.CompareTo(list[j]);
                }

                if (compValue > 0)
                {
                    i = i + 1;
                    string temp = list[i];
                    list[i] = list[j];
                    list[j] = temp;
                }
            }
            string tempPivot = list[i + 1];
            list[i + 1] = list[r];
            list[r] = tempPivot;
            return i + 1;
        }

        /* SEQUENTIAL MERGESORT IMPLEMENTATION */
        // Used the pseudocode in "Introduction to Algorithms" by Cormen, Leiserson, Rivest, and Stein as a guide.

        static void MergeSort(string[] list, int p, int r)
        {
            if (p < r)
            {
                int q = (p + r) / 2;
                MergeSort(list, p, q);
                MergeSort(list, q + 1, r);
                Merge(list, p, q, r);
            }
        }

        static void Merge(string[] list, int p, int q, int r)
        {
            int n1 = q - p + 1;
            int n2 = r - q;

            string[] L = new string[n1 + 1];
            string[] R = new string[n2 + 1];

            for (int i = 0; i < n1; i++)
            {
                L[i] = list[p + i];
            }

            for (int j = 0; j < n2; j++)
            {
                R[j] = list[q + 1 + j];
            }

            //Sentinels ('infinity) to shortcut some computation
            L[n1] = MERGE_SORT_SENTINEL;
            R[n2] = MERGE_SORT_SENTINEL;
            int leftIndex = 0;
            int rightIndex = 0;


            bool hasLeftChanged = true;
            bool hasRightChanged = true;
            int lastNameIndex1 = 0;
            int lastNameIndex2 = 0;

            for (int k = p; k < r + 1; k++)
            {
                if (hasLeftChanged)
                {
                    lastNameIndex1 = L[leftIndex].IndexOf(' ') + 1;
                }

                if (hasRightChanged)
                {
                    lastNameIndex2 = R[rightIndex].IndexOf(' ') + 1;
                }

                // Compares last names
                int compValue = (L[leftIndex].Substring(lastNameIndex1)).CompareTo(R[rightIndex].Substring(lastNameIndex2));

                //If the last names are the same, compare from the beginning of the string (first names)
                if (compValue == 0)
                {
                    compValue = L[leftIndex].CompareTo(R[rightIndex]);
                }

                if (compValue > 0)
                {
                    list[k] = R[rightIndex];
                    rightIndex = rightIndex + 1;
                    hasLeftChanged = false;
                    hasRightChanged = true;
                }
                else
                {
                    list[k] = L[leftIndex];
                    leftIndex = leftIndex + 1;
                    hasLeftChanged = true;
                    hasRightChanged = false;

                }
            }
        }
    }
}
