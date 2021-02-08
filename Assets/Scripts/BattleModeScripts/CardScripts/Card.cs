using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

using ZX.Trail;

namespace ZX.Card {

	public class Attack {
		public int actionPointCost;
		public List<AttackTrail> trails;

		/// <summary> 对自身的效果 </summary>
		public List<CardEffect> effects;

		public Attack() {
			effects = new List<CardEffect>();
		}

		public void AttackEffect(Player player) {
			foreach (var effect in effects) effect.Activate(player);
		}
		public void ClearTrails(){
			foreach (var trail in trails) trail.ClearTrail();
		}

		public static implicit operator bool(Attack attack) {
			return attack != null;
		}
	}

	public class Concentrate {
		public ConcentrateTrail trail;

		public int concentrateCount;
		public int threshold;
		public int limit;

		/// <summary> 是否专注过（只有第一次专注时触发专注效果） </summary>
		private bool concentrated;

		/// <summary> 对自身的效果 </summary>
		public List<CardEffect> effects;

		public Concentrate() {
			effects = new List<CardEffect>();
			concentrated = false;
			concentrateCount = 0;
			threshold = 0;
			limit = int.MaxValue;
		}

		public void ConcentrateEffect(Player player) {
			if (!concentrated) {
				concentrated = true;
				foreach (var effect in effects) effect.Activate(player);
			}
		}

		public static implicit operator bool(Concentrate concentrate) {
			return concentrate != null;
		}
	}

	public class Card {
		/// <summary> 卡牌ID </summary>
		public int id;
		/// <summary> 描述 </summary>
		public string description;

		private readonly Player player;

		public Concentrate concentrate;
		public Attack attack;

		#region Effects
		public List<CardEffect> discardEffects;

		public void Activate(Player player, List<CardEffect> effects) {
			foreach (var effect in effects) effect.Activate(player);
			Debug.Log($"{description}'s discard effects activated.");
		}
		#endregion

		public Card(Player player) {
			this.player = player;
			discardEffects = new List<CardEffect>();
		}
		public void Discard() {
			Activate(player, discardEffects);
		}
	}
}