using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

using ZX.Utils;
using ZX.Templates.FSM;
using ZX.Templates.Singleton;
using ZX.Templates.Graph;
using ZX.PathFindingStates;
using static ZX.PathFinder.Node.Type;
using static ZX.PathFinder.Node.State;

namespace ZX {
    namespace PathFindingStates {
        public class PathDecidingState : FSMState<PathFinder> {
            private readonly TrailDrawer drawer;
            private Transform transform;
            private Player player;
            private (int, int) startPos;
            private (int, int) endPos;

            public PathDecidingState() {
                drawer = new TrailDrawer();
            }

            public override void Enter(object param) {
                transform = BattleManager.instance.currentPlayer;
                player = transform.GetComponent<Player>();
                startPos = self.WorldToGrid(transform.position);

                self.GenerateGrid();
                self.GenerateGraph();
                self.graph.BellmanFord(startPos, 0);
            }
            public override void Update() {
                Vector2 aim = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                var mousePos = self.WorldToGrid(aim);

                // 获取距离鼠标最近的可到达的点
                int minDist = int.MaxValue;
                int GetDist((int, int) a, (int, int) b) {
                    return Mathf.Abs(a.Item1 - b.Item1) + Mathf.Abs(a.Item2 - b.Item2);
                }
                foreach (var x in self.graph.dist) {
                    (int, int) pos = x.Key;
                    if (GetDist(pos, mousePos) >= minDist) continue;
                    if (!self.map[pos].isStoppable) continue;
                    if (self.graph.dist[pos] > player.movePoint) continue;
                    minDist = GetDist(pos, mousePos);
                    endPos = pos;
                }
                var path = self.graph.GetPath(endPos);

                List<Vector3> points = new List<Vector3> { self.GridToWorld(startPos) };
                while (path.Count > 0) points.Add(self.GridToWorld(path.Pop().Item1));
                drawer.Draw(points);
                
                PlayerUI.deltaMovePointText.text = $"-{self.graph.dist[endPos]}";

                if (Input.GetMouseButtonDown(0)) {
                    player.movePoint -= self.graph.dist[endPos];
                    fsm.TransitionTo<MovingState>(self.graph.GetPath(endPos));
                }
                if (Input.GetMouseButtonDown(1)) fsm.ExitState();
            }
            public override void Exit(object param) {
                drawer.isActive = false;

                PlayerUI.deltaMovePointText.text = " ";
            }
        }

        public class MovingState : FSMState<PathFinder> {
            private Transform transform;
            Stack<((int, int), PathFinder.Edge)> path;
            private int i = 0;

            public override void Enter(object param) {
                transform = BattleManager.instance.currentPlayer;
                path = param as Stack<((int, int), PathFinder.Edge)>;

            }
            public override void Update() {
                if (path.Count > 0 && ++i % 10 == 0) {
                    var pos = path.Pop().Item1;
                    transform.position = self.GridToWorld(pos);
                }

                //移动到终点了
                if (path.Count == 0) fsm.ExitState();
            }
            public override void Exit(object param) { }
        }

    }

    public class PathFinder : Singleton<PathFinder> {
        public FSM<PathFinder> fsm;

        public class Node {
            public (int, int) pos;
            public enum Type {
                EMPTY,
                GROUND,
                PLATFORM,
                LADDER,
            }
            public enum State {
                INVALID,
                STAND,
                CLIMB,
                SCRAMBLE,
                FALL,
            }
            public Type type;
            public State state;
            public int height;
            public int heightNoLadder;
            public bool allowLong;
            public bool allowShort;
            public bool allowStand {
                get { return state != FALL && state != INVALID; }
            }
            public bool isStoppable;
        }

        public class Edge {
            public int dist;
            public Edge(int dist) {
                this.dist = dist;
            }
        }

        private readonly Grid grid;
        private readonly Tilemap groundMap;
        private readonly Tilemap platformMap;
        private readonly Tilemap ladderMap;
        private readonly BoundsInt gridBounds;
        public readonly int margin = 5;

        public readonly Dictionary<(int, int), Node> map;
        public readonly ShortestPath<(int, int), Edge, int> graph;

        public PathFinder() {
            #region Initialize FSM
            fsm = new FSM<PathFinder>(this);
            fsm.AddState<PathDecidingState>();
            fsm.AddState<MovingState>();
            #endregion

            #region Initialize Tilemaps
            grid = GameObject.Find("Grid").GetComponent<Grid>();
            groundMap = grid.transform.Find("Ground").GetComponent<Tilemap>();
            platformMap = grid.transform.Find("Platform").GetComponent<Tilemap>();
            ladderMap = grid.transform.Find("Ladder").GetComponent<Tilemap>();

            BoundsInt b1 = groundMap.cellBounds, b2 = platformMap.cellBounds, b3 = ladderMap.cellBounds;
            gridBounds.xMin = Mathf.Min(b1.xMin, b2.xMin, b3.xMin) - margin;
            gridBounds.xMax = Mathf.Max(b1.xMax, b2.xMax, b3.xMax) + margin;
            gridBounds.yMin = Mathf.Min(b1.yMin, b2.yMin, b3.yMin) - margin;
            gridBounds.yMax = Mathf.Max(b1.yMax, b2.yMax, b3.yMax) + margin;
            gridBounds.zMin = 0; gridBounds.zMax = 1;
            #endregion

            map = new Dictionary<(int, int), Node>();
            graph = new ShortestPath<(int, int), Edge, int>((int s, Edge e) => s + e.dist);
        }

        #region Axis Transformation
        /// <summary> 世界坐标转地图坐标 </summary>
        public (int, int) WorldToGrid(Vector2 p) {
            var ret = Vector2Int.RoundToInt((p - (Vector2)grid.transform.position) / grid.cellSize);
            return (ret.x, ret.y);
        }
        /// <summary> 世界坐标转地图坐标 </summary>
        public Vector2 GridToWorld((int, int) p) {
            return (Vector2)grid.transform.position + grid.cellSize * new Vector2(p.Item1, p.Item2);
        }
        #endregion

        #region Map Detection
        private RaycastHit2D Raycast(Vector2 worldPos, int layerMask) {
            return Physics2D.Raycast(worldPos, Vector2.zero, 1f, layerMask);
        }
        
        /// <summary> 创建地图块 </summary>
        public void GenerateGrid() {
            map.Clear();
            foreach (var cell in gridBounds.allPositionsWithin) {
                var (x, y) = (cell.x, cell.y);
                Vector2 worldPos = GridToWorld((x, y));
                Node node = new Node { pos = (x, y) };
                map[(x, y)] = node;

                if (Raycast(worldPos, Settings.groundMask)) node.type = GROUND;
                else if (Raycast(worldPos, Settings.platformMask)) node.type = PLATFORM;
                else if (Raycast(worldPos, Settings.ladderMask)) node.type = LADDER;
                else node.type = EMPTY;

                if (map.ContainsKey((x, y - 1))) {
                    Node below = map[(x, y - 1)];
                    if (node.type == GROUND || node.type == LADDER) node.height = -1;
                    else if (below.type == PLATFORM) node.height = 0;
                    else node.height = below.height + 1;

                    if (node.type == GROUND) node.heightNoLadder = -1;
                    else if (below.type == PLATFORM) node.heightNoLadder = 0;
                    else node.heightNoLadder = below.heightNoLadder + 1;
                } else {
                    node.height = node.heightNoLadder = 0;
                }

                node.isStoppable = true;
                var playerHit = Raycast(worldPos, Settings.playerMask);
                if (playerHit && !BattleManager.instance.IsCurrentPlayer(playerHit.collider.transform)) node.isStoppable = false;
            }
            
            foreach (var item in map) {
                var (x, y) = item.Key;
                if (!map.ContainsKey((x, y + 1)) || !map.ContainsKey((x, y - 1))) continue;
                Node node = item.Value, above = map[(x, y + 1)], below = map[(x, y - 1)];
                Node.State GetState() {
                    switch (node.type) {
                        case GROUND: return INVALID;
                        case LADDER:
                            if (below.type == GROUND || below.type == PLATFORM) {
                                if (above.type == GROUND) return SCRAMBLE;
                                return STAND;
                            }
                            return CLIMB;
                        default:
                            if (below.type == EMPTY) return FALL;
                            if (above.type == GROUND) return SCRAMBLE;
                            return STAND;
                    }
                }
                node.state = GetState();
                node.allowShort = node.type != GROUND;
                node.allowLong = node.allowShort && above.type != GROUND;

                void Draw(Color color) {
                    float r = .3f;
                    Vector2 v = GridToWorld(node.pos);
                    Vector2 dl = new Vector2(r, -r);
                    Vector2 dr = new Vector2(r, r);
                    Debug.DrawLine(v - dl, v + dl, color, 100000);
                    Debug.DrawLine(v - dr, v + dr, color, 100000);
                }
                /*if (node.heightNoLadder == 0) Draw(Color.white);
                if (node.heightNoLadder == 1) Draw(Color.cyan);
                if (node.heightNoLadder == 2) Draw(Color.blue);
                if (node.state == STAND) Draw(Color.green);
                if (node.state == CLIMB) Draw(Color.yellow);
                if (node.state == FALL) Draw(Color.red);*/
            }
        }

        /// <summary> 建图 </summary>
        public void GenerateGraph() {
            graph.Clear();
            foreach (var item in map) {
                var (x, y) = item.Key;
                if (!item.Value.allowStand) continue;

                void Draw((int, int) pos, Color color) {
                    /*float r = .1f;
                    Vector2 vs = GridToWorld((x, y)), vt = GridToWorld(pos);
                    Debug.DrawLine(vs, vt, color, 100000);
                    Debug.DrawLine(vs + new Vector2(0, r), vt, color, 100000);
                    Debug.DrawLine(vs + new Vector2(r, 0), vt, color, 100000);
                    Debug.DrawLine(vs + new Vector2(-r, 0), vt, color, 100000);
                    Debug.DrawLine(vs + new Vector2(0, -r), vt, color, 100000);*/
                }

                // 相邻块
                foreach (var p in new (int, int)[] { (1, 0), (0, 1), (-1, 0), (0, -1) }) {
                    var np = (x + p.Item1, y + p.Item2);
                    if (!map.ContainsKey(np)) continue;
                    if (!map[np].allowStand) continue;
                    graph.AddEdge((x, y), np, new Edge(1));
                    Draw(np, Color.green);
                }

                // 左右跳
                foreach (var p in new int[] { -1, 1 }) {
                    (int, int) mid = (x + p, y), np = (x + p * 2, y);
                    if (!map.ContainsKey(np)) continue;
                    if (!map[(x, y)].allowLong) continue;
                    if (!map[mid].allowLong || map[mid].allowStand) continue;
                    if (!map[np].allowStand || !map[np].allowLong) continue;
                    graph.AddEdge((x, y), np, new Edge(2));
                    Draw(np, Color.blue);
                }

                // 攀援
                foreach (var p in new (int, int)[] { (-1, 1), (1, 1) }) {
                    var np = (x + p.Item1, y + p.Item2);
                    if (!map.ContainsKey(np)) continue;
                    if (map[(x, y)].state != STAND) continue;
                    if (map[np].state != STAND && map[np].state != SCRAMBLE) continue;
                    graph.AddEdge((x, y), np, new Edge(2));
                    Draw(np, Color.red);
                }

                // 上跳
                foreach (var p in new int[] { 2 }) {
                    var np = (x, y + 2);
                    if (!map.ContainsKey(np)) continue;
                    if (map[(x, y)].state != STAND) continue;
                    if (!map[np].allowStand) continue;
                    graph.AddEdge((x, y), np, new Edge(2));
                    graph.AddEdge(np, (x, y), new Edge(1));
                    Draw(np, Color.yellow);
                }

                // 下跳
                foreach (var p in new int[] { -1, 1 }) {
                    var np = (x + p, y);
                    if (!map.ContainsKey(np)) continue;
                    if (map[(x, y)].state != STAND && map[(x, y)].state != SCRAMBLE) continue;
                    if (map[np].state != FALL || map[np].height > 2) continue;
                    var fall = (x + p, y - map[np].height);
                    if (!map.ContainsKey(fall)) continue;
                    graph.AddEdge((x, y), fall, new Edge(1));
                    Draw(fall, Color.cyan);
                }
            }
        }
        #endregion

        #region Check Freefall
        public List<ISpecialEffect> GetFreefallEffects(){
            GenerateGrid();
            List<ISpecialEffect> ret = new List<ISpecialEffect>();
            foreach(var player in BattleManager.instance.playerList){
                var (x,y) = WorldToGrid(player.position);
				if (map[(x, y)].state != FALL) continue;
                Vector3 goal = GridToWorld((x, y - map[(x, y)].heightNoLadder));
                ret.Add(new SpecialEffects.Move(player, goal, 3f));
			}
            return ret;
		}
        #endregion

		public void Enter(){
            fsm.TransitionTo<PathDecidingState>();
		}
        public bool Update(){
            fsm.Update();
            return fsm.isRunning;
		}
    }

}