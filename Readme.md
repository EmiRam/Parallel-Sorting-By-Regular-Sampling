# Overview

* The C# .NET code is contained in "ParallelSortingByRegularSampling/Program.cs"
* Takes in a list of names in a file "ListOfNames.txt" (not included in this repo) of the form 'Firstname Lastname' with each full name on a separate line
* Saves sorted list to file "SortedListOfNames.txt"
* Sorts by last names. If last names are the the same, then sorts by first names.
* Does the sorting using an implementation of the Parallel Sorting by Regular Sampling (PSRS) algorithm.

A research paper (Schaeffer & Shi, 1993) was used for the outline of the code and an example done in the C programming language (Fitzpatrick, 2012) was referenced for some of the finer implementation details.

# PSRS Description
A major issue with parallel sorting is figuring out how to split or partition the data so that load balancing is achieved between cores. PSRS tries to achieve this by taking a regular sample of the data and then partitioning the data in such a way that reduces the amount of access contention and data movement.

### Phase 1
Split the data evenly between the cores. In my implementation, a copy of the data is made so that there is a number of smaller lists (that number being the amount of cores we wish to run this algorithm with). In parallel, each processor sorts its own section of the data using sequential quicksort, yielding locally sorted data. From each section of the data, collect a regular sample.

### Phase 2
Have all the processors synchronize and then use sequential quicksort to sort all of the regular samples taken. From the sorted regular sample, select a few values as pivots. In parallel, each processor must figure out the indices that the values of the pivots would belong in its portion of the data. For example, if the pivots were 4 and 10, and a processor's data was 1,5,8,13, then it would be found that 4 belongs between 1 and 5, and 10 belongs between 8 and 13. These demarcations are critical to the next step. This is done using a binary search. Next, the data is reassigned to other processors through indexing. In the previous example, all of the data from each of the processors that is before the pivot value 4 would be sent to one processor. All of the data from each of the processors between 4 and 10 would be sent to another processor, and so on.

### Phase 3
In parallel, each processor takes its new data and performs a sequential mergesort. When the portions are put back together the entire array is fully sorted. In my implementation, the data portions are copied back to the original array and are then sorted using indices to tell the mergesort algorithm where to start and stop. These sorts can be done independently and in parallel because of the careful indexing and assignment of data based on how the values related to the chosen pivots.

Fitzpatrick, S. (2012). Parallel-sort-by-regular-sampling/psrs_sort.c. Retrieved November 29, 2021, from https://github.com/Fitzpasd/Parallel-sort-by-regularsampling/blob/master/psrs_sort.c

Schaeffer, J., & Shi, H. (1993). Parallel Sorting by Regular Sampling. Retrieved November 28, 2021, from https://webdocs.cs.ualberta.ca/~jonathan/publications/parrallel_computing_publications/psrs1.pdf
