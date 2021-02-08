using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using ZX.Trail;
using ZX.Templates.Singleton;
using ZX.Templates.FSM;
using ZX.Utils;
using ZX.BattleStates;
using static ZX.Utils.Math;
using static UnityEngine.Debug;

namespace ZX {
    public interface ISpecialEffect {
        bool Update();
    }
    namespace SpecialEffects {
        public class Move : ISpecialEffect {
            private readonly Transform transform;
            private readonly Vector3 goal;
            private readonly Vector3 direction;
            private readonly float speed;
            public Move(Transform tf, Vector3 goal, float speed) {
                transform = tf;
                this.goal = goal;
                direction = goal - tf.position;
                this.speed = speed * Time.deltaTime;
            }
            public bool Update() {
                if ((goal - transform.position).magnitude <= speed) {
                    transform.position = goal;
                    return false;
                }
                transform.position += direction.normalized * speed;
                return true;
            }
        }
    }

    namespace BattleStates {

        // 战斗开始
        public class BattleStartState : FSMState<BattleManager> {
            public override void Enter(object obj) {
                // 准备
                foreach (var player in self.GetPlayers()) player.Prepare();
            }
            public override void Exit(object obj) { }
            public override void Update() {
                Log("Battle Start!");
                fsm.TransitionTo<RoundStartState>();
            }
        }

        // 回合开始
        public class RoundStartState : FSMState<BattleManager> {
            ///<summary> 获取下一个行动角色 </summary>
            private Transform GetNextPlayer() {
                if (self.attackList.Count == 0) self.GetAttackSequence();
                Transform transform = self.attackList.Dequeue();
                self.attackList.Enqueue(transform);
                if (!transform.GetComponent<Player>().isAlive) return GetNextPlayer();
                return transform;
            }

            public override void Enter(object obj) {
                self.currentPlayer = GetNextPlayer();

                // 回合开始
                if (!self.GetPlayer().RoundStart()) {
                    // 若无法行动则跳过当前回合
                    fsm.TransitionTo<RoundStartState>();
                }

                // 若某一方人物数量为零则游戏结束，否则进入选卡阶段
                if (self.enemyNum <= 0 || self.heroNum <= 0) {
                    fsm.TransitionTo<BattleEndState>();
                } else {
                    fsm.TransitionTo<IdleState>();
                }

                Log($"{self.currentPlayer.name}'s Turn!");
            }
            public override void Exit(object obj) { }
            public override void Update() { }
        }

        // 特殊效果
        public class SpecialEffectState : FSMState<BattleManager> {
            private ISpecialEffect effect;
            public override void Enter(object param) {
                effect = param as ISpecialEffect;
            }
            public override void Update() {
                if (!effect.Update()) fsm.TransitionTo<IdleState>();
            }
            public override void Exit(object param) { }

        }

        // 闲置状态
        public class IdleState : FSMState<BattleManager> {
            private Tuple<int, int> choice;     // 选中的状态与卡牌ID

            protected void HandleInput() {
                // 点击人物则进入移动状态
                if (self.GetPlayer().allowMove) {
                    Transform player = self.GetSelectedObject(Settings.playerMask);
                    PlayerUI playerUI = self.currentPlayer.GetComponent<PlayerUI>();
                    playerUI.outlineType = self.IsCurrentPlayer(player) ? 1 : 0;
                    if (self.IsCurrentPlayer(player)) {
                        if (Input.GetMouseButtonDown(0)) fsm.TransitionTo<MovingState>();
                    }
                }
            }

            public override void Enter(object obj) {
                // 检测人物是否会自由落体
                foreach (var eff in PathFinder.instance.GetFreefallEffects()) {
                    self.effects.Enqueue(eff);
                }
                
                // 特殊效果动画
                if (self.effects.Count > 0) {
                    fsm.TransitionTo<SpecialEffectState>(self.NextEffect());
                }

                if (obj != null) choice = obj as Tuple<int, int>;
                else choice = null;

                // 显示当前玩家专注轨迹，并隐藏敌人的专注轨迹
                foreach (var player in self.GetPlayers()) {
                    player.isTrailVisible = false;
                    player.GetComponent<PlayerUI>().outlineType = 0;
                }
                self.GetPlayer().isTrailVisible = true;

                // 更新卡牌ui
                UIController.instance.SetCards();
            }
            public override void Exit(object obj) { }
            public override void Update() {
                if (choice == null) choice = self.CheckSelectedCardID();
                if (choice != null) {
                    if (choice.Item1 == 0) fsm.TransitionTo<AttackTrailState>(choice);
                    else fsm.TransitionTo<ConcentrateTrailState>(choice);
                    return;
                }
                HandleInput();
            }
        }

        // 人物移动
        public class MovingState : FSMState<BattleManager> {
            public override void Enter(object obj) {
                Log("Moving");
                PathFinder.instance.Enter();
            }
            public override void Exit(object obj) { }
            public override void Update() {
                if (!PathFinder.instance.Update()) fsm.TransitionTo<IdleState>();
            }
        }

        // 选择轨迹
        public class TrailDecidingState : FSMState<BattleManager> {
            protected int cardState;            // 选中的状态
            protected int cardID;               // 选中的卡牌ID
            protected List<Vector2> points;     // 选择的轨迹点

            /// <summary> 获得鼠标位置 </summary>
            protected Vector2 GetPosition() {
                Vector3 input = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                return new Vector2(input.x, input.y);
            }

            /// <summary> 处理点击卡牌事件 </summary>
            protected bool HandleCardSelection() {
                var choice = self.CheckSelectedCardID();
                if (choice != null) {
                    // 选中卡牌则返回选牌阶段或设定轨迹阶段
                    if (choice.Item1 == cardState && choice.Item2 == cardID) {
                        fsm.TransitionTo<IdleState>();
                    } else {
                        fsm.TransitionTo<IdleState>(choice);
                    }
                    return true;
                }
                return false;
            }

            /// <summary> 处理UI事件 </summary>
            protected bool IsOnUI() {
                Transform transform = self.GetSelectedObject();
                if (HandleCardSelection()) return true;

                return false;
            }

            public override void Enter(object obj) {
                Player player = self.GetPlayer();
                (cardState, cardID) = (Tuple<int, int>)obj;
                points = new List<Vector2>();

                // 若选定的卡牌无法发动则回到选牌阶段
                if (cardState == 0 && player.Attackable(cardID) < 0) {
                    fsm.TransitionTo<IdleState>();
                    return;
                } else if (cardState == 1 && player.Concentratable(cardID) < 0) {
                    fsm.TransitionTo<IdleState>();
                    return;
                }

                Log($"Deciding Trail ({cardState}, {cardID})");

            }

        }

        // 选择攻击轨迹
        public class AttackTrailState : TrailDecidingState {
            /// <summary> 处理输入 </summary>
            private void HandleInput() {
                if (IsOnUI()) return;

                AttackTrail currentTrail = self.GetPlayer().GetAttackTrailsAt(cardID)[points.Count];
                currentTrail.DrawTrail(self.currentPlayer.position, GetPosition());

                // 设定人物方向
                self.GetPlayer().direction = Sign(GetPosition().x - self.currentPlayer.position.x);

                // 设定轨迹
                if (Input.GetMouseButtonDown(0)) {
                    // 左键设定轨迹
                    points.Add(GetPosition());
                } else if (Input.GetMouseButtonDown(1)) {
                    // 右键撤销
                    if (points.Count > 0) {
                        currentTrail.ClearTrail();
                        points.RemoveAt(points.Count - 1);
                    } else fsm.TransitionTo<IdleState>();
                }
            }

            /// <summary> 轨迹达到需求数量则转移状态 </summary>
            private void Transition() {
                // 轨迹达到需求数量
                Player player = self.GetPlayer();
                if (points.Count == player.Attackable(cardID)) {
                    player.Attack(cardID, points);
                    fsm.TransitionTo<IdleState>();
                }
            }

            public override void Exit(object obj) {
                // 清除攻击轨迹
                self.GetPlayer().ClearAttackTrails();
            }
            public override void Update() {
                HandleInput();
                Transition();
            }
        }

        // 选择专注轨迹
        public class ConcentrateTrailState : TrailDecidingState {
            private ConcentrateTrail currentTrail;

            /// <summary> 处理输入 </summary>
            private void HandleInput() {
                if (IsOnUI()) return;

                currentTrail = self.GetPlayer().GetConcentrateTrailAt(cardID);
                currentTrail.DrawTrail(self.GetPlayer(), GetPosition());

                // 设定轨迹
                if (Input.GetMouseButtonDown(0)) points.Add(GetPosition());
                else if (Input.GetMouseButtonDown(1)) fsm.TransitionTo<IdleState>();
            }

            /// <summary> 轨迹达到需求数量则转移状态 </summary>
            private void Transition() {
                // 轨迹达到需求数量
                Player player = self.GetPlayer();
                if (points.Count == player.Concentratable(cardID)) {
                    player.Concentrate(cardID, points);
                    fsm.TransitionTo<IdleState>();
                }
            }

            public override void Exit(object obj) {
                // 清除专注轨迹
                currentTrail?.ClearTrail();
            }
            public override void Update() {
                HandleInput();
                Transition();
            }
        }

        // 回合结束
        public class RoundEndState : FSMState<BattleManager> {
            public override void Enter(object obj) {
                // 隐藏当前玩家专注轨迹
                self.GetPlayer().isTrailVisible = false;
                self.GetPlayer().RoundEnd();
                fsm.TransitionTo<RoundStartState>();
            }
            public override void Exit(object obj) { }
            public override void Update() { }
        }

        // 战斗结束
        public class BattleEndState : FSMState<BattleManager> {
            public override void Enter(object obj) {
                Log("Battle End!");
            }
            public override void Exit(object obj) { }
            public override void Update() { }
        }
    }

    public class BattleManager : MonoSingleton<BattleManager> {
        public FSM<BattleManager> fsm;

        #region 基本战斗信息
        public int heroNum;
        public int enemyNum;
        #endregion

        #region 攻击顺序相关

        public Transform currentPlayer;
        public bool IsCurrentPlayer(Transform player) {
            return player == currentPlayer;
        }
        public bool IsCurrentPlayer(Player player) {
            return player == GetPlayer();
        }

        public Queue<Transform> attackList;

        [Header("出战人物")]
        public List<Transform> playerList;

        /// <summary> 获取出战顺序 </summary>
        public void GetAttackSequence() {
            attackList = new Queue<Transform>();
            foreach (var tf in playerList) {
                if (tf.GetComponent<Player>().type == Player.Type.OBSTACLE) continue;
                attackList.Enqueue(tf);
            }
        }

		#endregion

		#region 特殊效果动画
		public Queue<ISpecialEffect> effects;
        public ISpecialEffect NextEffect() {
            return effects.Count == 0 ? null : effects.Dequeue();
        }
		#endregion

		/// <summary> 获取当前玩家的Player类 </summary>
		public Player GetPlayer() {
            return currentPlayer.GetComponent<Player>();
        }

        /// <summary> 获取所有玩家的Player类 </summary>
        public List<Player> GetPlayers() {
            List<Player> players = new List<Player>();
            foreach (var tf in playerList) {
                players.Add(tf.GetComponent<Player>());
            }
            return players;
        }

        /// <summary> 获取左键或右键点击的卡牌ID </summary>
        /// <returns> (选中状态, 卡牌ID) 或 null（未选中） </returns>
        public Tuple<int, int> CheckSelectedCardID() {
            Transform transform = GetSelectedObject();
            CardUI cardUI = transform ? transform.GetComponent<CardUI>() : null;
            if (!cardUI) return null;
            foreach (int mouse in new List<int> { 0, 1 }) {
                if (!Input.GetMouseButtonDown(mouse)) continue;
                return new Tuple<int, int>(mouse, cardUI.id);
            }
            return null;
        }

        /// <summary> 获取鼠标所在的Transform </summary>
        public Transform GetSelectedObject() {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(ray.origin.x, ray.origin.y), Vector2.zero);
            return hit.collider ? hit.collider.transform : null;
        }
        /// <summary> 获取鼠标所在的Transform </summary>
        public Transform GetSelectedObject(int layerMask) {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(ray.origin.x, ray.origin.y), Vector2.zero, 1f, layerMask);
            return hit.collider ? hit.collider.transform : null;
        }

        /// <summary> 点击回合结束按钮 </summary>
        public void CheckRollEndState() {
            fsm.TransitionTo<RoundEndState>();
        }

        protected override void Awake() {
            base.Awake();

            effects = new Queue<ISpecialEffect>();

			#region Initialize FSM
			fsm = new FSM<BattleManager>(this);
            fsm.AddState<BattleStartState>();
            fsm.AddState<RoundStartState>();
            fsm.AddState<SpecialEffectState>();
            fsm.AddState<IdleState>();
            fsm.AddState<MovingState>();
            fsm.AddState<AttackTrailState>();
            fsm.AddState<ConcentrateTrailState>();
            fsm.AddState<RoundEndState>();
            fsm.AddState<BattleEndState>();
			#endregion
		}

		private void Start() {
            GetAttackSequence();
            fsm.TransitionTo<BattleStartState>();
        }

        private void Update() {
            string GetString() {
                Transform transform = GetSelectedObject(Settings.playerMask);
                if (!transform) return "";
                Player player = transform.GetComponent<Player>();
                return $"{player.name}\n" + Common.EnumToString(player.buffs, "\n");
            }
            UIController.instance.buffListText.text = "BuffList: " + GetString();

            fsm.Update();
        }
    }

}