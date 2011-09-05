using System;
using System.Collections.Generic;

namespace Linnarsson.Mathematics
{

    [Serializable()]
    public struct HeapItem<P, T> where P: IComparable<P>
	{
		private P m_Priority;
		public P Priority
		{
			get { return m_Priority; }
		}

        private T m_Item;
		public T Item
		{
			get { return m_Item; }
		}
		

        public HeapItem(T item, P priority) {
            this.m_Item = item;
            this.m_Priority = priority;
        }

        public void Clear() {
            m_Item = default(T);
            m_Priority = default(P);
        }
    }

    public class PriorityQueue<P,T> where P: IComparable<P> {
		public int Count
		{
			get { return heap.Count; }
		}

		private int Capacity
		{
			get { return heap.Capacity; }
		}
        private List<HeapItem<P,T>> heap;

		public PriorityQueue() {
            heap = new List<HeapItem<P,T>>();
        }

		public T Peek()
		{
			if(Count == 0) throw new InvalidOperationException();

			return heap[0].Item;
		}

        public T Dequeue() {
            if (Count == 0) throw new InvalidOperationException();
            
            T result = heap[0].Item;
			trickleDown(0, heap[Count-1]);
			heap.RemoveAt(Count - 1);
            return result;
        }

        public void Enqueue(P priority, T item) {
			heap.Add(new HeapItem<P, T>());
			bubbleUp(Count - 1, new HeapItem<P,T>(item, priority));
        }

        private void bubbleUp(int index, HeapItem<P,T> item) {
            int parent = getParent(index);
            while ((index > 0) && (heap[parent].Priority.CompareTo(item.Priority) < 0)) 
			{
                heap[index] = heap[parent];
                index = parent;
                parent = getParent(index);
            }
            heap[index] = item;
        }

        private int getLeftChild(int index) {
            return (index * 2) + 1;
        }

        private int getParent(int index) {
            return (index - 1) / 2;
        }

        private void trickleDown(int index, HeapItem<P,T> item) {
            int child = getLeftChild(index);
            while (child < Count) 
			{
                if (((child + 1) < Count) && (heap[child].Priority.CompareTo(heap[child + 1].Priority) < 0)) 
				{
                    child++;
                }
                heap[index] = heap[child];
                index = child;
                child = getLeftChild(index);
            }
            bubbleUp(index, item);
        }
    }
}
