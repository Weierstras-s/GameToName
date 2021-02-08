using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

using ZX.Utils;
using static ZX.Utils.Math;
using static UnityEngine.Debug;

namespace ZX.Trail {
	public class CardEffect {
		/// <summary> 卡牌打出效果 </summary>
		public virtual void Activate(Player player) { }
	}

	public class AttackEffect {
		public float priority;

		public AttackEffect(int priority) { this.priority = priority; }

		/// <summary> 攻击效果 (玩家, 目标, 打中目标的方向)  </summary>
		public virtual void Activate(AttackTrail self, Player player, Player enemy, float dir) { }
	}

	public class ConcentrateEffect {
		public float priority;

		public ConcentrateEffect(int priority) { this.priority = priority; }

		/// <summary> 专注效果 (玩家, 攻击方, 攻击轨迹)  </summary>
		public virtual void Activate(ConcentrateTrail self, Player player, Player attacker, AttackTrail trail) { }
	}

	public class AttackTrail {
		public int power;

		#region Effects
		public List<AttackEffect> effects;

		/// <summary> 攻击 </summary>
		public void Attack(Player player) {
			List<(Action, float)> actions = new List<(Action, float)>();
			foreach (var (enemy, p) in targetedEnemies) {
				float ang = Arg(p - (Vector2)enemy.transform.position);
				foreach (var trail in enemy.GetActivatedTrailList()) {
					Log($"轨迹检测：{enemy.name}, {IntersectWith(trail)}");
					if (!IntersectWith(trail)) continue;
					actions.AddRange(trail.Activate(enemy, player, this));
				}
				foreach (var effect in effects) {
					actions.Add((() => effect.Activate(this, player, enemy, ang), effect.priority));
				}
			}
			actions.Sort(((Action, float) L, (Action, float) R) => R.Item2.CompareTo(L.Item2));
			foreach (var (action, _) in actions) action();
		}
		#endregion

		public AttackTrail() {
			targetedEnemies = new List<(Player, Vector2)>();
			points = new List<Vector2>();
			drawer = new TrailDrawer();

			effects = new List<AttackEffect>();
		}

		/// <summary> 获取某种类型的效果 </summary>
		/// <typeparam name="T"> 效果类型 </typeparam>
		public List<AttackEffect> GetEffects<T>() where T : AttackEffect {
			List<AttackEffect> ret = new List<AttackEffect>();
			foreach (var effect in effects) {
				if (effect.GetType() == typeof(T)) ret.Add(effect);
			}
			return ret;
		}
		/// <summary> 获取某种类型及其子类的效果 </summary>
		/// <typeparam name="T"> 效果类型 </typeparam>
		public List<AttackEffect> GetSubeffects<T>() where T : AttackEffect {
			List<AttackEffect> ret = new List<AttackEffect>();
			foreach (var effect in effects) {
				Type type = effect.GetType(), baseType = typeof(T);
				if (type == baseType || type.IsSubclassOf(baseType)) ret.Add(effect);
			}
			return ret;
		}

		#region Trail

		/// <summary> 获取轨迹攻击到的敌人列表并截断轨迹 </summary>
		/// <summary> 通过终点对起点的相对位置设定轨迹上的关键点 </summary>
		public Func<Vector2, List<Vector2>> setPoints;

		public List<Vector2> points;
		private readonly List<(Player, Vector2)> targetedEnemies;

		private readonly TrailDrawer drawer;

		public void SetPoints(Vector2 player, Vector2 goal) {
			points = setPoints(goal - player);
			targetedEnemies.Clear();
			points = points.ConvertAll(v => v + player);
			List<Vector2> cut = new List<Vector2> { points[0] };
			for (int i = 1; i < points.Count; i++) {
				int layerMask = Settings.groundMask | Settings.playerMask;
				RaycastHit2D[] hits = MyRayCast.RaycastAll(points[i - 1], points[i], layerMask);
				bool isHit = false;
				foreach (var hit in hits) {
					if (BattleManager.instance.IsCurrentPlayer(hit.transform)) continue;
					cut.Add(hit.point);
					isHit = true;
					Player enemy = hit.collider.GetComponent<Player>();
					if (enemy) targetedEnemies.Add((enemy, hit.point));
					break;
				}
				if (!isHit) cut.Add(points[i]);
				else break;
			}
			points = cut;
		}

		public bool IntersectWith(ConcentrateTrail trail) {
			List<Vector2> con = trail.GetPoints();
			for (int i = 1; i < points.Count; i++) {
				for (int j = 1; j < con.Count; j++) {
					Vector2 A1 = points[i], A2 = points[i - 1];
					Vector2 B1 = con[j], B2 = con[j - 1];
					if (IsIntersect(A1, A2, B1, B2)) return true;
				}
			}
			return false;
		}

		/// <summary> 绘制轨迹 </summary>
		public void DrawTrail(Vector2 player, Vector2 goal) {
			SetPoints(player, goal);
			drawer.Draw(points);
			foreach (var p in BattleManager.instance.GetPlayers()) {
				p.isTrailVisible = false;
				p.GetComponent<PlayerUI>().outlineType = 0;
			}
			foreach (var (p, _) in targetedEnemies) {
				if (p.type != Player.Type.OBSTACLE) p.isTrailVisible = true;
				p.GetComponent<PlayerUI>().outlineType = 1;
			}
		}

		/// <summary> 清除轨迹 </summary>
		public void ClearTrail() {
			drawer.isActive = false;
		}

		#endregion
	}

	public class ConcentrateTrail {
		public int power;

		#region Effects
		public List<ConcentrateEffect> arbitraryEffects;
		public List<ConcentrateEffect> reactEffects;
		public List<ConcentrateEffect> terminateEffects;

		/// <summary> 专注效果 (玩家, 攻击方, 攻击轨迹) </summary>
		public List<(Action, float)> Activate(Player player, Player attacker, AttackTrail trail) {
			List<(Action, float)> ret = new List<(Action, float)>();
			void Activate(ConcentrateTrail self, List<ConcentrateEffect> effects) {
				foreach (var effect in effects) {
					ret.Add((() => effect.Activate(self, player, attacker, trail), effect.priority));
				}
			}
			if (trail.power > power) {
				// 终止
				Activate(this, terminateEffects);
			} else {
				// 反应
				Activate(this, reactEffects);
				// 任意反应
				foreach (var other in player.GetActivatedTrailList()) {
					Activate(other, other.arbitraryEffects);
				}
			}
			return ret;
		}
		#endregion

		#region Attributes
		public Card.Card card;
		private bool m_isExist;
		public bool isExist {
			get { return m_isExist; }
			set {
				m_isExist = value;
				if (!value) ClearTrail();
			}
		}
		public bool delayedDeletion;
		#endregion

		public ConcentrateTrail(Card.Card card) {
			this.card = card;
			isExist = true;
			delayedDeletion = false;
			points = new List<Vector2>();
			drawer = new TrailDrawer();

			arbitraryEffects = new List<ConcentrateEffect>();
			reactEffects = new List<ConcentrateEffect>();
			terminateEffects = new List<ConcentrateEffect>();
		}

		#region Trail

		public static readonly float precision = 3f;
		public float radius;
		public List<(float, float)> ranges;
		public readonly List<Vector2> points;
		public readonly TrailDrawer drawer;
		public bool isHidden;

		private Player player;
		public List<Vector2> GetPoints() {
			return points.ConvertAll(v => v * new Vector2(player.direction, 1) + player.position);
		}

		/// <summary> 通过轨迹起点和终点设定轨迹上的关键点 </summary>
		public void SetPoints(Vector2 goal) {
			Vector2 dir = goal.normalized;
			(float, float) range = ranges[0];
			float minDist = float.MaxValue;
			foreach (var (x, y) in ranges) {
				Vector2 begin = new Vector2(Cos(x), Sin(x));
				Vector2 end = new Vector2(Cos(y), Sin(y));
				float dist = Mathf.Min(Vector2.Angle(begin, dir), Vector2.Angle(dir, end));
				if (Cross(begin, dir) > 0 && Cross(dir, end) > 0) {
					range = (x, y);
					minDist = 0;
				} else if (dist < minDist) {
					range = (x, y);
					minDist = dist;
				}
			}
			SetRange(range);
		}
		public void SetRange((float, float) range) {
			points.Clear();
			var (begin, end) = range;
			while (end < begin) end += 360.0f;
			for (float a = begin; a <= end; a += precision) {
				points.Add(new Vector2(Cos(a), Sin(a)) * radius);
			}
		}

		/// <summary> 绘制轨迹 </summary>
		public void DrawTrail(Player player) {
			this.player = player;
			drawer.Draw(GetPoints());
		}
		public void UpdateTrail(Player player) {
			this.player = player;
			drawer.Update(GetPoints());
		}
		public void DrawTrail(Player player, Vector2 goal) {
			SetPoints((goal - player.position) * new Vector2(player.direction, 1));
			DrawTrail(player);
		}

		/// <summary> 清除轨迹 </summary>
		public void ClearTrail() {
			drawer.isActive = false;
		}

		#endregion
	}
}