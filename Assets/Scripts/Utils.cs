using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Debug;

namespace ZX {
	namespace Utils {
		public static class Common {
			public static void Swap<T>(ref T a, ref T b) {
				T tmp = a; a = b; b = tmp;
			}
			public static void Swap<T>(List<T> list, int i, int j) {
				T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
			}
			public static void RandomShuffle<T>(List<T> list) {
				for (int i = list.Count; i > 1; i--) {
					int j = Rand.GetInt(0, i);
					Swap(list, i - 1, j);
				}
			}
			public static string EnumToString<T>(IEnumerable<T> ts, string par = " ") {
				if (ts.Count() == 0) return "";
				string str = "";
				foreach (T t in ts) str += t + par;
				return str.Remove(str.Length - par.Length, par.Length);
			}
		}
		public static class Math {
			public static float Sin(float deg) => Mathf.Sin(deg * Mathf.Deg2Rad);
			public static float Cos(float deg) => Mathf.Cos(deg * Mathf.Deg2Rad);
			public static float Tan(float deg) => Mathf.Tan(deg * Mathf.Deg2Rad);
			public static int Round(float f) => Mathf.RoundToInt(f);
			public static int Sign(float f) => Round(Mathf.Sign(f));
			public static float Cross(Vector2 a, Vector2 b) => Vector3.Cross(a, b).z;
			public static float Arg(Vector2 v) => Vector2.SignedAngle(Vector2.right, v);
			public static bool IsIntersect(Vector2 lA1, Vector2 lA2, Vector2 lB1, Vector2 lB2) {
				if (Sign(Cross(lB2 - lB1, lA1 - lB1)) == Sign(Cross(lB2 - lB1, lA2 - lB1))) return false;
				if (Sign(Cross(lA2 - lA1, lB1 - lA1)) == Sign(Cross(lA2 - lA1, lB2 - lA1))) return false;
				return true;
			}
		}
		public static class Rand {
			private static int seed;
			private static void GetRandom() {
				int newSeed = (int)DateTime.Now.Ticks;
				if (newSeed != seed) UnityEngine.Random.InitState(seed = newSeed);
			}
			public static int GetInt(int min, int max) => UnityEngine.Random.Range(min, max);
			public static float GetFloat() => UnityEngine.Random.value;
			public static float GetFloat(float min, float max) => min + (max - min) * GetFloat();
		}
		public static class MyRayCast {
			public static RaycastHit2D[] RaycastAll(Vector2 start, Vector2 end) {
				return Physics2D.RaycastAll(start, end - start, (end - start).magnitude);
			}
			public static RaycastHit2D[] RaycastAll(Vector2 start, Vector2 end, int layerMask) {
				return Physics2D.RaycastAll(start, end - start, (end - start).magnitude, layerMask);
			}
			public static RaycastHit2D[] RaycastAll(Vector2 start, Vector2 end, Color color) {
				DrawLine(start, end, color);
				return RaycastAll(start, end);
			}

			public static RaycastHit2D Raycast(Vector2 start, Vector2 end) {
				return Physics2D.Raycast(start, end - start, (end - start).magnitude);
			}
			public static RaycastHit2D Raycast(Vector2 start, Vector2 end, Color color) {
				DrawLine(start, end, color);
				return Raycast(start, end);
			}
		}
		public class TrailDrawer {
			private readonly GameObject line;
			private readonly LineRenderer renderer;
			public bool isActive {
				get { return line.activeSelf; }
				set { line.SetActive(value); }
			}

			public TrailDrawer() {
				line = new GameObject("Line");
				line.SetActive(false);
				renderer = line.AddComponent<LineRenderer>();
				renderer.material = Resources.Load<Material>("Material/Line");
				renderer.startColor = Color.red;
				renderer.endColor = Color.green;
			}
			~TrailDrawer() {
				UnityEngine.Object.Destroy(line);
			}

			public void Update(List<Vector2> points) {
				List<Vector3> vs = points.ConvertAll(v => (Vector3)v);
				renderer.positionCount = vs.Count;
				renderer.SetPositions(vs.ToArray());
				renderer.widthMultiplier = 0.1f;
			}
			public void Update(List<Vector3> points) {
				renderer.positionCount = points.Count;
				renderer.SetPositions(points.ToArray());
				renderer.widthMultiplier = 0.1f;
			}

			public void Draw(List<Vector2> points) {
				Update(points);
				isActive = true;
			}
			public void Draw(List<Vector3> points) {
				Update(points);
				isActive = true;
			}
		}
	}
	namespace Templates {
		namespace Graph {
			public class Graph<TNode, TValue> {
				public class Edge {
					public TNode from;
					public TNode to;
					public TValue value;
				}
				public readonly Dictionary<TNode, List<Edge>> G;

				public Graph() { G = new Dictionary<TNode, List<Edge>>(); }
				public void Clear() { G.Clear(); }
				public void AddNode(TNode u) {
					if (!G.ContainsKey(u)) G[u] = new List<Edge>();
				}
				public void AddEdge(TNode from, TNode to, TValue value) {
					AddNode(from); AddNode(to);
					G[from].Add(new Edge { from = from, to = to, value = value });
				}

				public List<Edge> this[TNode u] {
					get { return G[u]; }
				}
				public List<TNode> GetNodes() {
					List<TNode> nodes = new List<TNode>();
					foreach (var node in G) nodes.Add(node.Key);
					return nodes;
				}
			}

			/// <summary> 最短路 </summary>
			/// <typeparam name="TNode"> 节点类型 </typeparam>
			/// <typeparam name="TValue"> 边权类型 </typeparam>
			/// <typeparam name="TDist"> 距离类型 </typeparam>
			public class ShortestPath<TNode, TValue, TDist> where TDist : IComparable {
				public Graph<TNode, TValue> graph;
				public Func<TDist, TValue, TDist> getDist;

				public readonly Dictionary<TNode, TDist> dist;
				public readonly Dictionary<TNode, (TNode, TValue)> pre;

				public ShortestPath(Func<TDist, TValue, TDist> add) {
					graph = new Graph<TNode, TValue>();
					dist = new Dictionary<TNode, TDist>();
					pre = new Dictionary<TNode, (TNode, TValue)>();
					this.getDist = add;
				}
				public void Clear() { graph.Clear(); }
				public void AddEdge(TNode from, TNode to, TValue value) {
					graph.AddEdge(from, to, value);
				}
				public void BellmanFord(TNode src, TDist init) {
					if (!graph.G.ContainsKey(src)) return;
					dist.Clear(); pre.Clear();
					Dictionary<TNode, bool> vis = new Dictionary<TNode, bool>();
					foreach (var node in graph.GetNodes()) vis.Add(node, false);
					Queue<TNode> q = new Queue<TNode>();
					q.Enqueue(src); vis[src] = true;
					dist[src] = init; pre[src] = (src, default(TValue));
					while (q.Count > 0) {
						TNode u = q.Dequeue(); vis[u] = false;
						foreach (var edge in graph[u]) {
							TNode v = edge.to; TValue w = edge.value;
							TDist newDist = getDist(dist[u], w);
							if (!dist.ContainsKey(v) || dist[v].CompareTo(newDist) > 0) {
								dist[v] = newDist; pre[v] = (u, w);
								if (!vis[v]) { vis[v] = true; q.Enqueue(v); }
							}
						}
					}
				}
				public Stack<(TNode, TValue)> GetPath(TNode end) {
					if (!dist.ContainsKey(end)) return new Stack<(TNode, TValue)>();
					Stack<(TNode, TValue)> path = new Stack<(TNode, TValue)>();
					for (TNode cur = end; !pre[cur].Item1.Equals(cur);) {
						path.Push((cur, pre[cur].Item2)); cur = pre[cur].Item1;
					}
					return path;
				}
			}
		}
		namespace FSM {
			public class FSMState<T> {
				public T self;
				public FSM<T> fsm;
				public virtual void Enter(object param) { }
				public virtual void Exit(object param) { }
				public virtual void Update() { }
			}
			public class FSM<T> {
				private readonly T self;
				private readonly Dictionary<Type, FSMState<T>> stateDic;
				public FSMState<T> currentState { get; private set; }
				public FSMState<T> previousState { get; private set; }
				public bool isRunning { get { return currentState != null; } }
				public FSM(T self) {
					this.self = self;
					stateDic = new Dictionary<Type, FSMState<T>>();
					previousState = currentState = null;
				}
				public void AddState(params FSMState<T>[] states) {
					foreach (var state in states) {
						Assert(!stateDic.ContainsKey(state.GetType()), "State already exists.");
						state.self = self; state.fsm = this;
						stateDic.Add(state.GetType(), state);
					}
				}
				public void AddState<S>() where S : FSMState<T>, new() {
					Assert(!stateDic.ContainsKey(typeof(S)), "State already exists.");
					stateDic.Add(typeof(S), new S() { self = self, fsm = this });
				}
				public void TransitionTo<S>(object enter = null, object exit = null)
				where S : FSMState<T>, new() {
					if (!stateDic.ContainsKey(typeof(S))) {
						LogWarning($"State {typeof(S)} not exists.");
						AddState<S>();
					}
					if (currentState != null) currentState.Exit(exit);
					previousState = currentState;
					currentState = stateDic[typeof(S)];
					currentState.Enter(enter);
				}
				public void ExitState(object exit = null) {
					if (currentState != null) currentState.Exit(exit);
					previousState = currentState;
					currentState = null;
				}
				public void Update() {
					if (currentState != null) currentState.Update();
				}
			}
		}
		namespace Singleton {
			public class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T> {
				/*private static T m_instance;
				public static T instance {
					get {
						if (!m_instance) {
							m_instance = FindObjectOfType(typeof(T)) as T;
							if (!m_instance) {
								GameObject gameObject = new GameObject(typeof(T).ToString());
								m_instance = gameObject.AddComponent<T>();
							}
						}
						return m_instance;
					}
				}
				protected virtual void Awake() {
					if (m_instance == null) {
						m_instance = this as T;
					} else Destroy(gameObject);
				}*/
				public static T instance { get; private set; }
				protected virtual void Awake() {
					if (instance == null) {
						instance = this as T;
					} else Destroy(gameObject);
				}
			}
			public class Singleton<T> where T : Singleton<T>, new() {
				private static T m_instance = null;
				public static T instance => m_instance ?? (m_instance = new T());
			}
		}
	}
}
