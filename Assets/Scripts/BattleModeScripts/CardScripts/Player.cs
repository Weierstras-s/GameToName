using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

using ZX.Utils;
using ZX.Trail;
using static ZX.Utils.Math;

namespace ZX {
	public class Player : MonoBehaviour {
		public enum Type {
			PLAYER,
			OBSTACLE
		}

		#region Events
		public Action<Player> onDead;

		/// <summary> 移动前后的坐标 </summary>
		public Action<Player, Vector2, Vector2> onMove;

		/// <summary> 修改前后的HP值 </summary>
		public Action<Player, float, float> onHPChange;
		#endregion

		#region BasicAttributes
		[Header("BasicAttributes")]
		public Type type;

		public int maxCards;
		public List<int> presetCardsID;

		public float maxHitPoint;
		public float maxBodyHitPoint;
		public float resistance;

		public int maxActionPoint;
		public int maxMovePoint;
		public int maxConcentratePoint;

		public float baseAttack;
		public float baseDefense;
		#endregion

		#region BattleState
		[Header("BattleState")]

		private Vector3 m_position;
		public Vector2 position {
			get { return transform.position; }
		}

		private int m_direction;
		public int direction {
			get { return m_direction; }
			set {
				if (value != 0) m_direction = value;
				foreach (var trail in GetActivatedTrailList()) {
					trail.UpdateTrail(this);
				}
			}
		}

		public int actionPoint;
		public int movePoint;

		public bool isAlive;

		public bool isDodge;

		public class BodyHitPoint {
			enum Position { UP, CENTER, DOWN }
			private readonly Player player;
			private readonly float stdHp;
			private readonly Dictionary<Position, float> hp;
			private readonly Dictionary<Position, int> cnt;
			public BodyHitPoint(Player player) {
				this.player = player;
				hp = new Dictionary<Position, float>();
				cnt = new Dictionary<Position, int>();
				stdHp = player.maxBodyHitPoint;
				for (int i = 0; i < 3; ++i) {
					var pos = (Position)i;
					hp[pos] = stdHp; cnt[pos] = 0;
				}
			}
			private Position GetBodyPosition(float dir) {
				const float begin = 60f, end = 120f;
				if (begin < dir && dir < end) return Position.UP;
				if (begin < -dir && -dir < end) return Position.DOWN;
				return Position.CENTER;
			}
			public float this[float dir] {
				get { return hp[GetBodyPosition(dir)]; }
				set {
					Position pos = GetBodyPosition(dir);
					float newHp = value;
					if (newHp <= 0) {
						newHp = stdHp * (1 + Mathf.Pow(cnt[pos], 2));
						++cnt[pos];
						Buff buff = null;
						switch (pos) {
							case Position.UP:
								buff = new Card.Effects.Buffs.UnableConcentrate(1, player);
								break;
							case Position.CENTER:
								buff = new Card.Effects.Buffs.UnableAction(1);
								break;
							case Position.DOWN:
								buff = new Card.Effects.Buffs.UnableMove(1);
								break;
						}
						player.AddBuff(buff);
					}
					hp[pos] = newHp;
				}
			}
		}
		public BodyHitPoint bodyHitPoint;

		private float m_hitPoint;
		public float hitPoint {
			get { return m_hitPoint; }
			set {
				value = Mathf.Clamp(value, 0, maxHitPoint);
				if (value != m_hitPoint) onHPChange?.Invoke(this, m_hitPoint, value);
				m_hitPoint = value;
				if (value <= 0) onDead?.Invoke(this);
			}
		}

		#endregion

		#region Buff

		/// <summary> Buff列表 </summary>
		public List<Buff> buffs;

		/// <summary> 加个Buff </summary>
		public void AddBuff(Buff buff) {
			buffs.Add(buff);
		}

		/// <summary> Buff生效 </summary>
		private void ActivateBuff(Buff.TimeStamp time) {
			List<Buff> newBuffs = new List<Buff>();
			foreach (Buff buff in buffs) {
				if (buff.actTime == time) buff.Activate(this);
				if (buff.Exists()) newBuffs.Add(buff);
			}
			buffs = newBuffs;
		}

		public float attack {
			get {
				float atk = baseAttack;
				foreach (Buff buff in buffs) atk = buff.NewAttack(atk);
				return atk;
			}
		}
		public float defense {
			get {
				float def = baseDefense;
				foreach (Buff buff in buffs) def = buff.NewDefence(def);
				return def;
			}
		}
		public bool allowConcentrate {
			get {
				bool ret = true;
				foreach (Buff buff in buffs) ret &= buff.allowConcentrate;
				return ret;
			}
		}
		public bool allowMove {
			get {
				bool ret = true;
				foreach (Buff buff in buffs) ret &= buff.allowMove;
				return ret;
			}
		}
		public bool allowAction {
			get {
				bool ret = true;
				foreach (Buff buff in buffs) ret &= buff.allowAction;
				return ret;
			}
		}

		#endregion

		#region Card

		public CardManager cardManager;

		/// <summary> 获取某张卡牌的攻击轨迹列表 </summary>
		/// <param name="pos"> 卡牌在手卡中的序号 </param>
		public List<AttackTrail> GetAttackTrailsAt(int pos) {
			if (!cardManager.handCards[pos].attack) return null;
			return cardManager.handCards[pos].attack.trails;
		}
		/// <summary> 清除所有攻击轨迹 </summary>
		public void ClearAttackTrails() {
			foreach (var card in cardManager.handCards) {
				if (!card.attack) continue;
				card.attack.ClearTrails();
			}
		}

		/// <summary> 获取某张卡牌的专注轨迹 </summary>
		/// <param name="pos"> 卡牌在手卡中的序号 </param>
		public ConcentrateTrail GetConcentrateTrailAt(int pos) {
			if (!cardManager.handCards[pos].concentrate) return null;
			return cardManager.handCards[pos].concentrate.trail;
		}

		/// <summary> 获取已激活的专注轨迹列表 </summary>
		public List<ConcentrateTrail> GetActivatedTrailList() {
			List<ConcentrateTrail> trails = new List<ConcentrateTrail>();
			foreach (var card in cardManager.concentratedCards) {
				if (!card.concentrate.trail.isExist) continue;
				trails.Add(card.concentrate.trail);
			}
			return trails;
		}
		/// <summary> 已激活专注轨迹的可见状态 </summary>
		public bool isTrailVisible {
			set {
				foreach (var trail in GetActivatedTrailList()) {
					if (!BattleManager.instance.IsCurrentPlayer(this) && trail.isHidden) {
						trail.drawer.isActive = false;
					} else trail.drawer.isActive = value;
				}
			}
		}

		#endregion

		#region Battle

		/// <summary> 战斗开始 </summary>
		public void Prepare() {
			cardManager = new CardManager(presetCardsID, this);
			buffs = new List<Buff>();
			m_hitPoint = maxHitPoint;
		}

		/// <summary> 回合开始 </summary>
		public bool RoundStart() {
			bool ret = allowAction;
			ActivateBuff(Buff.TimeStamp.START);
			if (!ret) return false;
			actionPoint = maxActionPoint;
			movePoint = maxMovePoint;
			cardManager.RemoveCardsAtRoundStart();
			cardManager.DrawCard(maxCards - cardManager.handCards.Count);
			return true;
		}

		/// <summary> 回合结束 </summary>
		public void RoundEnd() {
			ActivateBuff(Buff.TimeStamp.END);
			cardManager.RemoveCardsAtRoundEnd();
		}
		#endregion

		#region Action
		/// <summary> 询问是否能够攻击 </summary>
		/// <param name="pos"> 卡牌在手卡中的序号 </param>
		/// <returns> 无法攻击则返回 -1，能够攻击则返回需要设定的轨迹数量 </returns>
		public int Attackable(int pos) {
			return cardManager.Attackable(pos);
		}

		/// <summary> 攻击 </summary>
		/// <param name="pos"> 卡牌在手卡中的序号 </param>
		public void Attack(int pos, List<Vector2> goals) {
			cardManager.Attack(pos, goals);
		}

		/// <summary> 询问是否能专注或取消专注 </summary>
		/// <param name="pos"> 卡牌在手卡中的序号 </param>
		/// <returns> 无法专注则返回 -1，能够攻击则返回需要设定的轨迹数量 (1) </returns>
		public int Concentratable(int pos) {
			if (!allowConcentrate) return -1;
			return cardManager.Concentratable(pos);
		}

		/// <summary> 专注或取消专注 </summary>
		/// <param name="pos"> 卡牌在手卡中的序号 </param>
		/// <param name="goal"> 若取消专注则 goal 为空 </param>
		public void Concentrate(int pos, List<Vector2> goal) {
			cardManager.Concentrate(pos, goal);
		}

		#endregion

		private void Awake() {
			isAlive = true;
			isDodge = false;
			m_position = transform.position;
			m_direction = 1;

			maxCards = 4;
			maxHitPoint = 10;
			maxBodyHitPoint = 6;
			maxActionPoint = 2;
			maxConcentratePoint = 2;
			maxMovePoint = 15;
			baseAttack = 1;
			presetCardsID = new List<int> { 1, 2, 3, 3 };

			bodyHitPoint = new BodyHitPoint(this);

			onHPChange += (Player player, float oldHP, float newHP) => {
				if (newHP < oldHP) {
					GetComponent<PlayerUI>().ShowDamageText(oldHP - newHP);
				} else if (newHP > oldHP) {
					GetComponent<PlayerUI>().ShowHealText(newHP - oldHP);
				}
			};
			onMove += (Player player, Vector2 oldPos, Vector2 newPos) => {
				direction = Sign(newPos.x - oldPos.x);
				foreach (var trail in GetActivatedTrailList()) {
					trail.UpdateTrail(this);
				}
			};
			onDead += (Player player) => {
				isAlive = false;
				isTrailVisible = false;
				BattleManager.instance.playerList.Remove(transform);
				Destroy(gameObject);
			};
		}

		private void Update() {
			if (transform.position != m_position) {
				onMove?.Invoke(this, m_position, transform.position);
				m_position = transform.position;
			}
		}
	}

}
