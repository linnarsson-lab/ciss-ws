using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Mathematics.Automata
{
	public class JanusList<T>
	{
		private List<T> storage = new List<T>();
		private int offset = 0;
		public int Min { get { return offset; } }
		public int Max { get { return storage.Count + offset; } }

		public void Add(T item)
		{
			storage.Add(item);
		}

		public void RemoveLast()
		{
			storage.RemoveAt(storage.Count - 1);
		}

		public JanusList<T> Copy()
		{
			JanusList<T> copy = new JanusList<T>();
			copy.storage = new List<T>(storage);
			copy.offset = offset;
			return copy;
		}

		public void Prepend(T item)
		{
			storage.Insert(0, item);
			offset--;
		}

		public T this[int index]
		{
			get
			{
				return storage[index - offset];
			}
			set
			{
				storage[index - offset] = value;
			}
		}
	}

	class State
	{
		public HashSet<int>[] Transitions { get; set; }
		public bool IsAccept { get; set; }

		public State(bool accept)
		{
			IsAccept = accept;
			Transitions = new HashSet<int>[129];
		}

		public State GetTranslatedCopy(int offset)
		{
			State temp = new State(IsAccept);
			for (int c = 0; c < 129; c++)
			{
				if(Transitions[c] == null) continue;
				temp.Transitions[c] = new HashSet<int>();
				foreach(int to in Transitions[c]) temp.Transitions[c].Add(to + offset);
			}
			return temp;
		}
	}

	/// <summary>
	/// A table-based finite state automaton that operates on the 7-bit ASCII characters
	/// </summary>
	public class Nfa
	{
		public const char EPSILON = (char)128;

		// A list of states, each having an array indexed by characters, each entry being a set of successor states
		// Characters are in range 0 - 128, with 128 being the epsilon character
		private JanusList<State> States;

		public bool IsDeterministic
		{
			get
			{
				for(int i = States.Min; i < States.Max; i++)
				{
					for(int j = 0; j < 129; j++)
					{
						if(States[i].Transitions[j] == null) continue;
						if(States[i].Transitions[j].Count > 1) return false;
					}
					if(States[i].Transitions[128] != null) return false; // disallow epsilons
				}
				return true;
			}
		}

		public bool IsEpsilonFree
		{
			get
			{
				for(int i = States.Min; i < States.Max; i++)
				{
					if(States[i].Transitions[EPSILON] == null) continue;
					if(States[i].Transitions[EPSILON].Count > 0) return false;
				}
				return true;
			}
		}

		public Nfa()
		{
			States = new JanusList<State>();
		}

		public Nfa Copy()
		{
			Nfa copy = new Nfa();
			copy.States = States.Copy();
			return copy;
		}

		/// <summary>
		/// Create a FSA that recognizes a single character from the given set (or if negated,
		/// recognizes any character not in the set
		/// </summary>
		/// <param name="set"></param>
		/// <param name="negated"></param>
		public static Nfa CharacterClass(HashSet<char> set, bool negated)
		{
			Nfa fsa = new Nfa();
			fsa.AppendState(false);
			fsa.AppendState(true);
			for(int i = 0; i < 128; i++)
			{
				if(!negated)
				{
					if(set.Contains((char)i)) fsa.AddTransition(0, 1, (char)i);
				}
				else if(!set.Contains((char)i)) fsa.AddTransition(0, 1, (char)i);
			}
			return fsa;
		}

		public static Nfa AnyCharacter()
		{
			Nfa fsa = new Nfa();
			fsa.AppendState(false);
			fsa.AppendState(true);
			for(int i = 0; i < 128; i++)
			{
				fsa.AddTransition(0, 1, (char)i);
			}
			return fsa;
		}

		public static Nfa SingleCharacter(char c)
		{
			Nfa fsa = new Nfa();
			fsa.AppendState(false);
			fsa.AppendState(true);
			fsa.AddTransition(0, 1, c);
			return fsa;
		}

		private void AppendState(bool accept)
		{
			States.Add(new State(accept));
		}

		private void PrependState(bool accept)
		{
			States.Prepend(new State(accept));
		}		
		
		private void AddTransition(int from, int to, char c)
		{
			if(from < States.Min || from >= States.Max) throw new InvalidProgramException("Cannot add transition; 'from' state does not exist");
			if(to < States.Min || to >= States.Max) throw new InvalidProgramException("Cannot add transition; 'to' state does not exist");
			int x = (int)c;
			if(x < 0 || x > EPSILON) throw new InvalidProgramException("Cannot add transition; character is not in 7-bit ASCII range");

			HashSet<int> temp = States[from].Transitions[x];
			if(temp == null)
			{
				temp = new HashSet<int>();
				States[from].Transitions[x] = temp;
			}
			temp.Add(to);
		}

		/// <summary>
		/// Appends another automaton, translating transitions as necessary, but does not
		/// change any transitions. 
		/// </summary>
		/// <param name="other"></param>
		public void AddAutomaton(Nfa other)
		{
			int otherFirst = States.Max;
			for(int ix = other.States.Min; ix < other.States.Max; ix++)
			{
				States.Add(other.States[ix].GetTranslatedCopy(otherFirst - other.States.Min));
			}
		}

		/// <summary>
		/// Augment the automaton so that it represents the concatenation with the other automaton. Assumes that the FSA is in NFA form
		/// with single start and accept states.
		/// </summary>
		/// <param name="other"></param>
		public void Concatenate(Nfa other)
		{
			// Remove the last state
			int last = States.Max - 1;
			HashSet<int>[] temp = States[last].Transitions;			
			States.RemoveLast();

			// Add states from other automaton
			AddAutomaton(other);

			// Add back transitions from last state
			for (int c = 0; c < 129; c++)
			{
				if(temp[c] == null) continue;
			 	foreach(int to in temp[c])
				{
					AddTransition(last, to, (char)c);
				}
			}
		}
		/// <summary>
		/// Augment the automaton so that it represents the alternation with the other automaton. Assumes that the FSA is in NFA form
		/// with single start and accept states.
		/// </summary>
		/// <param name="other"></param>
		public void Alternate(Nfa other)
		{
			int last = States.Max - 1;

			// Append the other automaton
			AddAutomaton(other);

			// Make the current accept states internal
			States[last].IsAccept = false;
			States[States.Max - 1].IsAccept = false;

			// Add new start and accept states
			PrependState(false);
			AppendState(true);

			// Add epsilon transitions to hook up the new start and accept states
			AddTransition(States.Min, States.Min + 1, EPSILON); // start to old start
			AddTransition(States.Min, last + 1, EPSILON); // start to old other start
			AddTransition(last, States.Max - 1, EPSILON); // old accept to accept
			AddTransition(States.Max - 2, States.Max - 1, EPSILON); // old other accept to accept
		}

		/// <summary>
		/// Augment the automaton with the Kleene closure. Assumes the FSA is in NFA form with single start and accept states.
		/// </summary>
		/// <param name="other"></param>
		public void KleeneClosure()
		{
			PrependState(false);
			States[States.Max - 1].IsAccept = false;
			AppendState(true);
			AddTransition(States.Min, States.Min + 1, EPSILON);
			AddTransition(States.Min, States.Max - 1, EPSILON);
			AddTransition(States.Max - 2, States.Max - 1, EPSILON);
			AddTransition(States.Max - 2, States.Min + 1, EPSILON);
		}

		public void KleenePlus()
		{
			PrependState(false);
			States[States.Max - 1].IsAccept = false;
			AppendState(true);
			AddTransition(States.Min, States.Min + 1, EPSILON);
			AddTransition(States.Max - 2, States.Max - 1, EPSILON);
			AddTransition(States.Max - 2, States.Min + 1, EPSILON);
		}

		public void Optional()
		{
			PrependState(false);
			States[States.Max - 1].IsAccept = false;
			AppendState(true);
			AddTransition(States.Min, States.Min + 1, EPSILON);
			AddTransition(States.Max - 2, States.Max - 1, EPSILON);
			AddTransition(States.Min, States.Max - 1, EPSILON);
		}

		private HashSet<int>[] cachedEpsilons;
		private void SetupEpsilonCache()
		{
			cachedEpsilons = new HashSet<int>[States.Max - States.Min];
			CompleteEpsilonClosure();
		}
		private HashSet<int> EpsilonClosure(int initialState)
		{
			return cachedEpsilons[initialState - States.Min];
		}

		private void CompleteEpsilonClosure()
		{
			// Uses Warshall's transitive closure algorithm as described in Sedgewicks's "Algorithms in C++"
			// TODO: get rid of the matrix and do this in place (if possible)
			int cnt = States.Max - States.Min;
			bool[,] closure = new bool[cnt, cnt];
			for(int i = 0; i < cnt; i++)
			{
				for(int s = 0; s < cnt; s++)
				{
					if(States[i + States.Min].Transitions[EPSILON] != null && States[i + States.Min].Transitions[EPSILON].Contains(s + States.Min)) closure[i, s] = true;
				}
			}

			for(int i = 0; i < cnt; i++)
			{
				for(int s = 0; s < cnt; s++)
				{
					for(int t = 0; t < cnt; t++)
					{
						if(closure[s, i] && closure[i, t]) closure[s, t] = true;
					}
				}
			}

			for(int i = 0; i < cnt; i++)
			{
				HashSet<int> result = new HashSet<int>();
				result.Add(i + States.Min); // epsilon closure includes self
				for(int s = 0; s < cnt; s++)
				{
					if(closure[i, s]) result.Add(s + States.Min);
				}
				cachedEpsilons[i] = result;
			}
		}


		public Dfa Determinize()
		{
			Dfa dfa = new Dfa();
			if(IsDeterministic)
			{
				// Just take the states as they are and make the DFA
				for(int i = States.Min; i < States.Max; i++)
				{
					HashSet<int>[] tr = States[i].Transitions;
					int[] transitions = new int[128];

					for(int c = 0; c < 128; c++)
					{
						if(tr[c] == null) transitions[c] = -1;
						else transitions[c] = tr[c].First<int>();
					}
					dfa.AddState(transitions, States[i].IsAccept);
				}
				dfa.AddDeadState();
				return dfa;
			}

			// Ok so we have to go the difficult route through epsilon closure
			SetupEpsilonCache();

			List<HashSet<int>> dfaStates = new List<HashSet<int>>();
			dfaStates.Add(EpsilonClosure(States.Min)); // the initial state
			int nextTodo = 0;
			
			while(nextTodo < dfaStates.Count)
			{
				HashSet<int> nextSubset = dfaStates[nextTodo++];
				int[] transitions = new int[128];

				for(int c = 0; c < 128; c++)
				{
					// Calculate the epsilon-closure for this input character
					HashSet<int> successor = new HashSet<int>();
					foreach(int st in nextSubset)
					{
						if(States[st].Transitions[c] == null) continue;
						foreach(int to in States[st].Transitions[c]) foreach(int ec in EpsilonClosure(to)) successor.Add(ec);
					}

					// Check for the empty set
					if(successor.Count == 0)
					{
						transitions[c] = -1;
						continue;
					}

					// Find out if the subset has already been seen
					bool seen = false;
					for (int j = 0; j < dfaStates.Count; j++)
					{
						if(dfaStates[j].SetEquals(successor))
						{
							seen = true;
							transitions[c] = j;
							break;
						}
					}
					if(!seen)
					{
						dfaStates.Add(successor);
						transitions[c] = dfaStates.Count - 1;
					}
				}
				dfa.AddState(transitions, subsetIsAcceptState(nextSubset));
			}

			// Finally, clean up the DFA by adding a dead state and hooking up all leftover transitions to it
			dfa.AddDeadState();

			return dfa;
		}

		private bool subsetIsAcceptState(HashSet<int> subset)
		{
			foreach(int st in subset)
			{
				if(States[st].IsAccept) return true;
			}
			return false;
		}

		public static Nfa EmptyString()
		{
			Nfa fsa = new Nfa();
			fsa.AppendState(true);
			return fsa;
		}
	}

	public class Dfa
	{
		private List<int[]> states = new List<int[]>();
		private List<bool> accept = new List<bool>();
		private int currentState;

		public bool IsAccept
		{
			get
			{
				return accept[currentState];
			}
		}

		public void AddState(int[] transitions, bool accept)
		{
			this.states.Add(transitions);
			this.accept.Add(accept);
		}

		/// <summary>
		/// Adds a dead state with transitions to itself, and connect all -1 transitions to it
		/// </summary>
		internal void AddDeadState()
		{
			int[] dead = new int[128];
			for(int i = 0; i < 128; i++)
			{
				dead[i] = states.Count;
			}
			AddState(dead, false);

			foreach(int[] tr in states)
			{
				for(int c = 0; c < 128; c++)
				{
					if(tr[c] == -1) tr[c] = states.Count - 1;
				}
			}
		}
		public void Restart()
		{
			currentState = 0;
		}

		public void Step(char c)
		{
			int x = (int)c;
			if(x < 0 || x > 127) throw new ArgumentOutOfRangeException("Only 7-bit ASCII is permitted in DFA");
			currentState = states[currentState][(int)c];
		}

		public void EliminateUnreachableStates()
		{
			bool[] reachable = FindReachableStates();
			for(int i = 0; i < states.Count; i++)
			{
				if(!reachable[i]) RemoveState(i);
			}
		}

		private void RemoveState(int state)
		{
			states.RemoveAt(state);
			accept.RemoveAt(state);

			for(int i = 0; i < states.Count; i++)
			{
				for(int c = 0; c < 128; c++)
				{
					if(states[i][c] > state) states[i][c]--;
				}
			}
		}

		private bool[] FindReachableStates()
		{
			bool[] reachable = new bool[states.Count];
			reachable[0] = true;
			findReachable(0, reachable);
			return reachable;
		}

		private void findReachable(int start, bool[] reachable)
		{
			for(int c = 0; c < 128; c++)
			{
				if(!reachable[states[start][c]])
				{
					reachable[states[start][c]] = true;
					findReachable(states[start][c], reachable);
				}
			}
		}

		public List<Pair<int, int>> GetEquivalentStates()
		{
			// Uses the table-filling algorithm to find pairs of equivalent states
			// O(n2) algorithm using lists described in Hopcroft et al. "Automata Theory, Languages and Computation"

			// Initialize a table of lists of predecessor states
			List<Pair<int, int>>[,] predecessors = new List<Pair<int, int>>[states.Count, states.Count];
			for(int i = 0; i < states.Count; i++)
			{
				for(int j = 0; j < states.Count; j++)
				{
					if(i >= j) continue; // we need only the lower left triangle, since it's symmetric
					predecessors[i, j] = new List<Pair<int, int>>();
				}
			}

			// Fill in all the predecessors, and keep track of distinguishable pairs
			Queue<Pair<int, int>> todo = new Queue<Pair<int, int>>();
			bool[,] distinguishable = new bool[states.Count, states.Count];
			for(int i = 0; i < states.Count; i++)
			{
				for(int j = 0; j < states.Count; j++)
				{
					if(i >= j) continue; // we need only the lower left triangle, since it's symmetric

					// Loop over all the symbols
					for(int c = 0; c < 128; c++)
					{
						// Add state i,j to the lists of its successors
						int x = Math.Min(states[i][c], states[j][c]);
						int y = Math.Max(states[i][c], states[j][c]);
						if(x == y) continue; // No need to keep track of self-equivalences
						predecessors[x, y].Add(new Pair<int, int>(i, j));
					}
					if(accept[i] != accept[j])
					{
						todo.Enqueue(new Pair<int, int>(i, j));
					}
				}
			}


			while(todo.Count != 0)
			{
				// Get a distinguishable pair
				Pair<int, int> next = todo.Dequeue();
				distinguishable[next.First, next.Second] = true;
				foreach(Pair<int, int> pred in predecessors[next.First, next.Second])
				{
					if(!distinguishable[pred.First, pred.Second]) todo.Enqueue(pred);
				}
			}

			List<Pair<int, int>> result = new List<Pair<int, int>>();
			for(int i = 0; i < states.Count; i++)
			{
				for(int j = 0; j < states.Count; j++)
				{
					if(i >= j) continue; // we need only the lower left triangle, since it's symmetric
					if(!distinguishable[i, j]) result.Add(new Pair<int, int>(i, j));
				}
			}
			return result;
		}

		public Dfa Copy()
		{
			Dfa copy = new Dfa();
			copy.accept = new List<bool>(accept);
			copy.currentState = currentState;
			foreach(int[] state in states)
			{
				int[] tr = new int[128];
				state.CopyTo(tr, 0);
				copy.states.Add(tr);
			}
			return copy;
		}

		public bool IsEquivalentTo(Dfa other)
		{
			// First, make a new DFA that combines the two (side-by-side)
			Dfa combined = this.Copy();
			int offset = combined.states.Count;

			for(int i = 0; i < other.states.Count; i++)
			{
				combined.accept.Add(other.accept[i]);
				int[] tr = new int[128];
				for(int c = 0; c < 128; c++)
				{
					tr[c] = other.states[i][c] + offset;
				}
				combined.states.Add(tr);
			}

			// Then calculate equivalence pairs
			List<Pair<int, int>> eqs = combined.GetEquivalentStates();

			// Then see if the start state of this DFA is equal to the start state of other
			foreach(Pair<int,int> pair in eqs)
			{
				if(pair.Second == 0 && pair.First == offset) return true;
				if(pair.First == 0 && pair.Second == offset) return true;
			}
			return false;
		}

		public Dfa Complement()
		{
			Dfa result = Copy();
			for(int i = 0; i < result.accept.Count; i++)
			{
				result.accept[i] = !result.accept[i];
			}
			return result;
		}

		public Dfa Intersection(Dfa other)
		{
			// Uses the product DFA construction as in Hopcroft et al. "Automata Theory, ..."

			Dfa result = new Dfa();

			for(int i = 0; i < states.Count; i++)
			{
				for(int j = 0; j < other.states.Count; j++)
				{
					int[] tr = new int[128];
					for(int c = 0; c < 128; c++)
					{
						int succThis = states[i][c];
						int succOther = other.states[j][c];
						tr[c] = succThis * other.states.Count + succOther;
					}
					result.states.Add(tr);
					result.accept.Add(accept[i] && other.accept[j]);
				}
			}
			return result;
		}

		public Dfa Difference(Dfa other)
		{
			return this.Intersection(other.Complement());
		}

		public bool IsEmpty
		{
			get
			{
				Queue<int> s = new Queue<int>();
				s.Enqueue(0);
				bool[] marked = new bool[states.Count];
				marked[0] = true;
				if(accept[0]) return false; // the start state is an accept state, i.e. this DFA recognizes epsilon

				while(s.Count > 0)
				{
					int i = s.Dequeue();
					for(int c = 0; c < 128; c++)
					{
						if(!marked[states[i][c]])
						{
							if(accept[states[i][c]]) return false;
							marked[states[i][c]] = true;
							s.Enqueue(states[i][c]);
						}
					}
				}
				return true;
			}
		}
	}
}
