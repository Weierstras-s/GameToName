using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

using ZX.Trail;
using static UnityEngine.Debug;

namespace ZX.Card {
	namespace Effects {
		namespace Buffs {
			public class DamageBuff : Buff {
				private readonly float damage;
				public DamageBuff(int duration, float damage) : base(duration) {
					actTime = TimeStamp.END;
					this.damage = damage;
				}
				public override void Activate(Player player) => player.hitPoint -= damage;
			}

			public class UnableConcentrate : Buff {
				public UnableConcentrate(int duration, Player player) : base(duration) {
					actTime = TimeStamp.END;
					allowConcentrate = false;
					player.cardManager.concentratedCards.Clear();
				}
			}
			public class UnableMove : Buff {
				public UnableMove(int duration) : base(duration) {
					actTime = TimeStamp.END;
					allowMove = false;
				}
			}
			public class UnableAction : Buff {
				public UnableAction(int duration) : base(duration) {
					actTime = TimeStamp.START;
					allowAction = false;
				}
			}

		}
		namespace CardEffects {
			public class DrawCard : CardEffect {
				private readonly int num;
				public DrawCard(int num) { this.num = num; }
				public override void Activate(Player player) {
					player.cardManager.DrawCard(num);
				}
			}
			public class DiscardCard : CardEffect {
				private readonly List<int> pos;
				public DiscardCard(int num) {
					pos = new List<int>();
					for (int i = 0; i < num; ++i) pos.Add(i);
				}
				public DiscardCard(List<int> pos) { this.pos = pos; }
				public override void Activate(Player player) {
					foreach (var i in pos) player.cardManager.RemoveCard(i);
				}
			}
			public class Heal : CardEffect {
				private readonly float power;
				public Heal(float power) { this.power = power; }
				public override void Activate(Player player) {
					player.hitPoint += power;
				}
			}
		}
		namespace AttackEffects {
			public class Damage : AttackEffect {
				private readonly float power;
				public Damage(float power) : base(2) { this.power = power; }
				public override void Activate(AttackTrail self, Player player, Player enemy, float dir) {
					if (enemy.isDodge) {
						enemy.isDodge = false;
						return;
					}
					float damage = player.attack * power;
					enemy.hitPoint -= damage;
					enemy.bodyHitPoint[dir] -= damage;
					//enemy.AddBuff(new DamageBuff(2, 1.0f));
					enemy.GetComponent<PlayerUI>().ShowDamageText(damage);
					Log($"{enemy.name} 受到 {damage} 点伤害.");
					Log($"{enemy.bodyHitPoint[90]} {enemy.bodyHitPoint[0]} {enemy.bodyHitPoint[-90]}");
				}
			}
			public class Move : AttackEffect {
				private readonly bool isRelative;
				private Vector3 position;
				private Move() : base(1) { }
				public Move(Vector3 position) : this() { isRelative = false; this.position = position; }
				public Move(int dist) : this() { isRelative = true; position = new Vector3(dist, 0); }
				public override void Activate(AttackTrail self, Player player, Player enemy, float dir) {
					if (enemy.type == Player.Type.OBSTACLE) return;
					if (isRelative) position = enemy.transform.position + player.direction * position;
					BattleManager.instance.effects.Enqueue(new SpecialEffects.Move(enemy.transform, position, 8f));
				}
			}
		}

		namespace ConcentrateEffects {
			public class Dodge : ConcentrateEffect {
				public Dodge() : base(3) { }
				public override void Activate(ConcentrateTrail self, Player player, Player enemy, AttackTrail trail) {
					player.isDodge = true;
					Log($"{enemy.name} 没打中.");
				}
			}
			public class Heal : ConcentrateEffect {
				private readonly float power;
				public Heal(float power) : base(1) { this.power = power; }
				public override void Activate(ConcentrateTrail self, Player player, Player enemy, AttackTrail trail) {
					player.hitPoint += power;
					Log($"{player.name} 恢复了 {power} HP.");
				}
			}
			public class Remove : ConcentrateEffect {
				public Remove() : base(int.MinValue) { }
				public override void Activate(ConcentrateTrail self, Player player, Player enemy, AttackTrail trail) {
					self.card.Activate(player, self.card.discardEffects);
					self.isExist = false;
				}
			}
		}
	}
	
	public class CardPresets {
		public static Card LoadCardByID(int id,Player player) {
			Card card = new Card(player) {
				id = id
			};
			if (id == 1) {
				Concentrate concentrate = new Concentrate();
				ConcentrateTrail trail = new ConcentrateTrail(card) {
					power = 2,
					radius = 1.0f,
					isHidden = false,
					ranges = new List<(float, float)> { (-30, 30), (30, 90), (90, 150), (150, 210) },
					reactEffects = new List<ConcentrateEffect> { new Effects.ConcentrateEffects.Dodge(), new Effects.ConcentrateEffects.Remove() },
					terminateEffects = new List<ConcentrateEffect> { new Effects.ConcentrateEffects.Remove() }
				};
				concentrate.trail = trail;
				card.concentrate = concentrate;
				card.description = "闪避 (2)";
			} else if (id == 2) {
				AttackTrail trail1 = new AttackTrail {
					power = 1,
					effects = new List<AttackEffect> { new Effects.AttackEffects.Damage(3.0f), new Effects.AttackEffects.Move(1) },
					setPoints = (Vector2 goal) => {
						return new List<Vector2> { Vector2.zero, Vector2.ClampMagnitude(goal, 2) };
					}
				};

				AttackTrail trail2 = new AttackTrail {
					power = 3,
					effects = new List<AttackEffect> { new Effects.AttackEffects.Damage(1.0f) },
					setPoints = (Vector2 goal) => {
						return new List<Vector2> { Vector2.zero, Vector2.ClampMagnitude(goal, 4) };
					}
				};

				Attack attack = new Attack {
					actionPointCost = 1,
					effects = new List<CardEffect> { new Effects.CardEffects.Heal(1.0f) },
					trails = new List<AttackTrail> { trail1, trail2 }
				};

				card.attack = attack;
				card.description = "攻击 (1,3)";
			} else if (id == 3) {
				AttackTrail trail1 = new AttackTrail {
					power = 2,
					effects = new List<AttackEffect> { new Effects.AttackEffects.Damage(3.0f) },
					setPoints = (Vector2 goal) => {
						return new List<Vector2> { Vector2.zero, Vector2.ClampMagnitude(goal, 2) };
					}
				};

				ConcentrateTrail trail2 = new ConcentrateTrail(card) {
					power = 3,
					radius = 1f,
					isHidden = false,
					ranges = new List<(float, float)> { (-30, 30), (30, 90), (90, 150), (150, 210) },
					reactEffects = new List<ConcentrateEffect> { new Effects.ConcentrateEffects.Heal(2.0f), new Effects.ConcentrateEffects.Remove() },
					terminateEffects = new List<ConcentrateEffect> { new Effects.ConcentrateEffects.Remove() }
				};

				Attack attack = new Attack {
					actionPointCost = 1,
					effects = new List<CardEffect> { new Effects.CardEffects.Heal(1.0f) },
					trails = new List<AttackTrail> { trail1 }
				};
				Concentrate concentrate = new Concentrate {
					threshold = 1,
					trail = trail2
				};

				card.attack = attack;
				card.concentrate = concentrate;
				card.description = "攻击 (2), 治疗 (3)";

			}

			return card;
		}

	}

}
