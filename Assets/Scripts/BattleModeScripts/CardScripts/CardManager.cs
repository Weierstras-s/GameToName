using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

using ZX.Utils;

namespace ZX {
	public class CardManager {
		private readonly Player player;

		/// <summary> 卡组 </summary>
		public Queue<int> cardDeckID;

		/// <summary> 弃牌区 </summary>
		public List<int> recycleCardsID;

		/// <summary> 删除区 </summary>
		public Queue<int> deletedCardsID;

		/// <summary> 手卡 </summary>
		public List<Card.Card> handCards;

		/// <summary> 专注区 </summary>
		public List<Card.Card> concentratedCards;

		public Card.Card GetCardByID(int id) {
			return Card.CardPresets.LoadCardByID(id, player);
		}

		public CardManager(List<int> presetCardsID, Player player) {
			this.player = player;
			cardDeckID = new Queue<int>(presetCardsID);
			recycleCardsID = new List<int>();
			deletedCardsID = new Queue<int>();
			handCards = new List<Card.Card>();
			concentratedCards = new List<Card.Card>();
			Shuffle();
		}

		/// <summary> 洗牌 </summary>
		public void Shuffle() {
			List<int> list = new List<int>(cardDeckID);
			list.AddRange(recycleCardsID);
			cardDeckID.Clear();
			recycleCardsID.Clear();
			Common.RandomShuffle(list);
			foreach (int id in list) {
				cardDeckID.Enqueue(id);
			}
		}

		/// <summary> 抽卡 </summary>
		/// <param name="num"> 卡牌数量 </param>
		public void DrawCard(int num) {
			if (num <= 0) return;
			if (cardDeckID.Count < num) Shuffle();
			for (int i = 0; i < num; i++) {
				if (cardDeckID.Count == 0) break;
				handCards.Add(GetCardByID(cardDeckID.Dequeue()));
			}
		}

		/// <summary> 删除某张卡牌（与弃卡不同，不触发弃卡效果） </summary>
		/// <param name="pos"> 卡牌位置 </param>
		public void DeleteCard(int pos) {
			var card = handCards[pos];
			handCards.RemoveAt(pos);
			recycleCardsID.Add(card.id);
			card.attack?.ClearTrails();
			card.concentrate?.trail.ClearTrail();
		}

		/// <summary> 弃卡 </summary>
		/// <param name="pos"> 卡牌位置 </param>
		public void RemoveCard(int pos) {
			handCards[pos].Activate(player, handCards[pos].discardEffects);
			if (concentratedCards.Contains(handCards[pos])) {
				concentratedCards.Remove(handCards[pos]);
			}
			DeleteCard(pos);
		}

		/// <summary> 是否能攻击 </summary>
		/// <param name="pos"> 卡牌在手卡中的序号 </param>
		public int Attackable(int pos) {
			Card.Card card = handCards[pos];
			if (!card.attack) return -1;
			if (card.concentrate && card.concentrate.concentrateCount < card.concentrate.threshold) return -1;
			if (concentratedCards.Contains(card)) return -1;
			if (player.actionPoint < card.attack.actionPointCost) return -1;
			return card.attack.trails.Count;
		}

		/// <summary> 攻击 </summary>
		/// <param name="pos"> 卡牌在手卡中的序号 </param>
		public void Attack(int pos, List<Vector2> goals) {
			Debug.Assert(goals.Count == handCards[pos].attack.trails.Count, "攻击目标与轨迹数量不一致");
			var card = handCards[pos];
			DeleteCard(pos);

			card.attack.AttackEffect(player);
			for (int i = 0; i < goals.Count; i++) {
				card.attack.trails[i].SetPoints(player.transform.position, goals[i]);
				card.attack.trails[i].Attack(player);
			}
			player.actionPoint -= card.attack.actionPointCost;

		}

		/// <summary> 是否能专注或取消专注 </summary>
		/// <param name="pos"> 卡牌在手卡中的序号 </param>
		public int Concentratable(int pos) {
			if (!handCards[pos].concentrate) return -1;
			if (concentratedCards.Contains(handCards[pos])) return 0;
			if (concentratedCards.Count >= player.maxConcentratePoint) return -1;
			//if (!handCards[pos].concentrate.concentrate(player)) return -1;
			return 1;
		}

		/// <summary> 专注或取消专注 </summary>
		/// <param name="pos"> 卡牌在手卡中的序号 </param>
		public void Concentrate(int pos, List<Vector2> goal) {
			if (goal.Count == 0) {
				Debug.Assert(concentratedCards.Contains(handCards[pos]), "无法取消专注");
				concentratedCards.Remove(handCards[pos]);
				handCards[pos].concentrate.trail.ClearTrail();
			} else {
				Debug.Assert(goal.Count == 1, "专注数量与轨迹数量不一致");
				concentratedCards.Add(handCards[pos]);
				handCards[pos].concentrate.ConcentrateEffect(player);
				handCards[pos].concentrate.trail.DrawTrail(player, goal[0]);
			}
		}

		/// <summary> 仅保留专注且未作用的卡 </summary>
		public void RemoveCardsAtRoundEnd() {
			foreach (var card in handCards) {
				// 保留所有专注的卡
				if (concentratedCards.Contains(card)) continue;
				// 直接丢弃
				card.Discard();
				recycleCardsID.Add(card.id);
			}
			handCards.Clear();
			handCards.AddRange(concentratedCards);
		}
		public void RemoveCardsAtRoundStart() {
			handCards.Clear();
			foreach (var card in concentratedCards) {
				if (card.concentrate.trail.delayedDeletion) {
					// 延迟丢弃
					card.Discard();
					card.concentrate.trail.isExist = false;
				}
				if (!card.concentrate.trail.isExist) recycleCardsID.Add(card.id);
				else {
					card.concentrate.concentrateCount++;
					handCards.Add(card);
				}
			}
			concentratedCards.Clear();
			concentratedCards.AddRange(handCards);
		}

	}

}
