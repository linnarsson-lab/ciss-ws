using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public class Sort
	{
		public static void Permute<T>(IList<T> list, int[] index)
		{
			int[] positions = (int[])index.Clone();
			for(int ix = 0; ix < positions.Length; ix++)
			{
				int curPos = ix;
				T curItem = list[curPos];
                int toPos = positions[curPos];
				while (toPos != curPos)
				{
					T nextItem = list[toPos];
                    int nextPos = positions[toPos];
                    list[toPos] = curItem;
                    positions[toPos] = toPos;
                    curPos = toPos;
                    curItem = nextItem;
                    toPos = nextPos;
				}
			}
		}

		public static void PermuteRandom<T>(IList<T> list)
		{
			int[] positions = new int[list.Count];
			for(int ix = 0; ix < list.Count; ix++)
			{
				positions[ix] = ix;
			}
			for(int ix = 0; ix < list.Count; ix++)
			{
				int rnd = (int)(MersenneTwister.Instance.NextUInt32() % positions.Length);
				swap<int>(positions, ix, rnd);
			}
			Permute(list, positions);
		}

        public static void QuickSort<T, U, V, W>(IList<T> array, IList<U> other1, IList<V> other2, IList<W> other3) where T : IComparable<T>
        {
            if (array.Count != other1.Count) throw new ArgumentException("Lists must be of equal length!");
            if (array.Count != other2.Count) throw new ArgumentException("Lists must be of equal length!");
            if (array.Count != other3.Count) throw new ArgumentException("Lists must be of equal length!");
            QuickSort<T, U, V, W>(array, other1, other2, other3, 0, array.Count);
        }
        public static void QuickSort<T, U, V, W>(IList<T> array, IList<U> other1, IList<V> other2, IList<W> other3,
                                                int start, int end) where T : IComparable<T>
        {
            if (end > start + 1)
            {
                T piv = array[start];
                int left = start + 1, right = end;
                while (left < right)
                {
                    if (array[left].CompareTo(piv) <= 0) left++;
                    else
                    {
                        right--;
                        swap<T>(array, left, right);
                        swap<U>(other1, left, right);
                        swap<V>(other2, left, right);
                        swap<W>(other3, left, right);
                    }
                }
                left--;
                swap<T>(array, left, start);
                swap<U>(other1, left, start);
                swap<V>(other2, left, start);
                swap<W>(other3, left, start);

                QuickSort<T, U, V, W>(array, other1, other2, other3, start, left);
                QuickSort<T, U, V, W>(array, other1, other2, other3, right, end);
            }
        }

		public static void QuickSort<T, U, V>(IList<T> array, IList<U> other1, IList<V> other2) where T : IComparable<T>
		{
            if (array.Count != other1.Count) throw new ArgumentException("Lists must be of equal length!");
            if (array.Count != other2.Count) throw new ArgumentException("Lists must be of equal length!");
            QuickSort<T, U, V>(array, other1, other2, 0, array.Count);
		}
		public static void QuickSort<T, U, V>(IList<T> array, IList<U> other1, IList<V> other2, int start, int end) where T : IComparable<T>
		{
			if(end > start + 1)
			{
				T piv = array[start];
				int left = start + 1, right = end;
				while(left < right)
				{
					if(array[left].CompareTo(piv) <= 0) left++;
					else
					{
						right--;
						swap<T>(array, left, right);
						swap<U>(other1, left, right);
						swap<V>(other2, left, right);
					}
				}
				left--;
				swap<T>(array, left, start);
				swap<U>(other1, left, start);
				swap<V>(other2, left, start);

				QuickSort<T,U,V>(array, other1, other2, start, left);
				QuickSort<T, U, V>(array, other1, other2, right, end);
			}
		}
		public static void QuickSort<T, U>(IList<T> array, IList<U> other) where T : IComparable<T>
		{
            if (array.Count != other.Count) throw new ArgumentException("Lists must be of equal length!");
			QuickSort<T, U>(array, other, 0, array.Count);
		}
		public static void QuickSort<T, U>(IList<T> array, IList<U> other, int start, int end) where T : IComparable<T>
		{
			if(end > start + 1)
			{
				T piv = array[start];
				int left = start + 1, right = end;
				while(left < right)
				{
					if(array[left].CompareTo(piv) <= 0) left++;
					else
					{
						right--;
						swap<T>(array, left, right);
						swap<U>(other, left, right);
					}
				}
				left--;
				swap<T>(array, left, start);
				swap<U>(other, left, start);

				QuickSort<T,U>(array, other, start, left);
				QuickSort<T,U>(array, other, right, end);
			}

		}



		public static void QuickSort<T>(IList<T> array) where T : IComparable<T>
		{
			QuickSort<T>(array, 0, array.Count);
		}
		public static void QuickSort<T>(IList<T> array, int start, int end) where T: IComparable<T>
		{
			if(end > start + 1)
			{
				T piv = array[start];
				int left = start + 1, right = end;
				while(left < right)
				{
					if(array[left].CompareTo(piv) <= 0) left++;
					else
					{
						right--;
						swap<T>(array, left, right);
					}
				}
				left--;
				swap<T>(array, left, start);

				QuickSort<T>(array, start, left);
				QuickSort<T>(array, right, end);
			}
		}

		private static void swap<T>(IList<T> list, int from, int to)
		{
			T temp = list[from];
			list[from] = list[to];
			list[to] = temp;
		}

		public static void HeapSort<T, U, V>(IList<T> a, IList<U> other1, IList<V> other2) where T: IComparable<T>
		{
            if (a.Count == 0) return;
            if (a.Count != other1.Count) throw new ArgumentException("Lists must be of equal length!");
            if (a.Count != other2.Count) throw new ArgumentException("Lists must be of equal length!");
            int N = a.Count - 1, i;
			for (i = N / 2; i >= 0; i--) downHeap<T,U,V>(a, other1, other2, i, N);
			/* a[0..N] is now a heap */

			for (i = N; i > 0; i--)
			{
				swap<T>(a, 0, i);
				swap<U>(other1, 0, i);
				swap<V>(other2, 0, i);
				downHeap<T,U,V>(a, other1, other2, 0, i - 1); /* restore a[0..i-1] heap */
			}
		}

		private static void downHeap<T, U, V>(IList<T> a, IList<U> other1, IList<V> other2, int k, int N) where T : IComparable<T>
		/*  PRE: a[k+1..N] is a heap */
		/* POST:  a[k..N]  is a heap */
		{
			int child;
			T newElt = a[k];
			U newEltOther1 = other1[k];
			V newEltOther2 = other2[k];
			while (k <= N / 2)   /* k has child(s) */
			{
				child = 2 * k;
				/* pick larger child */
				if (child < N && a[child].CompareTo(a[child + 1]) < 0) child++;
				if (newElt.CompareTo(a[child]) >= 0) break;
				/* else */
				a[k] = a[child]; /* move child up */
				other1[k] = other1[child];
				other2[k] = other2[child];
				k = child;
			}
			a[k] = newElt;
			other1[k] = newEltOther1;
			other2[k] = newEltOther2;
		}

		public static void HeapSort<T, U>(IList<T> a, IList<U> other1) where T : IComparable<T>
		{
            if (a.Count == 0) return;
            if (a.Count != other1.Count) throw new ArgumentException("Lists must be of equal length!");
			int N = a.Count - 1, i;
			for (i = N / 2; i >= 0; i--) downHeap<T, U>(a, other1, i, N);
			/* a[0..N] is now a heap */

			for (i = N; i > 0; i--)
			{
				swap<T>(a, 0, i);
				swap<U>(other1, 0, i);
				downHeap<T, U>(a, other1, 0, i - 1); /* restore a[0..i-1] heap */
			}
		}

		private static void downHeap<T, U>(IList<T> a, IList<U> other1, int k, int N) where T : IComparable<T>
		/*  PRE: a[k+1..N] is a heap */
		/* POST:  a[k..N]  is a heap */
		{
			int child;
			T newElt = a[k];
			U newEltOther1 = other1[k];
			while (k <= N / 2)   /* k has children */
			{
				child = 2 * k;
				/* pick larger child */
				if (child < N && a[child].CompareTo(a[child + 1]) < 0) child++;
				if (newElt.CompareTo(a[child]) >= 0) break;
				/* else */
				a[k] = a[child]; /* move child up */
				other1[k] = other1[child];
				k = child;
			}
			a[k] = newElt;
			other1[k] = newEltOther1;
		}
		public static void HeapSort<T>(IList<T> a) where T : IComparable<T>
		{
            if (a.Count == 0) return;
            int N = a.Count - 1, i;
			for (i = N / 2; i >= 0; i--) downHeap<T>(a, i, N);
			/* a[0..N] is now a heap */

			for (i = N; i > 0; i--)
			{
				swap<T>(a, 0, i);
				downHeap<T>(a, 0, i - 1); /* restore a[0..i-1] heap */
			}
		}

		private static void downHeap<T>(IList<T> a, int k, int N) where T : IComparable<T>
		/*  PRE: a[k+1..N] is a heap */
		/* POST:  a[k..N]  is a heap */
		{
			int child;
			T newElt = a[k];
			while (k <= N / 2)   /* k has children */
			{
				child = 2 * k;
				/* pick larger child */
				if (child < N && a[child].CompareTo(a[child + 1]) < 0) child++;
				if (newElt.CompareTo(a[child]) >= 0) break;
				/* else */
				a[k] = a[child]; /* move child up */
				k = child;
			}
			a[k] = newElt;
		}
	}
}
